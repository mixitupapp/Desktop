#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    /// <summary>
    /// The read side of the dev bridge: what windows are open, what is in them, and what they say.
    /// </summary>
    /// <remarks>
    /// Present only in a Dev build. See Directory.Build.props for the gate.
    /// <para>
    /// These tools exist because the rendered surface is not where the answer to "what does the app
    /// think is going on" lives. UI Automation can report that a button is disabled but structurally
    /// cannot report why, because the reason is in a view model that UIA has no concept of. Every dump
    /// here carries the DataContext type and a handle for it, which is what makes the rest reachable.
    /// </para>
    /// </remarks>
    [McpServerToolType]
    public class UITools
    {
        [McpServerTool(Name = "ui_list_windows", ReadOnly = true)]
        [Description("List every open WPF window in the running Mix It Up instance, with a handle for each. Start here: the handles this returns are the roots for ui_dump_tree. A modal dialog appearing in this list is usually the explanation for the app being unresponsive.")]
        public static Task<WindowListResult> ListWindows()
        {
            return DevBridgeGate.RunOnUI("ui_list_windows", () =>
            {
                WindowListResult result = new WindowListResult();

                foreach (Window window in Application.Current.Windows)
                {
                    if (window == null)
                    {
                        continue;
                    }

                    object dataContext = window.DataContext;

                    result.Windows.Add(new WindowInfo()
                    {
                        Handle = HandleRegistry.Instance.GetOrCreate(window, "e", null),
                        Type = window.GetType().Name,
                        Title = window.Title,
                        IsActive = window.IsActive,
                        IsVisible = window.IsVisible,
                        IsModal = IsModal(window),
                        IsMainWindow = ReferenceEquals(window, Application.Current.MainWindow),
                        Width = Math.Round(window.ActualWidth),
                        Height = Math.Round(window.ActualHeight),
                        DataContextType = dataContext?.GetType().Name,
                        DataContextHandle = dataContext != null ? HandleRegistry.Instance.GetOrCreate(dataContext, "d", null) : null,
                    });
                }

                if (result.Windows.Count == 0)
                {
                    result.Status = DevBridgeStatus.NotFound;
                    result.Message = "No windows are open. The app may still be starting up.";
                }

                return result;
            });
        }

        [McpServerTool(Name = "ui_dump_tree", ReadOnly = true)]
        [Description("Dump the live visual tree as one indented line per element, with a stable handle at the start of each line. Each line carries the element type, x:Name, displayed text, state flags, size, and the DataContext type plus its own handle where a new one is introduced. Collapsed subtrees are skipped by default, which matters in this tab-based app because most of the tree is built but not shown. Prefer narrowing with handle or filter over raising depth.")]
        public static Task<TreeResult> DumpTree(
            [Description("What to read. Accepts three forms: a handle from a previous call (e12), an x:Name prefixed with # (#ChatMessageTextBox), or a type name (ChatControl). Name and type forms resolve fresh every call and need no prior discovery, so an agent that knows this app can skip the tree dump entirely. Omit to use the active window, falling back to the main window.")] string element = null,
            [Description("How many levels below the root to walk. Clamped to 1-60, default 25.")] int depth = 25,
            [Description("Case-insensitive substring. When given, only matching elements and the path down to them are shown, and matches are marked with '*'. Matches against type name, x:Name, displayed text, and DataContext type name.")] string filter = null,
            [Description("Maximum elements to emit before truncating. Clamped to 1-2000, default 300. On truncation the result explains how to narrow the request.")] int maxElements = 300,
            [Description("Include Visibility=Collapsed subtrees. Default false. Turn this on when the question is why something is not showing.")] bool includeInvisible = false,
            [Description("Collapse theme and control template plumbing, promoting its children so only meaningful elements get a line. Default true, and worth keeping: this app is built on MaterialDesignInXAML, where one icon button expands into eleven template elements. Set false for the literal visual tree, which you need when reasoning about layout or hit testing, at roughly five times the output.")] bool collapseChrome = true,
            [Description("Emit only what can be acted on (buttons, text boxes, selectors, list rows) plus the view model boundaries that locate them. This is the cheapest read of a screen and is usually enough to decide where to go next, in the same way an accessibility tool surfaces a dozen actionable things rather than every control. Default false.")] bool interactiveOnly = false)
        {
            return DevBridgeGate.RunOnUI("ui_dump_tree", () =>
            {
                TreeResult result = new TreeResult();

                DependencyObject root = ResolveTarget(element, result);
                if (root == null)
                {
                    return result;
                }

                TreeDumpOptions options = new TreeDumpOptions()
                {
                    MaxDepth = Clamp(depth, 1, 60),
                    MaxNodes = Clamp(maxElements, 1, 2000),
                    Filter = filter,
                    IncludeInvisible = includeInvisible,
                    CollapseChrome = collapseChrome,
                    InteractiveOnly = interactiveOnly,
                };

                TreeDumpOutcome outcome = VisualTreeWalker.DumpWithFilter(root, options);

                result.Tree = outcome.Text;
                result.EmittedElements = outcome.EmittedNodes;
                result.SearchedElements = outcome.VisitedNodes;
                result.CollapsedChromeElements = outcome.SkippedChrome;
                result.Matches = outcome.MatchCount;
                result.Truncated = outcome.Truncated;
                result.Hint = outcome.Hint;

                return result;
            });
        }

        [McpServerTool(Name = "ui_find", ReadOnly = true)]
        [Description("Find elements matching a substring and return them as a flat list with an ancestor path for each, rather than as a tree. Use this when you know roughly what you are looking for and want its handle. Use ui_dump_tree instead when you need to understand structure.")]
        public static Task<FindResult> Find(
            [Description("Case-insensitive substring to match against type name, x:Name, displayed text, and DataContext type name.")] string query,
            [Description("What to read. Accepts three forms: a handle from a previous call (e12), an x:Name prefixed with # (#ChatMessageTextBox), or a type name (ChatControl). Name and type forms resolve fresh every call and need no prior discovery, so an agent that knows this app can skip the tree dump entirely. Omit to use the active window, falling back to the main window.")] string element = null,
            [Description("Maximum matches to return. Clamped to 1-200, default 50.")] int maxResults = 50,
            [Description("Include Visibility=Collapsed subtrees. Default false.")] bool includeInvisible = false,
            [Description("Include theme and control template elements in the search. Default false. Leave it off unless you are looking for something inside a template: with it on, a query for a view model type name also matches every template element underneath the row that merely inherits it.")] bool includeChrome = false)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ModelContextProtocol.McpException("The 'query' parameter is required and cannot be empty. Pass a substring to match against element type, x:Name, text, or DataContext type.");
            }

            return DevBridgeGate.RunOnUI("ui_find", () =>
            {
                FindResult result = new FindResult();

                DependencyObject root = ResolveTarget(element, result);
                if (root == null)
                {
                    return result;
                }

                int limit = Clamp(maxResults, 1, 200);
                result.MatchesFound = TreeQueries.Find(root, query.Trim(), limit, includeInvisible, includeChrome, out int visited);
                result.SearchedElements = visited;

                if (result.MatchesFound.Count == 0)
                {
                    result.Status = DevBridgeStatus.NotFound;
                    result.Message = $"Nothing matched '{query}' in the {visited} elements searched. Theme and template elements are excluded unless includeChrome is true, collapsed subtrees unless includeInvisible is true, and unrealized items in a virtualized list have no visual to match at all.";
                }
                else if (result.MatchesFound.Count >= limit)
                {
                    result.Truncated = true;
                    result.Message = $"Stopped at the {limit} result limit. Narrow the query or pass a handle to search a smaller subtree.";
                }

                return result;
            });
        }

        [McpServerTool(Name = "ui_get_text", ReadOnly = true)]
        [Description("Extract the visible text of a window or subtree in document order, as prose. This is the quickest way to read what a dialog says or what state a page is displaying, without the structure a tree dump carries.")]
        public static Task<TextResult> GetText(
            [Description("What to read. Accepts three forms: a handle from a previous call (e12), an x:Name prefixed with # (#ChatMessageTextBox), or a type name (ChatControl). Name and type forms resolve fresh every call and need no prior discovery, so an agent that knows this app can skip the tree dump entirely. Omit to use the active window, falling back to the main window.")] string element = null,
            [Description("Maximum characters to return. Clamped to 100-100000, default 20000.")] int maxChars = 20000,
            [Description("Include Visibility=Collapsed subtrees. Default false.")] bool includeInvisible = false)
        {
            return DevBridgeGate.RunOnUI("ui_get_text", () =>
            {
                TextResult result = new TextResult();

                DependencyObject root = ResolveTarget(element, result);
                if (root == null)
                {
                    return result;
                }

                int limit = Clamp(maxChars, 100, 100000);
                result.Text = TreeQueries.ExtractText(root, includeInvisible, limit);
                result.Truncated = result.Text.Length >= limit;

                if (result.Truncated)
                {
                    result.Message = $"Output reached the {limit} character limit. Pass a handle to read a smaller subtree.";
                }

                return result;
            });
        }

        /// <summary>
        /// Same resolution the read tools use, exposed so capture shares one addressing model rather
        /// than growing a second one that drifts.
        /// </summary>
        internal static DependencyObject ResolveTargetForCapture(string element, DevBridgeResult result)
        {
            return ResolveTarget(element, result);
        }

        /// <summary>
        /// Resolves a reference to any object, including a view model behind a 'd' handle, writing the
        /// failure onto the result and returning null when it cannot. Returning the status on the result
        /// rather than throwing is what lets an agent tell a stale handle from a missing one without
        /// parsing prose.
        /// </summary>
        /// <remarks>
        /// Three addressing forms, and the reason there is more than one is that they trade off against
        /// each other rather than one being better.
        /// <para>
        /// A handle identifies one specific object, including one specific row of a list, and carries the
        /// recycling protection described in <see cref="HandleRegistry"/>. It has to be discovered first
        /// and it dies with the process.
        /// </para>
        /// <para>
        /// An x:Name or a type name is resolved fresh on every call, so it needs no discovery pass at all
        /// and can never go stale. An agent that already knows this app can go straight to
        /// #ChatMessageTextBox without dumping anything. What it cannot express is "the third row", since
        /// rows have no names, which is exactly where handles earn their cost.
        /// </para>
        /// <para>
        /// This is the looser form the property and invoke tools need. The tree and capture tools can only
        /// work on something with a visual, so they layer the DependencyObject requirement on top in
        /// <see cref="ResolveTarget"/>, but a view model is the most useful target there is for reading a
        /// property or running a command and it is not an element at all.
        /// </para>
        /// </remarks>
        internal static object ResolveObjectTarget(string element, DevBridgeResult result)
        {
            if (string.IsNullOrWhiteSpace(element))
            {
                Window defaultWindow = GetDefaultWindow();
                if (defaultWindow == null)
                {
                    result.Status = DevBridgeStatus.NotFound;
                    result.Message = "No window is open to read. The app may still be starting up. Call ui_list_windows.";
                    return null;
                }
                return defaultWindow;
            }

            string target = element.Trim();

            // A handle. Matched by shape so that an x:Name could never be mistaken for one.
            if (IsHandle(target))
            {
                HandleLookup lookup = HandleRegistry.Instance.Resolve(target);
                if (!lookup.IsOk)
                {
                    result.Status = lookup.Status;
                    result.Message = lookup.Message;
                    return null;
                }

                return lookup.Target;
            }

            return ResolveByNameOrType(target, result);
        }

        private static DependencyObject ResolveTarget(string element, DevBridgeResult result)
        {
            object resolved = ResolveObjectTarget(element, result);
            if (resolved == null)
            {
                return null;
            }

            if (!(resolved is DependencyObject dependencyObject))
            {
                result.Status = DevBridgeStatus.NotFound;
                result.Message = $"'{element}' refers to a {resolved.GetType().Name}, which is a DataContext rather than an element, so it has no visual tree. Handles beginning with 'd' are view models: use one with ui_get, ui_set, or ui_invoke instead. For a tree dump or a screenshot, pass an element handle, an x:Name like #ChatMessageTextBox, or a type name.";
                return null;
            }

            return dependencyObject;
        }

        private static DependencyObject ResolveByNameOrType(string target, DevBridgeResult result)
        {
            bool byName = target.StartsWith("#", StringComparison.Ordinal);
            string lookupKey = byName ? target.Substring(1) : target;

            if (string.IsNullOrEmpty(lookupKey))
            {
                result.Status = DevBridgeStatus.NotFound;
                result.Message = "'#' on its own is not an element name. Pass #SomeName, a type name, or a handle.";
                return null;
            }

            // Search the active window first, since that is what the caller is almost certainly asking
            // about, then any others.
            foreach (Window window in WindowsInSearchOrder())
            {
                DependencyObject found = byName
                    ? TreeQueries.FindByName(window, lookupKey)
                    : TreeQueries.FindByTypeName(window, lookupKey);
                if (found != null)
                {
                    return found;
                }
            }

            result.Status = DevBridgeStatus.NotFound;
            result.Message = byName
                ? $"No element named '{lookupKey}' is realized in any open window. Names inside a collapsed tab or an unrealized list row do not exist in the visual tree yet, so navigate there first, or call ui_dump_tree with includeInvisible=true to see what is built but hidden."
                : $"No element of type '{lookupKey}' is realized in any open window. Type matching is on the simple class name, for example ChatControl rather than MixItUp.WPF.Controls.MainControls.ChatControl.";
            return null;
        }

        /// <summary>
        /// Whether a reference is a handle rather than a name. Handles are a letter and digits, which no
        /// x:Name in this codebase looks like, and names are additionally required to be '#'-prefixed.
        /// </summary>
        private static bool IsHandle(string value)
        {
            if (value.Length < 2 || (value[0] != 'e' && value[0] != 'd'))
            {
                return false;
            }

            for (int i = 1; i < value.Length; i++)
            {
                if (!char.IsDigit(value[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static IEnumerable<Window> WindowsInSearchOrder()
        {
            List<Window> ordered = new List<Window>();
            Window active = null;

            foreach (Window window in Application.Current.Windows)
            {
                if (window == null)
                {
                    continue;
                }
                if (window.IsActive && active == null)
                {
                    active = window;
                }
                else
                {
                    ordered.Add(window);
                }
            }

            if (active != null)
            {
                ordered.Insert(0, active);
            }

            return ordered;
        }

        /// <summary>
        /// The window a read should default to. The active window is the right default because it is
        /// what the user is looking at, and when a modal dialog is up it is the dialog, which is
        /// invariably the thing being asked about.
        /// </summary>
        private static Window GetDefaultWindow()
        {
            Window firstVisible = null;

            foreach (Window window in Application.Current.Windows)
            {
                if (window == null)
                {
                    continue;
                }
                if (window.IsActive)
                {
                    return window;
                }
                if (firstVisible == null && window.IsVisible)
                {
                    firstVisible = window;
                }
            }

            return Application.Current.MainWindow ?? firstVisible;
        }

        private static bool IsModal(Window window)
        {
            // WPF does not expose modality. ComponentDispatcher.IsThreadModal is per-thread rather than
            // per-window, so it can only say that some window is modal, which combined with the window
            // being active is a reliable enough signal to be worth reporting.
            try
            {
                return window.IsActive && System.Windows.Interop.ComponentDispatcher.IsThreadModal;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }

    public class WindowInfo
    {
        [Description("Handle for this window. Use it as the root for ui_dump_tree, ui_find, or ui_get_text.")]
        public string Handle { get; set; }

        [Description("The window's CLR type name, for example MainWindow or CommandEditorWindow.")]
        public string Type { get; set; }

        [Description("The window title as displayed. The main window title carries the profile name and version.")]
        public string Title { get; set; }

        [Description("Whether this window currently has focus.")]
        public bool IsActive { get; set; }

        [Description("Whether the window is currently visible.")]
        public bool IsVisible { get; set; }

        [Description("Best-effort indication that this window is running a modal message pump. A modal window is the usual reason the rest of the app stops responding.")]
        public bool IsModal { get; set; }

        [Description("Whether this is the application's main window.")]
        public bool IsMainWindow { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        [Description("Type name of the window's DataContext, which is its view model.")]
        public string DataContextType { get; set; }

        [Description("Handle for the window's DataContext, for property work.")]
        public string DataContextHandle { get; set; }
    }

    public class WindowListResult : DevBridgeResult
    {
        [Description("Every open window, in the order WPF reports them.")]
        public List<WindowInfo> Windows { get; set; } = new List<WindowInfo>();
    }

    public class TreeResult : DevBridgeResult
    {
        [Description("The tree, one line per element: handle, then indentation, then type, #x:Name, \"text\", state flags, size, and dc=Type(handle). A '*' after the handle marks a filter match. 'BROKEN-BINDING=Prop' marks a binding whose path did not resolve, which renders as empty rather than throwing and is easy to miss otherwise. '[row]' marks an items control container, whose handle is pinned to its current item.")]
        public string Tree { get; set; }

        [Description("How many elements were emitted.")]
        public int EmittedElements { get; set; }

        [Description("How many elements were walked. Larger than the emitted count when a filter is applied or chrome was collapsed.")]
        public int SearchedElements { get; set; }

        [Description("How many theme and template elements were collapsed away. When this dwarfs the emitted count, that is the collapsing doing its job.")]
        public int CollapsedChromeElements { get; set; }

        [Description("How many elements matched the filter, when one was given.")]
        public int Matches { get; set; }

        [Description("Whether output hit the element cap. When true, Hint says how to narrow the request.")]
        public bool Truncated { get; set; }

        [Description("What to do next, when the result needs narrowing or nothing matched.")]
        public string Hint { get; set; }
    }

    public class FindResult : DevBridgeResult
    {
        [Description("Matching elements, in visual tree order.")]
        public List<FoundElement> MatchesFound { get; set; } = new List<FoundElement>();

        [Description("How many elements were searched.")]
        public int SearchedElements { get; set; }

        [Description("Whether the result limit was reached.")]
        public bool Truncated { get; set; }
    }

    public class TextResult : DevBridgeResult
    {
        [Description("Visible text in document order, one entry per line, deduplicated. Controls that look like they hold credentials report [REDACTED].")]
        public string Text { get; set; }

        [Description("Whether the character limit was reached.")]
        public bool Truncated { get; set; }
    }
}
