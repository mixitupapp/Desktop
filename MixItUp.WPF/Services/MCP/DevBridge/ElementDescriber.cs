#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    /// <summary>
    /// Turns a single element into the compact tokens that make up one line of a tree dump.
    /// </summary>
    internal static class ElementDescriber
    {
        /// <summary>
        /// How much of an element's text to keep. Long enough to identify a control, short enough that a
        /// wide dump stays readable.
        /// </summary>
        private const int MaxTextLength = 70;

        /// <summary>
        /// Name fragments that mean a control's contents must never be echoed.
        /// </summary>
        /// <remarks>
        /// This app holds live OAuth access and refresh tokens for four platforms, and its own log file
        /// already carries them when diagnostic logging is on. A tree dump is read straight into an
        /// agent's context, so it is a worse place for a credential to surface than a local log. Any
        /// control whose name or binding path looks like it holds a secret reports [REDACTED] instead of
        /// its value. Matching on names is imprecise by nature, so it errs toward redacting.
        /// </remarks>
        private static readonly string[] SecretNameFragments = new string[]
        {
            "password", "passwd", "secret", "token", "apikey", "api_key", "clientid", "client_id",
            "clientsecret", "credential", "bearer", "oauth", "authkey", "accesskey", "privatekey",
            "webhook", "connectionstring",
        };

        public const string RedactedText = "[REDACTED]";

        /// <summary>
        /// Above this many items, realized containers are not counted. See AppendItemsInfo.
        /// </summary>
        private const int RealizedCountCeiling = 1000;

        /// <summary>
        /// The x:Name of an element, or null when it has none.
        /// </summary>
        public static string GetName(DependencyObject element)
        {
            if (element is FrameworkElement frameworkElement && !string.IsNullOrEmpty(frameworkElement.Name))
            {
                return frameworkElement.Name;
            }
            if (element is FrameworkContentElement contentElement && !string.IsNullOrEmpty(contentElement.Name))
            {
                return contentElement.Name;
            }
            return null;
        }

        /// <summary>
        /// The most identifying text an element carries, already truncated and redacted.
        /// </summary>
        public static string GetText(DependencyObject element)
        {
            // Checked first and unconditionally. A PasswordBox exists to hold a secret, so its content
            // is never reportable regardless of what it is named.
            if (element is PasswordBox)
            {
                return RedactedText;
            }

            string raw = ExtractRawText(element);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (LooksSecret(GetName(element)))
            {
                return RedactedText;
            }

            return Shorten(raw);
        }

        private static string ExtractRawText(DependencyObject element)
        {
            switch (element)
            {
                case Window window:
                    return window.Title;
                case TextBlock textBlock:
                    return textBlock.Text;
                case TextBoxBase textBoxBase:
                    return textBoxBase is TextBox textBox ? textBox.Text : null;
                case Run run:
                    return run.Text;
                // Before ContentControl: a TabItem's Header is what identifies it, not its Content,
                // which is the whole page underneath.
                case HeaderedContentControl headered:
                    return headered.Header as string;
                case ComboBox comboBox:
                    // ComboBox.Text is populated from the selection, so the default ToString arrives
                    // through it rather than through SelectedItem. Both paths need the same check.
                    return Stringify(
                        !string.IsNullOrEmpty(comboBox.Text) ? comboBox.Text : comboBox.SelectedItem?.ToString(),
                        comboBox.SelectedItem);
                case ContentControl contentControl:
                    return contentControl.Content as string;
                default:
                    return null;
            }
        }

        /// <summary>
        /// An object's text, but only when it actually has one.
        /// </summary>
        /// <remarks>
        /// Object.ToString returns the full type name when a type does not override it, so a ComboBox
        /// bound to view models was reporting "MixItUp.Base.ViewModel.Chat.PlatformOption" as its
        /// displayed text. That is not what is on screen, and it is actively misleading: the real text
        /// comes from the item template underneath. Detecting the default implementation and declining to
        /// report it is more honest than echoing a type name as a label.
        /// </remarks>
        private static string Stringify(string candidate, object source)
        {
            if (string.IsNullOrEmpty(candidate) || source == null)
            {
                return candidate;
            }

            return string.Equals(candidate, source.GetType().FullName, StringComparison.Ordinal) ? null : candidate;
        }

        private static bool LooksSecret(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string lowered = name.ToLowerInvariant();
            foreach (string fragment in SecretNameFragments)
            {
                if (lowered.Contains(fragment))
                {
                    return true;
                }
            }
            return false;
        }

        private static string Shorten(string value)
        {
            // Newlines and tabs would break the one-line-per-element contract.
            string collapsed = StripUnprintable(value).Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
            while (collapsed.Contains("  "))
            {
                collapsed = collapsed.Replace("  ", " ");
            }

            if (collapsed.Length == 0)
            {
                return null;
            }

            if (collapsed.Length <= MaxTextLength)
            {
                return collapsed;
            }
            return collapsed.Substring(0, MaxTextLength) + "…";
        }

        /// <summary>
        /// Drops characters that carry no meaning when read as text.
        /// </summary>
        /// <remarks>
        /// This app draws its icons with an icon font, so a MaterialSymbolIcon's Text is a codepoint in
        /// the Unicode private use area. Those render as a glyph on screen and as a replacement box or
        /// nothing at all in a tool result, so they were showing up as empty quotes on hundreds of lines.
        /// An icon has no text, and saying so is more accurate than emitting a character the caller
        /// cannot interpret.
        /// </remarks>
        private static string StripUnprintable(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                bool privateUse = (c >= '\uE000' && c <= '\uF8FF');
                bool unusable = char.IsControl(c) && c != '\r' && c != '\n' && c != '\t';
                if (!privateUse && !unusable)
                {
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// State flags worth a token on the line. Only states an agent would act on are emitted, so an
        /// ordinary enabled visible control contributes nothing.
        /// </summary>
        public static void AppendStateFlags(StringBuilder builder, DependencyObject element)
        {
            if (element is UIElement uiElement)
            {
                if (uiElement.Visibility == Visibility.Collapsed)
                {
                    builder.Append(" collapsed");
                }
                else if (uiElement.Visibility == Visibility.Hidden)
                {
                    builder.Append(" hidden");
                }

                if (!uiElement.IsEnabled)
                {
                    builder.Append(" disabled");
                }

                if (uiElement.IsKeyboardFocusWithin)
                {
                    builder.Append(" focuswithin");
                }
            }

            if (element is ToggleButton toggle)
            {
                builder.Append(toggle.IsChecked == null ? " checked=null" : (toggle.IsChecked.Value ? " checked=true" : " checked=false"));
            }

            if (element is Selector selector)
            {
                builder.Append(" sel=").Append(selector.SelectedIndex.ToString());
            }

            if (element is RangeBase rangeBase)
            {
                builder.Append(" value=").Append(rangeBase.Value.ToString("0.##"));
            }

            if (element is TextBoxBase textBoxBase && textBoxBase.IsReadOnly)
            {
                builder.Append(" readonly");
            }
        }

        /// <summary>
        /// Item count and how much of it is actually realized, for an items control.
        /// </summary>
        /// <remarks>
        /// This is the honest answer to a real limitation. Virtualized items that have not been realized
        /// have no visual at all, so they cannot appear in a visual tree dump no matter how deep it
        /// goes. Reporting "items=412 realized=18" tells the agent that scrolling or reading the bound
        /// collection through the view model is the way to see the rest, instead of leaving it to
        /// conclude the list only holds 18 things.
        /// </remarks>
        public static void AppendItemsInfo(StringBuilder builder, DependencyObject element)
        {
            if (!(element is ItemsControl itemsControl))
            {
                return;
            }

            int total;
            try
            {
                total = itemsControl.Items.Count;
            }
            catch (Exception)
            {
                // Enumerating some bound collections can throw if they are being mutated.
                return;
            }

            builder.Append(" items=").Append(total.ToString());

            bool virtualizing = itemsControl.ItemsPanel != null && VirtualizingPanel.GetIsVirtualizing(itemsControl);
            if (!virtualizing)
            {
                return;
            }

            // Counting realized containers is a lookup per item, so it is skipped entirely on very
            // large lists rather than turning a tree dump into an O(n) walk of every row in chat
            // history. The item count alone already tells the agent the list is bigger than the dump.
            if (total > RealizedCountCeiling)
            {
                builder.Append(" virtualized realized=unknown");
                return;
            }

            int realized = 0;
            for (int i = 0; i < total; i++)
            {
                if (itemsControl.ItemContainerGenerator.ContainerFromIndex(i) != null)
                {
                    realized++;
                }
            }

            builder.Append(" realized=").Append(realized.ToString());
            if (realized < total)
            {
                builder.Append(" virtualized");
            }
        }

        /// <summary>
        /// Reports bindings on this element that are not resolving.
        /// </summary>
        /// <remarks>
        /// The obvious thing to surface would be Validation.HasError, but this app configures no
        /// validation anywhere: Validation appears in zero of its XAML files, so that flag is always
        /// false and reporting it would be noise dressed as a signal. A broken binding path is the
        /// failure that actually happens in WPF, and it is the quiet kind, because a binding whose path
        /// does not resolve renders as empty rather than throwing. Walking locally set values is cheap
        /// and finds exactly those.
        /// </remarks>
        public static string GetBrokenBindings(DependencyObject element)
        {
            List<string> broken = null;

            try
            {
                LocalValueEnumerator enumerator = element.GetLocalValueEnumerator();
                while (enumerator.MoveNext())
                {
                    LocalValueEntry entry = enumerator.Current;
                    if (!(entry.Value is BindingExpressionBase expression))
                    {
                        continue;
                    }

                    if (!IsBindingBroken(expression))
                    {
                        continue;
                    }

                    broken = broken ?? new List<string>();
                    broken.Add(entry.Property.Name);
                }
            }
            catch (Exception)
            {
                // Never let diagnostics of a broken binding be what breaks the dump.
                return null;
            }

            return broken != null ? string.Join(",", broken) : null;
        }

        private static bool IsBindingBroken(BindingExpressionBase expression)
        {
            if (expression.HasError || expression.HasValidationError)
            {
                return true;
            }

            // PathError is the one that matters: the binding path did not resolve against the source,
            // which is the classic silent WPF failure.
            return expression.Status == BindingStatus.PathError
                || expression.Status == BindingStatus.UpdateSourceError
                || expression.Status == BindingStatus.UpdateTargetError;
        }
    }
}
