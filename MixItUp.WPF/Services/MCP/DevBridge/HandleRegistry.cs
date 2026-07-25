#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    /// <summary>
    /// The outcome of resolving a handle back to a live object.
    /// </summary>
    internal readonly struct HandleLookup
    {
        public string Status { get; }

        public string Message { get; }

        public object Target { get; }

        private HandleLookup(string status, string message, object target)
        {
            this.Status = status;
            this.Message = message;
            this.Target = target;
        }

        public bool IsOk { get { return this.Status == DevBridgeStatus.Ok; } }

        public static HandleLookup Ok(object target) { return new HandleLookup(DevBridgeStatus.Ok, null, target); }

        public static HandleLookup Fail(string status, string message) { return new HandleLookup(status, message, null); }
    }

    /// <summary>
    /// Hands out stable string handles for objects in the running UI and resolves them back again.
    /// </summary>
    /// <remarks>
    /// Every reference held here is weak in both directions, so the bridge can never be the reason a
    /// window, control, or view model fails to be collected. A handle whose target has been collected
    /// resolves to <see cref="DevBridgeStatus.StaleHandle"/>.
    /// <para>
    /// <b>Why handles are not simply keyed on the visual.</b> This app sets
    /// VirtualizationMode="Recycling" in 19 XAML files, including the chat list, the command history,
    /// and the users grid. A recycled container keeps its <see cref="DependencyObject"/> identity while
    /// being re-bound to a different DataContext. Keying purely on the visual therefore produces a
    /// handle that looks stable and quietly means a different row after a scroll, so an agent that
    /// reads, acts, and re-reads would be acting on the wrong item with no indication anything moved.
    /// </para>
    /// <para>
    /// The fix has two halves. Item containers are additionally registered under their DataContext, so
    /// the model behind a row has its own handle that survives recycling entirely and is the thing to
    /// hold for Phase B property work. And every visual inside a virtualized region records the
    /// DataContext it was bound to when the handle was issued, which is compared by reference on every
    /// resolve. When a container has been recycled underneath a handle the mismatch is detected and
    /// reported as a stale handle, turning a silent wrong-row action into a loud, correct error that
    /// tells the agent to re-read.
    /// </para>
    /// </remarks>
    internal sealed class HandleRegistry
    {
        /// <summary>
        /// The registry outlives individual tool calls so that handles stay valid across them, which is
        /// the whole point of a handle. Access is serialized by <see cref="DevBridgeGate"/> and further
        /// guarded by <see cref="sync"/>.
        /// </summary>
        public static readonly HandleRegistry Instance = new HandleRegistry();

        /// <summary>
        /// Dead entries are swept once the table grows past this, so a long session spent scrolling a
        /// virtualized list does not accumulate them without bound.
        /// </summary>
        private const int PruneThreshold = 2048;

        private readonly object sync = new object();

        // Weak keys: registering an object never keeps it alive.
        private readonly ConditionalWeakTable<object, Entry> entryByObject = new ConditionalWeakTable<object, Entry>();

        // Weak values, held via the entry, for the same reason.
        private readonly Dictionary<string, Entry> entryByHandle = new Dictionary<string, Entry>(StringComparer.Ordinal);

        private int nextId;

        private sealed class Entry
        {
            public string Handle;

            public WeakReference<object> Target;

            /// <summary>
            /// The DataContext this visual carried when the handle was issued, recorded only inside a
            /// virtualized region. Null means there is nothing to verify.
            /// </summary>
            public WeakReference<object> ExpectedDataContext;

            public bool VerifyDataContext;

            public string TypeName;
        }

        /// <summary>
        /// Returns the existing handle for <paramref name="target"/> or issues a new one.
        /// </summary>
        /// <param name="expectedDataContext">
        /// The DataContext to pin this handle to. Pass null to skip verification. When a handle already
        /// exists but was pinned to a different DataContext the container has been recycled, so a fresh
        /// handle is issued and the old one is left to resolve as stale. That ordering matters: a
        /// re-dump after scrolling must hand back usable handles while any handle the agent captured
        /// before the scroll must not silently start meaning a different row.
        /// </param>
        public string GetOrCreate(object target, string prefix, object expectedDataContext)
        {
            if (target == null)
            {
                return null;
            }

            lock (this.sync)
            {
                if (this.entryByObject.TryGetValue(target, out Entry existing))
                {
                    if (!existing.VerifyDataContext || SameDataContext(existing, expectedDataContext))
                    {
                        return existing.Handle;
                    }

                    // Recycled underneath the old handle. Retire it rather than repointing it.
                    this.entryByObject.Remove(target);
                }

                this.nextId++;
                Entry entry = new Entry()
                {
                    Handle = prefix + this.nextId.ToString(),
                    Target = new WeakReference<object>(target),
                    VerifyDataContext = expectedDataContext != null,
                    ExpectedDataContext = expectedDataContext != null ? new WeakReference<object>(expectedDataContext) : null,
                    TypeName = target.GetType().Name,
                };

                this.entryByObject.Add(target, entry);
                this.entryByHandle[entry.Handle] = entry;

                if (this.entryByHandle.Count > PruneThreshold)
                {
                    this.PruneDead();
                }

                return entry.Handle;
            }
        }

        /// <summary>
        /// The handle for an object the caller has already resolved, reusing the existing one when there
        /// is one.
        /// </summary>
        /// <remarks>
        /// This is what to call when you are not registering an object off a tree walk but simply need a
        /// handle for something you already hold.
        /// <para>
        /// It exists because <see cref="GetOrCreate"/> reads a null <c>expectedDataContext</c> as an
        /// assertion that the object is pinned to no DataContext, which is correct for a walker declaring
        /// what it found but wrong for a caller that has no opinion. Passing null for a row already
        /// registered under its item therefore looked like a recycle: the correctly pinned handle was
        /// retired and a fresh unpinned one issued, so reading a property off a row both churned handles
        /// and quietly dropped that row's recycling protection.
        /// </para>
        /// </remarks>
        public string HandleFor(object target, string prefix)
        {
            if (target == null)
            {
                return null;
            }

            lock (this.sync)
            {
                if (this.entryByObject.TryGetValue(target, out Entry existing))
                {
                    return existing.Handle;
                }
            }

            return this.GetOrCreate(target, prefix, null);
        }

        /// <summary>
        /// Resolves a handle back to its live object, or explains why it cannot.
        /// </summary>
        public HandleLookup Resolve(string handle)
        {
            if (string.IsNullOrWhiteSpace(handle))
            {
                return HandleLookup.Fail(DevBridgeStatus.NotFound, "No handle was supplied.");
            }

            handle = handle.Trim();

            lock (this.sync)
            {
                if (!this.entryByHandle.TryGetValue(handle, out Entry entry))
                {
                    return HandleLookup.Fail(DevBridgeStatus.NotFound, $"Handle '{handle}' was never issued by this instance. Handles are per-process and are not stable across an app restart. Call ui_list_windows or ui_dump_tree to get current handles.");
                }

                if (!entry.Target.TryGetTarget(out object target))
                {
                    this.entryByHandle.Remove(handle);
                    return HandleLookup.Fail(DevBridgeStatus.StaleHandle, $"Handle '{handle}' referred to a {entry.TypeName} that has since been collected. Re-read the tree with ui_dump_tree to get current handles.");
                }

                if (entry.VerifyDataContext && !SameDataContext(entry, CurrentDataContext(target)))
                {
                    return HandleLookup.Fail(DevBridgeStatus.StaleHandle, $"Handle '{handle}' referred to a {entry.TypeName} inside a virtualized list, and that container has since been recycled onto a different item. Acting on it now would target the wrong row. Re-read the tree with ui_dump_tree to get current handles, or hold the DataContext handle instead, which survives recycling.");
                }

                return HandleLookup.Ok(target);
            }
        }

        /// <summary>
        /// The DataContext currently on an object, or null when it is not a framework element.
        /// </summary>
        public static object CurrentDataContext(object target)
        {
            if (target is FrameworkElement element)
            {
                return element.DataContext;
            }
            if (target is FrameworkContentElement contentElement)
            {
                return contentElement.DataContext;
            }
            return null;
        }

        private static bool SameDataContext(Entry entry, object current)
        {
            object expected = null;
            entry.ExpectedDataContext?.TryGetTarget(out expected);
            return ReferenceEquals(expected, current);
        }

        private void PruneDead()
        {
            List<string> dead = new List<string>();
            foreach (KeyValuePair<string, Entry> pair in this.entryByHandle)
            {
                if (!pair.Value.Target.TryGetTarget(out _))
                {
                    dead.Add(pair.Key);
                }
            }
            foreach (string handle in dead)
            {
                this.entryByHandle.Remove(handle);
            }
        }
    }
}
