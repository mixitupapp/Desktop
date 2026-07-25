#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Shapes;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    /// <summary>
    /// Decides whether an element earns a line in a tree dump.
    /// </summary>
    /// <remarks>
    /// This exists because the raw visual tree of this app is mostly not about this app. It is built on
    /// MaterialDesignInXAML, and a single IconButton expands into roughly eleven elements of template
    /// plumbing. A first dump of the main window reached 120 elements without getting past the chat
    /// list, and almost none of those lines were something an agent could act on.
    /// <para>
    /// Chrome is skipped rather than pruned: children of a skipped element are promoted to its parent's
    /// level, so the structure an agent needs is preserved while the plumbing between it disappears.
    /// </para>
    /// <para>
    /// The ordering of the checks below is the design. Evidence that an element matters is tested before
    /// evidence that it is decoration, so a Border that carries a broken binding still gets a line while
    /// its eleven siblings do not.
    /// </para>
    /// </remarks>
    internal static class ElementInterest
    {
        /// <summary>
        /// Names belonging to a control template's contract rather than to this application. A template
        /// part is named so the template can find it, which is not evidence that it means anything here.
        /// </summary>
        private static readonly string[] TemplateNamePrefixes = new string[] { "PART_", "Template", "Adorner" };

        private static readonly string[] TemplateNames = new string[]
        {
            // WPF and MaterialDesignInXAML template internals, gathered from real dumps of this app.
            "border", "Border", "Bd", "Bg", "canvas", "Grid", "OuterGrid", "RootGrid", "InnerRoot",
            "ContentGrid", "OuterBorder", "contentPresenter", "ContentPresenter", "DialogHostRoot",
            "MouseOverBorder", "SelectedBorder", "SelectedUnfocusedBorder", "Ripple", "toggleButton",
            "ToggleOuterBorder", "splitBorder", "arrow", "Underline", "UnderlineBorder", "Hint",
            "HintBackgroundBorder", "HintBackgroundGrid", "HintWrapper", "SimpleHintTextBlock",
            "FloatingHintTextBlock", "HelperTextWrapper", "HelperTextTextBlock", "FooterGrid",
            "ScaleHost", "GeometryEllipse", "ClickEllipse", "Animation", "rectangle", "rectangle1",
            "rectangle2", "ArrowTop", "ArrowBottom",
        };

        /// <summary>
        /// Types that exist to lay out or decorate other things. Matched by name so that theme library
        /// types are covered without referencing the theme assembly.
        /// </summary>
        private static readonly string[] DecorativeTypeNames = new string[]
        {
            "Ripple", "Underline", "ScaleHost", "SmartHint", "ContainerVisual", "AdornerDecorator",
            "AdornerLayer", "ItemsPresenter", "ScrollContentPresenter", "Viewbox", "Decorator",
            // A scrollbar and its parts are chrome here. What an agent needs to know about a long list
            // is how many items it holds and how many are realized, which the items control reports
            // directly, not that the scrollbar has two repeat buttons and a thumb.
            "ScrollBar", "RepeatButton", "Thumb",
        };

        /// <summary>
        /// True when this element should get its own line.
        /// </summary>
        /// <param name="textIsNew">
        /// The element's text differs from the nearest kept ancestor's. A Button, its Ripple, its
        /// ContentPresenter, and its TextBlock all report the same string, so without this the same label
        /// is repeated on four consecutive lines.
        /// </param>
        public static bool IsInteresting(DependencyObject element, bool introducesDataContext, bool isItemContainer, bool hasBrokenBinding, bool textIsNew)
        {
            // Evidence it matters, checked first so decoration rules cannot suppress it.

            // A broken binding is the whole reason to look, wherever it happens to sit.
            if (hasBrokenBinding)
            {
                return true;
            }

            // A view model boundary is the single most useful thing in a dump, because it is what UI
            // Automation structurally cannot see and what makes the rest reachable.
            if (introducesDataContext)
            {
                return true;
            }

            if (isItemContainer)
            {
                return true;
            }

            if (textIsNew)
            {
                return true;
            }

            // Evidence it is decoration.

            // A shape is a drawing primitive. It is never the thing an agent is looking for unless one
            // of the checks above already claimed it.
            if (element is Shape)
            {
                return false;
            }

            string name = ElementDescriber.GetName(element);
            if (!string.IsNullOrEmpty(name) && IsTemplateName(name))
            {
                return false;
            }

            if (IsDecorativeType(element))
            {
                return false;
            }

            // Anything the agent could actually drive.
            if (element is ButtonBase
                || element is TextBoxBase
                || element is PasswordBox
                || element is Selector
                || element is MenuItem
                || element is TreeViewItem
                || element is RangeBase
                || element is Hyperlink
                || element is Window
                || element is ItemsControl)
            {
                return true;
            }

            // Controls defined by this application, as opposed to the framework or the theme library.
            // These are the app's own structure and are always worth a line.
            if (IsApplicationType(element))
            {
                return true;
            }

            // A name this app chose, on something not otherwise accounted for.
            return !string.IsNullOrEmpty(name) && !IsLayoutPanel(element);
        }

        /// <summary>
        /// Whether an agent could act on this element: click it, type into it, or pick from it.
        /// </summary>
        /// <remarks>
        /// This is a much narrower question than <see cref="IsInteresting"/>. It answers "what is there to
        /// do on this screen", which is the cheapest useful read of a window and is usually all that is
        /// needed to drive the app to somewhere a change can be checked.
        /// </remarks>
        public static bool IsActionable(DependencyObject element, bool isItemContainer)
        {
            // A row is actionable because selecting it is how navigation works throughout this app.
            if (isItemContainer)
            {
                return true;
            }

            // Template parts are not actionable in their own right. The control that owns them is.
            string name = ElementDescriber.GetName(element);
            if (!string.IsNullOrEmpty(name) && IsTemplateName(name))
            {
                return false;
            }

            if (IsDecorativeType(element))
            {
                return false;
            }

            return element is ButtonBase
                || element is TextBoxBase
                || element is PasswordBox
                || element is Selector
                || element is MenuItem
                || element is TreeViewItem
                || element is RangeBase
                || element is Hyperlink;
        }

        private static bool IsApplicationType(DependencyObject element)
        {
            string ns = element.GetType().Namespace;
            return ns != null && ns.StartsWith("MixItUp", StringComparison.Ordinal);
        }

        private static bool IsLayoutPanel(DependencyObject element)
        {
            return element is Panel || element is Border || element is ContentPresenter;
        }

        private static bool IsDecorativeType(DependencyObject element)
        {
            if (IsLayoutPanel(element) || element is ScrollViewer)
            {
                return true;
            }

            string typeName = element.GetType().Name;
            foreach (string decorative in DecorativeTypeNames)
            {
                if (string.Equals(typeName, decorative, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTemplateName(string name)
        {
            foreach (string exact in TemplateNames)
            {
                if (string.Equals(name, exact, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            foreach (string prefix in TemplateNamePrefixes)
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a DataContext is worth naming and handing out a handle for.
        /// </summary>
        /// <remarks>
        /// Setting a ContentPresenter's DataContext to a string is how WPF passes content down a template,
        /// so dumps were reporting dc=String on hundreds of lines and minting a handle for each. A string
        /// is content, not a view model, and there is nothing on it to reach.
        /// </remarks>
        public static bool IsMeaningfulDataContext(object dataContext)
        {
            if (dataContext == null)
            {
                return false;
            }

            Type type = dataContext.GetType();
            return !type.IsPrimitive
                && !type.IsEnum
                && type != typeof(string)
                && type != typeof(decimal)
                && type != typeof(DateTime)
                && type != typeof(DateTimeOffset)
                && type != typeof(TimeSpan)
                && type != typeof(Guid)
                && type != typeof(Uri);
        }
    }
}
