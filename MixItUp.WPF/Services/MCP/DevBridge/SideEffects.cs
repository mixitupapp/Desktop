#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    /// <summary>
    /// Base for the result of a tool that does something, carrying what the doing opened.
    /// </summary>
    /// <remarks>
    /// Shared rather than repeated because the two ways of driving this app -- running its command
    /// (ui_invoke) and pressing its control (ui_click) -- produce the same two side effects and an agent
    /// should not have to learn them twice.
    /// </remarks>
    public abstract class ActionResult : DevBridgeResult
    {
        [Description("Windows that appeared during the call, with a handle for each. A press that opens a command editor shows up here.")]
        public List<string> WindowsOpened { get; set; }

        [Description("Dialog overlays that appeared during the call, with a handle for the dialog's content. These are content inside an existing window rather than real windows, so they never add an entry to ui_list_windows and would otherwise be invisible to a caller that only counted windows.")]
        public List<string> DialogsOpened { get; set; }

        [Description("What to do next to observe the effect.")]
        public string Hint { get; set; }
    }

    /// <summary>
    /// What was on screen before an action ran, so that what the action opened can be named afterwards.
    /// </summary>
    internal sealed class UISnapshot
    {
        public int WindowCount;

        /// <summary>
        /// The dialog overlays already up. Held by reference and compared by reference, because two
        /// successive dialogs are distinct objects in the same host and counting would miss the swap.
        /// </summary>
        public List<DialogHost> OpenDialogs = new List<DialogHost>();
    }

    /// <summary>
    /// Notices what an action opened.
    /// </summary>
    /// <remarks>
    /// <b>Two kinds of thing count as "a dialog appeared" here and only one of them is a window.</b> This
    /// app opens 28 of its windows with Show and 5 with ShowDialog, which
    /// <see cref="Application.Current"/>.Windows reports either way. But its everyday prompts -- every
    /// DialogHelper.ShowMessage, ShowConfirmation, and ShowCustom -- are MaterialDesign
    /// <see cref="DialogHost"/> overlays, which are content swapped into a control inside the window that
    /// is already open. Nothing about the window collection changes when one appears.
    /// <para>
    /// A caller that pressed a button and got an overlay would therefore see an empty WindowsOpened and
    /// conclude nothing happened, which is exactly backwards: the overlay is modal to the user and is the
    /// most important thing on the screen. Both are reported, and they are reported separately because
    /// what to do next differs -- a window is addressable by handle in ui_list_windows, an overlay is
    /// only reachable through the window that hosts it.
    /// </para>
    /// </remarks>
    internal static class SideEffects
    {
        /// <summary>Depth cap, matching the tree walkers, so a cycle cannot hang the UI thread.</summary>
        private const int MaxDepth = 200;

        /// <summary>
        /// Records the windows and overlays already present. Must run on the UI thread.
        /// </summary>
        public static UISnapshot Capture()
        {
            UISnapshot snapshot = new UISnapshot();

            try
            {
                if (Application.Current == null)
                {
                    return snapshot;
                }

                foreach (Window window in Application.Current.Windows)
                {
                    if (window == null)
                    {
                        continue;
                    }

                    snapshot.WindowCount++;
                    CollectOpenDialogs(window, snapshot.OpenDialogs, 0);
                }
            }
            catch (Exception)
            {
                // Never let taking a baseline be what fails the call it was taken for.
            }

            return snapshot;
        }

        /// <summary>
        /// Fills in what appeared since <paramref name="before"/> was captured. Must run on the UI thread.
        /// </summary>
        public static void Observe(ActionResult result, UISnapshot before)
        {
            result.WindowsOpened = NewWindows(before);
            result.DialogsOpened = NewDialogs(before);
        }

        /// <summary>
        /// Titles of windows that appeared, with a handle for each.
        /// </summary>
        /// <remarks>
        /// Show adds to the window collection synchronously, so a window opened before a handler's first
        /// await is caught. Position in the collection is the discriminator rather than identity, which is
        /// enough because WPF only ever appends.
        /// </remarks>
        private static List<string> NewWindows(UISnapshot before)
        {
            List<string> opened = new List<string>();

            try
            {
                if (Application.Current == null || Application.Current.Windows.Count <= before.WindowCount)
                {
                    return opened;
                }

                int index = 0;
                foreach (Window window in Application.Current.Windows)
                {
                    if (window == null)
                    {
                        continue;
                    }
                    if (index++ < before.WindowCount)
                    {
                        continue;
                    }

                    string handle = HandleRegistry.Instance.HandleFor(window, "e");
                    opened.Add($"{window.GetType().Name} ({handle}) \"{window.Title}\"");
                }
            }
            catch (Exception)
            {
                // Never let reporting a side effect be what fails the call that caused it.
            }

            return opened;
        }

        /// <summary>
        /// Dialog overlays that were not up before and are now.
        /// </summary>
        private static List<string> NewDialogs(UISnapshot before)
        {
            List<string> opened = new List<string>();

            try
            {
                foreach (DialogHost host in FindOpenDialogs())
                {
                    if (!ContainsReference(before.OpenDialogs, host))
                    {
                        opened.Add(Describe(host));
                    }
                }
            }
            catch (Exception)
            {
                // As above.
            }

            return opened;
        }

        /// <summary>
        /// Every dialog overlay currently up, across every open window. Must run on the UI thread.
        /// </summary>
        public static List<DialogHost> FindOpenDialogs()
        {
            List<DialogHost> found = new List<DialogHost>();

            try
            {
                if (Application.Current == null)
                {
                    return found;
                }

                foreach (Window window in Application.Current.Windows)
                {
                    CollectOpenDialogs(window, found, 0);
                }
            }
            catch (Exception)
            {
                // As above.
            }

            return found;
        }

        /// <summary>
        /// One line naming an overlay and how to reach into it.
        /// </summary>
        public static string Describe(DialogHost host)
        {
            string hostName = ElementDescriber.GetName(host);
            string where = string.IsNullOrEmpty(hostName) ? "DialogHost" : $"DialogHost #{hostName}";

            object content = null;
            try
            {
                content = host.CurrentSession?.Content;
            }
            catch (Exception)
            {
                // A session torn down between finding it and reading it. The host is still worth naming.
            }

            if (content is DependencyObject element)
            {
                string handle = HandleRegistry.Instance.HandleFor(element, "e");
                return $"{content.GetType().Name} ({handle}) in {where}";
            }

            return content != null ? $"{content.GetType().Name} in {where}" : where;
        }

        /// <summary>
        /// The overlay up inside one window, or null. Must run on the UI thread.
        /// </summary>
        public static string DescribeOpenDialogIn(Window window)
        {
            List<DialogHost> found = new List<DialogHost>();
            CollectOpenDialogs(window, found, 0);
            return found.Count > 0 ? Describe(found[0]) : null;
        }

        /// <summary>
        /// The standing advice attached to a result that opened something.
        /// </summary>
        public static string OpenedHint(ActionResult result)
        {
            bool window = result.WindowsOpened != null && result.WindowsOpened.Count > 0;
            bool dialog = result.DialogsOpened != null && result.DialogsOpened.Count > 0;

            if (window && dialog)
            {
                return "A window and a dialog overlay both opened. The read tools default to the active window, so a following call with no element reads the new window; pass the overlay's handle to read the overlay.";
            }
            if (window)
            {
                return "A window opened. The read tools default to the active window, so a following ui_screenshot or ui_dump_tree with no element will read the new one.";
            }
            if (dialog)
            {
                return "A dialog overlay opened. It is content inside an existing window, not a new window, so read it by passing its handle to ui_dump_tree or ui_get_text rather than expecting a new entry in ui_list_windows.";
            }

            return null;
        }

        private static void CollectOpenDialogs(DependencyObject visual, List<DialogHost> found, int depth)
        {
            if (visual == null || depth > MaxDepth)
            {
                return;
            }

            // VisualTreeHelper refuses anything that is not a visual, and this app's trees do contain
            // content elements.
            if (!(visual is Visual) && !(visual is System.Windows.Media.Media3D.Visual3D))
            {
                return;
            }

            // Kept walking past a match on purpose: a dialog's own content can host another DialogHost,
            // which is how a confirmation raised from inside a dialog appears.
            if (visual is DialogHost host && host.IsOpen && !ContainsReference(found, host))
            {
                found.Add(host);
            }

            int childCount;
            try
            {
                childCount = VisualTreeHelper.GetChildrenCount(visual);
            }
            catch (Exception)
            {
                return;
            }

            for (int i = 0; i < childCount; i++)
            {
                CollectOpenDialogs(VisualTreeHelper.GetChild(visual, i), found, depth + 1);
            }
        }

        private static bool ContainsReference(List<DialogHost> hosts, DialogHost host)
        {
            foreach (DialogHost candidate in hosts)
            {
                if (ReferenceEquals(candidate, host))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
