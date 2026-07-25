using MixItUp.Base;
using MixItUp.Base.Model;
using MixItUp.Base.Util;
using ModelContextProtocol;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services.MCP.Tools
{
    /// <summary>
    /// Shared argument validation and audit logging for the MCP tools. Every validation failure here
    /// is something the calling agent can correct on its own, so the messages say what to do rather
    /// than just what broke.
    /// </summary>
    /// <remarks>
    /// Consent for the state-changing tools is deliberately left to the client, which is where the
    /// MCP specification puts it. An earlier revision enforced it server-side with elicitation, which
    /// works against the Claude Code CLI but is auto-declined by clients that advertise form-mode
    /// support without implementing a UI for it, leaving those tools permanently unusable. The
    /// annotations plus this audit trail carry that responsibility instead.
    /// </remarks>
    internal static class ToolHelpers
    {
        public static void RequireSettingsLoaded()
        {
            if (ChannelSession.Settings == null)
            {
                throw new McpException("No settings profile is loaded yet. Call get_status and wait until isSettingsLoaded is true.");
            }
        }

        public static Guid ParseID(string id, string parameterName)
        {
            if (!Guid.TryParse(id, out Guid result))
            {
                throw new McpException($"The {parameterName} value '{id}' is not a valid GUID.");
            }
            return result;
        }

        public static StreamingPlatformTypeEnum ParsePlatform(string platform)
        {
            if (string.IsNullOrEmpty(platform))
            {
                return StreamingPlatformTypeEnum.All;
            }

            // Enum.TryParse alone accepts every member, including the obsolete Mixer / Trovo /
            // Glimesh / Facebook entries plus None and Mock. Those parse cleanly and then fail
            // confusingly further down, so gate on the supported set instead.
            if (!Enum.TryParse<StreamingPlatformTypeEnum>(platform, ignoreCase: true, out StreamingPlatformTypeEnum result)
                || (result != StreamingPlatformTypeEnum.All && !StreamingPlatforms.IsValidPlatform(result)))
            {
                throw new McpException($"Unknown or unsupported platform '{platform}'. Valid values are: {string.Join(", ", StreamingPlatforms.SupportedPlatforms)}, or All.");
            }

            return result;
        }

        /// <summary>
        /// Records every state-changing tool call. ForceLog rather than Log because the default log
        /// level drops Information, and an audit trail that only exists on verbose builds is not one.
        /// </summary>
        public static void LogToolAction(string toolName, string detail)
        {
            Logger.ForceLog(LogLevel.Information, $"MCP tool '{toolName}' invoked: {detail}");
        }

        /// <summary>
        /// The ceiling on how long a single tool call may wait before it gives up and reports back.
        /// Generous enough for the slowest legitimate work, which is get_user performing a live
        /// platform lookup on top of loading user data.
        /// </summary>
        public static readonly TimeSpan ToolTimeout = TimeSpan.FromSeconds(20);

        /// <summary>
        /// Bounds how long a tool call can wait for the application to answer.
        /// </summary>
        /// <remarks>
        /// This is not here to bound command execution, which is already asynchronous: CommandService
        /// enqueues onto a background runner and returns as soon as the work is accepted, so a command
        /// that legitimately runs for half a minute never holds a request thread.
        /// <para>
        /// It is here because several of these paths end up inside ThreadSafeObservableCollection,
        /// which marshals through DispatcherHelper.Dispatcher.Invoke with no timeout of its own. If the
        /// UI thread is wedged, behind a modal dialog for instance, those calls block indefinitely, and
        /// a hung tool call is indistinguishable to the caller from a hung application.
        /// </para>
        /// <para>
        /// The abandoned work is deliberately not cancelled. The blocking call is a synchronous
        /// dispatcher Invoke inside shipping code with no cancellation token to pass it, so the thread
        /// stays parked until the UI frees up. What this buys is a caller that gets a diagnosable
        /// answer rather than waiting forever.
        /// </para>
        /// </remarks>
        public static async Task<T> RunWithTimeout<T>(string toolName, Func<Task<T>> work)
        {
            Task<T> task = Task.Run(work);

            using (CancellationTokenSource timeoutCancellation = new CancellationTokenSource())
            {
                Task timeout = Task.Delay(ToolTimeout, timeoutCancellation.Token);
                if (await Task.WhenAny(task, timeout) != task)
                {
                    Logger.ForceLog(LogLevel.Warning, $"MCP tool '{toolName}' exceeded {ToolTimeout.TotalSeconds:0}s and was abandoned. The UI thread may be blocked.");
                    throw new McpException($"The '{toolName}' tool did not respond within {ToolTimeout.TotalSeconds:0} seconds and was abandoned. This usually means the Mix It Up window is blocked, for example by an open dialog. The operation may still complete on its own, so re-read state before retrying.");
                }

                timeoutCancellation.Cancel();
            }

            // Awaiting the completed task rather than returning its Result so that any exception it
            // raised, including the McpException validation failures, surfaces unwrapped.
            return await task;
        }
    }
}
