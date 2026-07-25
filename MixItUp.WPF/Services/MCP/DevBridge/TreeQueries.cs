#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    public class FoundElement
    {
        [Description("Handle for this element. Pass it to ui_dump_tree as the root, or hold it for property work.")]
        public string Handle { get; set; }

        [Description("The element's CLR type name, for example Button or ListBoxItem.")]
        public string Type { get; set; }

        [Description("The x:Name of the element, or null if it has none.")]
        public string Name { get; set; }

        [Description("The element's displayed text, truncated. '[REDACTED]' when the control looks like it holds a credential.")]
        public string Text { get; set; }

        [Description("Type name of the element's DataContext, which is usually the view model behind it.")]
        public string DataContextType { get; set; }

        [Description("Handle for the DataContext, when the element introduces one. This handle survives an items control recycling its containers, so prefer it over the element handle when holding a reference to a row.")]
        public string DataContextHandle { get; set; }

        [Description("Ancestor path from the window down to this element, for orientation.")]
        public string Path { get; set; }

        [Description("Whether the element is currently enabled.")]
        public bool IsEnabled { get; set; }

        [Description("Whether the element is currently visible.")]
        public bool IsVisible { get; set; }
    }

    /// <summary>
    /// Flat queries over the visual tree, as opposed to the structural dump in
    /// <see cref="VisualTreeWalker"/>. Must be called on the UI thread.
    /// </summary>
    internal static class TreeQueries
    {
        private const int MaxVisitedNodes = 20000;

        public static List<FoundElement> Find(DependencyObject root, string query, int maxResults, bool includeInvisible, bool includeChrome, out int visited)
        {
            List<FoundElement> results = new List<FoundElement>();
            visited = 0;
            Search(root, query, maxResults, includeInvisible, includeChrome, null, null, results, ref visited);
            return results;
        }

        private static void Search(DependencyObject visual, string query, int maxResults, bool includeInvisible, bool includeChrome, object inheritedDataContext, string inheritedText, List<FoundElement> results, ref int visited)
        {
            if (visual == null || results.Count >= maxResults || visited >= MaxVisitedNodes)
            {
                return;
            }

            if (!(visual is Visual) && !(visual is System.Windows.Media.Media3D.Visual3D))
            {
                return;
            }

            visited++;

            object dataContext = HandleRegistry.CurrentDataContext(visual);
            bool meaningfulDataContext = ElementInterest.IsMeaningfulDataContext(dataContext);
            bool isItemContainer = IsItemContainer(visual);

            // A DataContext is only this element's own where it enters scope, or where the element is the
            // row bound to it. Everything underneath merely inherits it, so counting those as matches
            // turned one bound row into twenty hits on its template internals.
            bool ownsDataContext = meaningfulDataContext
                && (!ReferenceEquals(dataContext, inheritedDataContext) || isItemContainer);

            string text = ElementDescriber.GetText(visual);
            string name = ElementDescriber.GetName(visual);

            // Same rule as the tree dump: a Button, its Ripple, and its TextBlock all report the same
            // label, so only the outermost one is a real hit for a text query.
            bool textIsNew = !string.IsNullOrEmpty(text) && !string.Equals(text, inheritedText, StringComparison.Ordinal);

            bool interesting = includeChrome
                || ElementInterest.IsInteresting(visual, ownsDataContext && !isItemContainer, isItemContainer, ElementDescriber.GetBrokenBindings(visual) != null, textIsNew);

            if (interesting && IsMatch(visual, name, text, ownsDataContext ? dataContext : null, query))
            {
                results.Add(Describe(visual));
                if (results.Count >= maxResults)
                {
                    return;
                }
            }

            object dataContextForChildren = meaningfulDataContext ? dataContext : inheritedDataContext;
            string textForChildren = interesting && !string.IsNullOrEmpty(text) ? text : inheritedText;

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
                DependencyObject child;
                try
                {
                    child = VisualTreeHelper.GetChild(visual, i);
                }
                catch (Exception)
                {
                    continue;
                }

                if (!includeInvisible && child is UIElement childElement && childElement.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                Search(child, query, maxResults, includeInvisible, includeChrome, dataContextForChildren, textForChildren, results, ref visited);
                if (results.Count >= maxResults || visited >= MaxVisitedNodes)
                {
                    return;
                }
            }
        }

        private static bool IsMatch(DependencyObject visual, string name, string text, object ownedDataContext, string query)
        {
            if (Contains(visual.GetType().Name, query)) { return true; }
            if (Contains(name, query)) { return true; }
            if (Contains(text, query)) { return true; }
            if (ownedDataContext != null && Contains(ownedDataContext.GetType().Name, query)) { return true; }

            return false;
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static FoundElement Describe(DependencyObject visual)
        {
            object dataContext = HandleRegistry.CurrentDataContext(visual);
            bool insideRow = IsInsideItemContainer(visual);

            // A string DataContext is template content rather than a view model, so it gets named but
            // not handed a handle. There is nothing on it to reach.
            bool reachableDataContext = ElementInterest.IsMeaningfulDataContext(dataContext);

            FoundElement found = new FoundElement()
            {
                Handle = HandleRegistry.Instance.GetOrCreate(visual, "e", insideRow ? dataContext : null),
                Type = visual.GetType().Name,
                Name = ElementDescriber.GetName(visual),
                Text = ElementDescriber.GetText(visual),
                DataContextType = dataContext?.GetType().Name,
                DataContextHandle = reachableDataContext ? HandleRegistry.Instance.GetOrCreate(dataContext, "d", null) : null,
                Path = BuildPath(visual),
                IsEnabled = !(visual is UIElement element) || element.IsEnabled,
                IsVisible = !(visual is UIElement visible) || visible.Visibility == Visibility.Visible,
            };

            return found;
        }

        /// <summary>
        /// Finds the first element with the given x:Name, depth-first.
        /// </summary>
        /// <remarks>
        /// FrameworkElement.FindName is not used because it only searches the namescope the name was
        /// registered in, and this app's names are spread across control and template namescopes, so it
        /// misses most of them. Comparing Name while walking finds them all.
        /// </remarks>
        public static DependencyObject FindByName(DependencyObject root, string name)
        {
            return FirstWhere(root, element => string.Equals(ElementDescriber.GetName(element), name, StringComparison.Ordinal), 0);
        }

        /// <summary>
        /// Finds the first element of the given type name, depth-first.
        /// </summary>
        public static DependencyObject FindByTypeName(DependencyObject root, string typeName)
        {
            return FirstWhere(root, element => string.Equals(element.GetType().Name, typeName, StringComparison.OrdinalIgnoreCase), 0);
        }

        private static DependencyObject FirstWhere(DependencyObject visual, Func<DependencyObject, bool> predicate, int depth)
        {
            if (visual == null || depth > 200)
            {
                return null;
            }

            if (!(visual is Visual) && !(visual is System.Windows.Media.Media3D.Visual3D))
            {
                return null;
            }

            if (predicate(visual))
            {
                return visual;
            }

            int childCount;
            try
            {
                childCount = VisualTreeHelper.GetChildrenCount(visual);
            }
            catch (Exception)
            {
                return null;
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

                DependencyObject found = FirstWhere(child, predicate, depth + 1);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>Whether this element is itself a container generated by an items control.</summary>
        private static bool IsItemContainer(DependencyObject visual)
        {
            try
            {
                return System.Windows.Controls.ItemsControl.ItemsControlFromItemContainer(visual) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsInsideItemContainer(DependencyObject visual)
        {
            DependencyObject current = visual;
            int guard = 0;
            while (current != null && guard++ < 200)
            {
                try
                {
                    if (System.Windows.Controls.ItemsControl.ItemsControlFromItemContainer(current) != null)
                    {
                        return true;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }

        private static string BuildPath(DependencyObject visual)
        {
            List<string> segments = new List<string>();
            DependencyObject current = visual;
            int guard = 0;

            while (current != null && guard++ < 200)
            {
                StringBuilder segment = new StringBuilder(current.GetType().Name);
                string name = ElementDescriber.GetName(current);
                if (!string.IsNullOrEmpty(name))
                {
                    segment.Append('#').Append(name);
                }
                segments.Add(segment.ToString());

                try
                {
                    current = VisualTreeHelper.GetParent(current);
                }
                catch (Exception)
                {
                    break;
                }
            }

            segments.Reverse();

            // The interesting end is the leaf. A deep path through layout panels is mostly noise, so
            // keep the window at the front and the last few levels, and elide the middle.
            if (segments.Count > 8)
            {
                List<string> trimmed = new List<string>();
                trimmed.Add(segments[0]);
                trimmed.Add("…");
                trimmed.AddRange(segments.GetRange(segments.Count - 6, 6));
                segments = trimmed;
            }

            return string.Join(" > ", segments);
        }

        /// <summary>
        /// Concatenates the visible text of a subtree in document order, which is the closest equivalent
        /// to reading a rendered page as prose.
        /// </summary>
        public static string ExtractText(DependencyObject root, bool includeInvisible, int maxChars)
        {
            StringBuilder builder = new StringBuilder();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            int visited = 0;
            Collect(root, includeInvisible, builder, seen, maxChars, ref visited);
            return builder.ToString();
        }

        private static void Collect(DependencyObject visual, bool includeInvisible, StringBuilder builder, HashSet<string> seen, int maxChars, ref int visited)
        {
            if (visual == null || builder.Length >= maxChars || visited >= MaxVisitedNodes)
            {
                return;
            }

            if (!(visual is Visual) && !(visual is System.Windows.Media.Media3D.Visual3D))
            {
                return;
            }

            visited++;

            string text = ElementDescriber.GetText(visual);

            // A Window contributes its title, and a ContentControl its content, both of which are
            // usually repeated by a TextBlock underneath. Deduplicating keeps the output readable
            // without having to special-case every container type.
            if (!string.IsNullOrWhiteSpace(text) && seen.Add(text))
            {
                builder.AppendLine(text);
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
                DependencyObject child;
                try
                {
                    child = VisualTreeHelper.GetChild(visual, i);
                }
                catch (Exception)
                {
                    continue;
                }

                if (!includeInvisible && child is UIElement childElement && childElement.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                Collect(child, includeInvisible, builder, seen, maxChars, ref visited);
                if (builder.Length >= maxChars)
                {
                    return;
                }
            }
        }
    }
}
