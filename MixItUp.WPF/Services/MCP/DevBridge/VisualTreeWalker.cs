#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    internal sealed class TreeDumpOptions
    {
        public int MaxDepth { get; set; } = 25;

        public int MaxNodes { get; set; } = 300;

        public string Filter { get; set; }

        /// <summary>
        /// Collapsed subtrees are skipped by default. This app is a tab-based shell where most of the
        /// tree is built but not shown, so including it multiplies the output for no benefit most of the
        /// time. Turn it on when the question is why something is not visible.
        /// </summary>
        public bool IncludeInvisible { get; set; }

        /// <summary>
        /// Skip theme and control template plumbing, promoting its children. See
        /// <see cref="ElementInterest"/> for why this defaults on.
        /// </summary>
        public bool CollapseChrome { get; set; } = true;

        /// <summary>
        /// Emit only what can be acted on, plus the view model boundaries that say where each control
        /// lives. This is the cheapest useful read of a screen.
        /// </summary>
        public bool InteractiveOnly { get; set; }
    }

    internal sealed class TreeDumpOutcome
    {
        public string Text { get; set; }

        public int EmittedNodes { get; set; }

        public int VisitedNodes { get; set; }

        public int SkippedChrome { get; set; }

        public int MatchCount { get; set; }

        public bool Truncated { get; set; }

        public string Hint { get; set; }
    }

    /// <summary>
    /// Walks the live visual tree and renders it as one indented line per element.
    /// </summary>
    /// <remarks>
    /// Deliberately not nested JSON. An agent has to be able to scan a whole window in one read and
    /// grep it, and the brace overhead of a nested structure costs several times the payload for
    /// information the indentation already carries. Handles lead each line for the same reason.
    /// <para>
    /// Must be called on the UI thread. <see cref="DevBridgeGate"/> is what guarantees that.
    /// </para>
    /// </remarks>
    internal static class VisualTreeWalker
    {
        /// <summary>
        /// Bounds the work regardless of how much of the tree the caller asked to see. Emission is
        /// capped separately and much lower: a filtered dump may need to search a large tree to emit a
        /// handful of lines, so the two limits are not the same number.
        /// </summary>
        private const int MaxVisitedNodes = 20000;

        private sealed class Node
        {
            public DependencyObject Visual;

            public List<Node> Children;

            public object DataContext;

            /// <summary>This element introduces a DataContext its parent did not have.</summary>
            public bool IntroducesDataContext;

            /// <summary>
            /// This element sits in or under an items control container, so its handle is pinned to the
            /// DataContext it currently carries. See <see cref="HandleRegistry"/>.
            /// </summary>
            public bool PinToDataContext;

            public bool IsItemContainer;

            public bool IsInteresting;

            public bool IsActionable;

            public bool Matches;

            /// <summary>Kept only to preserve the path down to a match.</summary>
            public bool OnPathToMatch;

            // Cached because each is a non-trivial call and would otherwise be made once for the filter
            // check and again when rendering the line.
            public string Name;

            public string Text;

            /// <summary>
            /// A label pulled from a descendant, for an actionable element that carries no text itself.
            /// Only populated in interactive-only mode, where the descendant holding the label is
            /// filtered out and the control would otherwise be listed unnamed.
            /// </summary>
            public string DerivedLabel;

            public string BrokenBindings;
        }

        public static TreeDumpOutcome Dump(DependencyObject root, TreeDumpOptions options)
        {
            TreeDumpOutcome outcome = new TreeDumpOutcome();

            int visited = 0;
            Node rootNode = Build(root, 0, null, null, false, options, ref visited);
            outcome.VisitedNodes = visited;

            if (rootNode == null)
            {
                outcome.Text = string.Empty;
                outcome.Hint = "The root element is not part of the visual tree, so it has nothing to dump.";
                return outcome;
            }

            // The root always gets a line, whatever it is, so the output is never headless.
            rootNode.IsInteresting = true;

            if (options.InteractiveOnly)
            {
                DeriveLabels(rootNode);
            }

            bool filtering = !string.IsNullOrWhiteSpace(options.Filter);
            if (filtering)
            {
                outcome.MatchCount = MarkPathsToMatches(rootNode);
            }

            StringBuilder builder = new StringBuilder();
            Render(rootNode, 0, builder, options, filtering, outcome);

            outcome.Text = builder.ToString();

            if (outcome.Truncated)
            {
                outcome.Hint = $"Output was capped at {options.MaxNodes} elements. To narrow it: pass handle=<a handle from this dump> to dump only that subtree, lower depth (currently {options.MaxDepth}), or pass filter=<substring> to show only matching elements and the path down to them.";
            }
            else if (filtering && outcome.MatchCount == 0)
            {
                outcome.Hint = $"Nothing matched '{options.Filter}' in the {visited} elements searched. The filter matches against type name, x:Name, displayed text, and DataContext type name, case-insensitively. Unrealized rows in a virtualized list have no visual and cannot match.";
            }
            else if (visited >= MaxVisitedNodes)
            {
                outcome.Hint = $"Stopped after visiting the {MaxVisitedNodes} element ceiling, so part of the tree was not searched. Dump a subtree instead by passing handle=<a handle from this dump>.";
            }
            else if (options.CollapseChrome && outcome.SkippedChrome > 0)
            {
                outcome.Hint = $"{outcome.SkippedChrome} theme and template elements were collapsed away, and indentation reflects meaningful nesting rather than true visual depth. Pass collapseChrome=false for the literal tree, which is what to do when reasoning about layout or hit testing.";
            }

            return outcome;
        }

        private static Node Build(DependencyObject visual, int depth, object inheritedDataContext, string inheritedText, bool insideItemContainer, TreeDumpOptions options, ref int visited)
        {
            if (visual == null || visited >= MaxVisitedNodes)
            {
                return null;
            }

            // VisualTreeHelper only accepts Visual and Visual3D.
            if (!(visual is Visual) && !(visual is System.Windows.Media.Media3D.Visual3D))
            {
                return null;
            }

            visited++;

            object dataContext = HandleRegistry.CurrentDataContext(visual);
            bool isItemContainer = IsItemContainer(visual);
            bool pinScope = insideItemContainer || isItemContainer;
            bool introducesDataContext = dataContext != null
                && !ReferenceEquals(dataContext, inheritedDataContext)
                && ElementInterest.IsMeaningfulDataContext(dataContext);

            Node node = new Node()
            {
                Visual = visual,
                DataContext = dataContext,
                IntroducesDataContext = introducesDataContext,
                IsItemContainer = isItemContainer,
                // Pinning only inside item containers is the point of 3.1: those are the elements whose
                // DataContext is swapped underneath them by recycling. Pinning ordinary chrome as well
                // would invalidate handles on every unrelated navigation for no gain.
                PinToDataContext = pinScope && dataContext != null,
                Name = ElementDescriber.GetName(visual),
                Text = ElementDescriber.GetText(visual),
                BrokenBindings = ElementDescriber.GetBrokenBindings(visual),
            };

            bool textIsNew = !string.IsNullOrEmpty(node.Text) && !string.Equals(node.Text, inheritedText, StringComparison.Ordinal);

            node.IsActionable = ElementInterest.IsActionable(visual, isItemContainer);

            node.IsInteresting = !options.CollapseChrome
                || ElementInterest.IsInteresting(visual, introducesDataContext, isItemContainer, node.BrokenBindings != null, textIsNew);

            // Only a kept element resets what counts as already-said text, so a chrome element echoing
            // its parent's label does not let the label through again further down.
            string textForChildren = node.IsInteresting && !string.IsNullOrEmpty(node.Text) ? node.Text : inheritedText;

            // Compare against the last meaningful DataContext rather than the immediate parent's.
            // A ContentPresenter takes its Content as its DataContext, so a template that puts a string
            // or a geometry there would otherwise reset the baseline and make the next real element
            // underneath look like it introduced a view model that was already in scope.
            object dataContextForChildren = ElementInterest.IsMeaningfulDataContext(dataContext) ? dataContext : inheritedDataContext;

            if (depth >= options.MaxDepth)
            {
                return node;
            }

            int childCount;
            try
            {
                childCount = VisualTreeHelper.GetChildrenCount(visual);
            }
            catch (Exception)
            {
                return node;
            }

            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child;
                try
                {
                    child = VisualTreeHelper.GetChild(visual, i);
                }
                catch (Exception)
                {
                    continue;
                }

                if (!options.IncludeInvisible && child is UIElement childElement && childElement.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                Node childNode = Build(child, depth + 1, dataContextForChildren, textForChildren, pinScope, options, ref visited);
                if (childNode != null)
                {
                    node.Children = node.Children ?? new List<Node>();
                    node.Children.Add(childNode);
                }

                if (visited >= MaxVisitedNodes)
                {
                    break;
                }
            }

            return node;
        }

        /// <summary>
        /// Gives every actionable element a name, taking it from a descendant when it has none of its
        /// own. This is the same computation an accessibility tool performs to announce a control: a list
        /// row bound to a view model has no text, but the TextBlock inside it reads "Commands", and that
        /// is the only thing that makes the row addressable by a caller deciding where to click.
        /// </summary>
        private static void DeriveLabels(Node node)
        {
            if (node.IsActionable && string.IsNullOrEmpty(node.Text))
            {
                node.DerivedLabel = FirstDescendantText(node, 0);
            }

            if (node.Children != null)
            {
                foreach (Node child in node.Children)
                {
                    DeriveLabels(child);
                }
            }
        }

        private static string FirstDescendantText(Node node, int depth)
        {
            if (node.Children == null || depth > 12)
            {
                return null;
            }

            foreach (Node child in node.Children)
            {
                if (!string.IsNullOrEmpty(child.Text))
                {
                    return child.Text;
                }
            }

            foreach (Node child in node.Children)
            {
                string found = FirstDescendantText(child, depth + 1);
                if (!string.IsNullOrEmpty(found))
                {
                    return found;
                }
            }

            return null;
        }

        private static bool IsItemContainer(DependencyObject visual)
        {
            try
            {
                return ItemsControl.ItemsControlFromItemContainer(visual) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Flags matching nodes and every ancestor of one, so a filtered dump still shows where its hits
        /// sit rather than a flat list stripped of context.
        /// </summary>
        private static int MarkPathsToMatches(Node node)
        {
            int matches = 0;

            node.Matches = Matches(node);
            if (node.Matches)
            {
                matches++;
            }

            bool descendantMatched = false;
            if (node.Children != null)
            {
                foreach (Node child in node.Children)
                {
                    int childMatches = MarkPathsToMatches(child);
                    matches += childMatches;
                    if (childMatches > 0 || child.OnPathToMatch)
                    {
                        descendantMatched = true;
                    }
                }
            }

            node.OnPathToMatch = node.Matches || descendantMatched;
            return matches;
        }

        private static bool Matches(Node node)
        {
            string filter = CurrentFilter;
            if (string.IsNullOrEmpty(filter))
            {
                return false;
            }

            if (Contains(node.Visual.GetType().Name, filter)) { return true; }
            if (Contains(node.Name, filter)) { return true; }
            if (Contains(node.Text, filter)) { return true; }
            if (node.DataContext != null && Contains(node.DataContext.GetType().Name, filter)) { return true; }

            return false;
        }

        private static bool Contains(string value, string filter)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Set for the duration of a dump so the recursive match check does not have to thread the
        /// filter through every frame. Safe because dumps are serialized onto the UI thread by
        /// <see cref="DevBridgeGate"/>.
        /// </summary>
        [ThreadStatic]
        private static string CurrentFilter;

        /// <summary>
        /// Emits a node and recurses. <paramref name="renderDepth"/> counts only emitted ancestors, so
        /// skipping a chrome element promotes its children to its own indentation level rather than
        /// leaving a gap where it was.
        /// </summary>
        private static void Render(Node node, int renderDepth, StringBuilder builder, TreeDumpOptions options, bool filtering, TreeDumpOutcome outcome)
        {
            if (outcome.Truncated)
            {
                return;
            }

            // An explicit filter match is always shown, even if the element would otherwise be
            // collapsed as chrome. The agent asked for it by name.
            bool interesting = node.IsInteresting || (filtering && node.Matches);
            if (options.InteractiveOnly)
            {
                // Actionable controls, plus the view model boundaries that give them an address.
                interesting = node.IsActionable || node.IntroducesDataContext;
            }
            bool wantedByFilter = !filtering || node.OnPathToMatch;
            bool emit = interesting && wantedByFilter;

            if (emit)
            {
                if (outcome.EmittedNodes >= options.MaxNodes)
                {
                    outcome.Truncated = true;
                    return;
                }

                builder.AppendLine(RenderLine(node, renderDepth, filtering));
                outcome.EmittedNodes++;
            }
            else if (!interesting)
            {
                outcome.SkippedChrome++;
            }

            if (node.Children != null)
            {
                int childDepth = emit ? renderDepth + 1 : renderDepth;
                foreach (Node child in node.Children)
                {
                    Render(child, childDepth, builder, options, filtering, outcome);
                    if (outcome.Truncated)
                    {
                        return;
                    }
                }
            }
        }

        private static string RenderLine(Node node, int renderDepth, bool filtering)
        {
            string handle = HandleRegistry.Instance.GetOrCreate(
                node.Visual,
                "e",
                node.PinToDataContext ? node.DataContext : null);

            StringBuilder line = new StringBuilder();

            // Handle first and left-aligned so the output greps cleanly and columns line up.
            line.Append(handle.PadRight(7));

            if (filtering)
            {
                line.Append(node.Matches ? "* " : "  ");
            }

            line.Append(new string(' ', 2 * renderDepth));

            line.Append(node.Visual.GetType().Name);

            if (!string.IsNullOrEmpty(node.Name))
            {
                line.Append(" #").Append(node.Name);
            }

            string label = !string.IsNullOrEmpty(node.Text) ? node.Text : node.DerivedLabel;
            if (!string.IsNullOrEmpty(label))
            {
                line.Append(" \"").Append(label).Append('"');
            }

            ElementDescriber.AppendStateFlags(line, node.Visual);
            ElementDescriber.AppendItemsInfo(line, node.Visual);

            if (node.BrokenBindings != null)
            {
                line.Append(" BROKEN-BINDING=").Append(node.BrokenBindings);
            }

            if (node.Visual is FrameworkElement frameworkElement)
            {
                if (frameworkElement.ActualWidth > 0 || frameworkElement.ActualHeight > 0)
                {
                    line.Append(' ')
                        .Append(frameworkElement.ActualWidth.ToString("0"))
                        .Append('x')
                        .Append(frameworkElement.ActualHeight.ToString("0"));
                }
                else
                {
                    // Zero-sized but not collapsed is worth calling out: it is a common reason an
                    // element is present and invisible, and it is invisible to a screenshot too.
                    line.Append(" 0x0");
                }
            }

            // A row always names its item even when that item was already in scope, because the bound
            // item is the entire reason the row is interesting. Everything else only reports a
            // DataContext at the point it enters scope, to keep the column from repeating down a branch.
            bool showDataContext = node.IntroducesDataContext
                || (node.IsItemContainer && ElementInterest.IsMeaningfulDataContext(node.DataContext));

            if (showDataContext)
            {
                // The DataContext gets a handle of its own. It is the one that survives an items
                // control recycling its containers, so it is what to hold for property work.
                string dataHandle = HandleRegistry.Instance.GetOrCreate(node.DataContext, "d", null);
                line.Append(" dc=").Append(node.DataContext.GetType().Name).Append('(').Append(dataHandle).Append(')');
            }

            if (node.IsItemContainer)
            {
                line.Append(" [row]");
            }

            return line.ToString();
        }

        /// <summary>
        /// Renders a dump, setting up and tearing down the ambient filter.
        /// </summary>
        public static TreeDumpOutcome DumpWithFilter(DependencyObject root, TreeDumpOptions options)
        {
            CurrentFilter = options.Filter;
            try
            {
                return Dump(root, options);
            }
            finally
            {
                CurrentFilter = null;
            }
        }
    }
}
