#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using MixItUp.Base.Util;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    /// <summary>
    /// Outcome discriminators shared by every dev bridge tool.
    /// </summary>
    /// <remarks>
    /// The product tools throw <see cref="ModelContextProtocol.McpException"/> with prose, which reads
    /// well to a human but gives an agent nothing to branch on. These values are carried as a field on
    /// an otherwise successful result instead, because the distinction between them implies a genuinely
    /// different next action: give up, re-read the tree, or back off and retry.
    /// <para>
    /// Argument validation still throws, matching the product tools. The split is deliberate: a bad
    /// argument is the agent's own mistake and is fixed by calling again differently, whereas these
    /// are states of the application that the agent has to react to.
    /// </para>
    /// </remarks>
    internal static class DevBridgeStatus
    {
        public const string Ok = "ok";

        /// <summary>The thing asked for does not exist. Give up or look somewhere else.</summary>
        public const string NotFound = "not_found";

        /// <summary>
        /// The handle no longer refers to what it did. Re-read the tree and use fresh handles. This is
        /// the expected outcome after an items control recycles its containers, and is reported rather
        /// than silently resolving to whatever now occupies that container.
        /// </summary>
        public const string StaleHandle = "stale_handle";

        /// <summary>The UI thread did not answer in time. Back off and retry.</summary>
        public const string DispatcherTimeout = "dispatcher_timeout";

        /// <summary>Another dev bridge call held the gate. Retry.</summary>
        public const string Busy = "busy";

        /// <summary>There is no WPF application or it is shutting down. Nothing to inspect.</summary>
        public const string NoUI = "no_ui";

        /// <summary>
        /// The property exists but cannot be written. Distinct from not_found because the answer is to
        /// find the property this one derives from, not to look somewhere else: a great many view model
        /// properties here are computed getters over another property that is settable.
        /// </summary>
        public const string NotSettable = "not_settable";

        /// <summary>
        /// The property exists and is writable but the supplied text does not convert to its type. The
        /// message carries what the type is, and for an enum what its legal values are, so the retry is
        /// informed rather than a guess.
        /// </summary>
        public const string CoercionFailed = "coercion_failed";

        /// <summary>
        /// The command exists but refused to run, because CanExecute returned false. Reported separately
        /// from an outright failure because it is the app declining rather than breaking, and it is the
        /// answer to "why is that button greyed out" -- the question UI Automation structurally cannot
        /// answer, since the reason lives in a view model UIA has no concept of.
        /// </summary>
        public const string CannotExecute = "cannot_execute";

        /// <summary>Something threw. The message carries the detail.</summary>
        public const string Error = "error";
    }

    /// <summary>
    /// Base for every dev bridge result so that the outcome is always machine-readable.
    /// </summary>
    public abstract class DevBridgeResult
    {
        [Description("Outcome discriminator. Branch on this, not on the message text. 'ok' means the payload is valid. 'stale_handle' means re-read the tree and use fresh handles. 'dispatcher_timeout' or 'busy' mean back off and retry. 'not_found' means give up. 'not_settable' means the property is read-only, so set whatever it derives from instead. 'coercion_failed' means the value did not convert to the property's type, and the message says what that type is. 'cannot_execute' means the command refused because CanExecute is false, which is the app declining rather than breaking. 'no_ui' means the window is gone. 'error' means something threw.")]
        public string Status { get; set; } = DevBridgeStatus.Ok;

        [Description("Human-readable detail, and where applicable what to do about it. Null when status is 'ok'.")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Serializes dev bridge calls and marshals their bodies onto the UI thread with a real timeout.
    /// </summary>
    /// <remarks>
    /// Two separate hazards are handled here.
    /// <para>
    /// <b>Re-entrancy.</b> Only one dev bridge call runs at a time. This matters because a modal dialog
    /// pumps messages, and a nested pump will happily dispatch a second tool call while the first is
    /// still on the stack, leaving two calls interleaved inside one dispatcher frame mutating the same
    /// object graph. The gate covers only the dev bridge: the product tools are a shipped, separately
    /// verified surface and are deliberately left untouched.
    /// </para>
    /// <para>
    /// <b>Deadlock.</b> Dispatch uses <see cref="Dispatcher.InvokeAsync(Action, DispatcherPriority)"/>
    /// rather than the synchronous <see cref="DispatcherHelper"/> abstraction, which has no timeout and
    /// no cancellation token, so a wedged UI thread would park the request thread forever. Every wait
    /// here is bounded, and the two bounds together stay under the 20 second ceiling the product tools
    /// use, so a caller always gets a diagnosable answer.
    /// </para>
    /// <para>
    /// Priority is Normal on purpose. Normal priority operations still run inside a modal dialog's
    /// nested message pump, which means the tree stays readable while the app sits behind a dialog.
    /// That is precisely the support scenario this exists for.
    /// </para>
    /// </remarks>
    internal static class DevBridgeGate
    {
        private static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        /// <summary>How long to wait for another dev bridge call to finish.</summary>
        public static readonly TimeSpan AcquireTimeout = TimeSpan.FromSeconds(8);

        /// <summary>How long to wait for the UI thread to run the work.</summary>
        public static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Runs <paramref name="uiWork"/> on the UI thread, one dev bridge call at a time, and converts
        /// every failure mode into a status on the result rather than an exception or a hang.
        /// </summary>
        public static async Task<T> RunOnUI<T>(string toolName, Func<T> uiWork) where T : DevBridgeResult, new()
        {
            if (!await gate.WaitAsync(AcquireTimeout))
            {
                return Fail<T>(DevBridgeStatus.Busy, $"Another dev bridge call was still running after {AcquireTimeout.TotalSeconds:0} seconds. Dev bridge calls are serialized to keep concurrent callers from interleaving inside one dispatcher frame. Retry.");
            }

            try
            {
                Application application = Application.Current;
                if (application == null)
                {
                    return Fail<T>(DevBridgeStatus.NoUI, "There is no WPF Application instance. The app is not running a UI.");
                }

                Dispatcher dispatcher = application.Dispatcher;
                if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                {
                    return Fail<T>(DevBridgeStatus.NoUI, "The UI dispatcher is shutting down or has shut down. The app is closing.");
                }

                // Already on the UI thread would mean a re-entrant call through a nested pump. Run
                // inline rather than deadlocking against ourselves.
                if (dispatcher.CheckAccess())
                {
                    return Invoke(toolName, uiWork);
                }

                DispatcherOperation<T> operation = dispatcher.InvokeAsync(() => Invoke(toolName, uiWork), DispatcherPriority.Normal);

                using (CancellationTokenSource timeoutCancellation = new CancellationTokenSource())
                {
                    Task delay = Task.Delay(DispatchTimeout, timeoutCancellation.Token);
                    if (await Task.WhenAny(operation.Task, delay) != operation.Task)
                    {
                        // Only succeeds while still queued. If it has already begun executing the work
                        // continues on the UI thread and is abandoned, the same tradeoff the product
                        // tools make, because there is no way to interrupt work already running there.
                        operation.Abort();

                        Logger.ForceLog(LogLevel.Warning, $"MCP dev bridge '{toolName}' did not reach the UI thread within {DispatchTimeout.TotalSeconds:0}s.");
                        return Fail<T>(DevBridgeStatus.DispatcherTimeout, $"The UI thread did not run this within {DispatchTimeout.TotalSeconds:0} seconds. It is likely blocked. The work may still complete on its own, so re-read state before retrying.");
                    }

                    timeoutCancellation.Cancel();
                }

                return await operation.Task;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return Fail<T>(DevBridgeStatus.Error, $"{ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Runs the body on the UI thread, turning a throw into an 'error' result so that an exception
        /// raised on the UI thread cannot escape into WPF's unhandled exception path and trip the
        /// crash handler.
        /// </summary>
        private static T Invoke<T>(string toolName, Func<T> uiWork) where T : DevBridgeResult, new()
        {
            try
            {
                return uiWork();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return Fail<T>(DevBridgeStatus.Error, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        private static T Fail<T>(string status, string message) where T : DevBridgeResult, new()
        {
            return new T() { Status = status, Message = message };
        }
    }
}
