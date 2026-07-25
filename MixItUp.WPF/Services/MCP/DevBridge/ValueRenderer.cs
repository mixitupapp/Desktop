#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    /// <summary>
    /// Renders a CLR value as one short line, and decides which of an object's members are worth listing.
    /// </summary>
    /// <remarks>
    /// This is where the density requirement is enforced for the property tools. The tree tools got a
    /// 12.6x reduction out of <see cref="ElementInterest"/> by refusing to emit theme plumbing, and
    /// reading properties can undo that just as easily from the other direction: a bare
    /// <c>GetProperties</c> on any FrameworkElement returns well over a hundred entries, almost all of
    /// them WPF's rather than this app's, and expanding nested values would turn a single read of a view
    /// model into a transitive dump of the object graph behind it.
    /// <para>
    /// Two rules follow. <b>Nothing is ever expanded recursively</b>: a nested value is summarized to one
    /// line and given a handle, so the caller drills in deliberately, one hop per call, the way a
    /// debugger's locals window works. And <b>framework-declared members are hidden by default</b>,
    /// except for a curated set that a caller genuinely needs, because SelectedIndex and Visibility live
    /// on WPF base classes and are the whole point of reaching a control.
    /// </para>
    /// </remarks>
    internal static class ValueRenderer
    {
        /// <summary>
        /// How much of a rendered value to keep. Matches the tree dump's text budget so output from the
        /// two halves of the bridge reads consistently.
        /// </summary>
        private const int MaxValueLength = 120;

        /// <summary>
        /// Namespaces belonging to the framework or a third-party library rather than to this app.
        /// </summary>
        private static readonly string[] ForeignNamespacePrefixes = new string[]
        {
            "System", "Microsoft", "MaterialDesignThemes", "GongSolutions", "Newtonsoft", "MahApps",
        };

        /// <summary>
        /// Framework-declared members that are shown anyway, because they are what a caller reaches a
        /// control for in the first place.
        /// </summary>
        /// <remarks>
        /// SelectedIndex is the load-bearing entry. Navigation throughout this app is a ListBox selection,
        /// and SelectedIndex is declared on Selector, so without this list the one property needed to
        /// drive the app would be filtered out as framework noise.
        /// <para>
        /// Password is deliberately absent, and reaching it by explicit path is redacted anyway.
        /// </para>
        /// </remarks>
        private static readonly HashSet<string> AlwaysInterestingMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            // Identity and content.
            "Name", "Title", "Text", "Content", "Header", "Tag", "ToolTip",
            // State a caller branches on.
            "Visibility", "IsEnabled", "IsVisible", "IsChecked", "IsSelected", "IsExpanded", "IsActive",
            "IsReadOnly", "IsFocused", "IsKeyboardFocusWithin", "IsLoaded", "IsOpen",
            // Selection, which is how this app navigates.
            "SelectedIndex", "SelectedItem", "SelectedValue", "SelectedValuePath", "DisplayMemberPath",
            // Collections.
            "Items", "ItemsSource", "HasItems",
            // The view model boundary, which is the whole reason the bridge exists.
            "DataContext",
            // Commands, so ui_invoke targets can be found without a second call.
            "Command", "CommandParameter",
            // Ranges.
            "Value", "Minimum", "Maximum",
            // Layout, for the common "why is it not showing" question.
            "ActualWidth", "ActualHeight",
        };

        /// <summary>
        /// Property names checked, in order, for a label when a type does not override ToString.
        /// </summary>
        /// <remarks>
        /// Without this a list of view models renders as a column of identical type names.
        /// MainMenuItem is exactly that case: it does not override ToString, so the menu that drives all
        /// navigation in this app would list eighteen indistinguishable "MainMenuItem" rows. Reading its
        /// Name turns the same output into something a caller can act on, which is the same thing the
        /// tree dump does when it derives a label for an unnamed actionable element.
        /// </remarks>
        private static readonly string[] LabelMemberNames = new string[]
        {
            "Name", "DisplayName", "Title", "Header", "Text", "Label", "Description", "Id", "ID", "Key",
        };

        /// <summary>
        /// A value rendered as a single line, redacted where the policy requires it.
        /// </summary>
        public static string Summarize(object value, Type ownerType = null, string memberName = null)
        {
            if (ownerType != null && SecretGuard.ShouldRedact(ownerType, memberName))
            {
                return SecretGuard.RedactedText;
            }

            if (value == null)
            {
                return "null";
            }

            if (SecretGuard.IsSecretValue(value))
            {
                return SecretGuard.RedactedText;
            }

            if (IsScalar(value.GetType()))
            {
                return Shorten(FormatScalar(value));
            }

            // A collection reports its size rather than its contents. Reading the contents is a separate,
            // paged call, which is what keeps a 40,000-message chat history from arriving as one value.
            if (value is IEnumerable enumerable && !(value is string))
            {
                string count = TryCount(enumerable, out int items) ? items.ToString(CultureInfo.InvariantCulture) : "?";
                return $"{value.GetType().Name}[{count}]";
            }

            string label = DeriveLabel(value);
            return label != null
                ? Shorten($"{value.GetType().Name} {label}")
                : value.GetType().Name;
        }

        /// <summary>
        /// A type whose value is meaningful on its own, so it is returned rather than summarized.
        /// </summary>
        public static bool IsScalar(Type type)
        {
            if (type == null)
            {
                return false;
            }

            Type effective = Nullable.GetUnderlyingType(type) ?? type;

            return effective.IsPrimitive
                || effective.IsEnum
                || effective == typeof(string)
                || effective == typeof(decimal)
                || effective == typeof(DateTime)
                || effective == typeof(DateTimeOffset)
                || effective == typeof(TimeSpan)
                || effective == typeof(Guid)
                || effective == typeof(Uri);
        }

        private static string FormatScalar(object value)
        {
            switch (value)
            {
                case bool flag:
                    // Lowercased so it matches what ui_set accepts, rather than "True".
                    return flag ? "true" : "false";
                case double number:
                    return number.ToString("0.####", CultureInfo.InvariantCulture);
                case float number:
                    return number.ToString("0.####", CultureInfo.InvariantCulture);
                case DateTime timestamp:
                    return timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }

        /// <summary>
        /// A short human label for an object, or null when it has none.
        /// </summary>
        public static string DeriveLabel(object value)
        {
            if (value == null)
            {
                return null;
            }

            Type type = value.GetType();

            // An overridden ToString is the author saying how this should read, so it wins.
            string text = SafeToString(value);
            if (text != null && !string.Equals(text, type.FullName, StringComparison.Ordinal))
            {
                return text;
            }

            // No override, so fall back to whichever label-ish property the type happens to have.
            foreach (string memberName in LabelMemberNames)
            {
                MemberInfo member = PropertyPath.FindMember(type, memberName);
                if (member == null || SecretGuard.ShouldRedact(member))
                {
                    continue;
                }

                object memberValue = TryRead(member, value);
                if (memberValue == null)
                {
                    continue;
                }

                if (memberValue is string candidate)
                {
                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        return $"{memberName}=\"{candidate}\"";
                    }
                    continue;
                }

                if (IsScalar(memberValue.GetType()))
                {
                    return $"{memberName}={FormatScalar(memberValue)}";
                }
            }

            return null;
        }

        /// <summary>
        /// Whether a member earns a line when listing an object's properties.
        /// </summary>
        /// <param name="includeFrameworkMembers">
        /// Turn on to see everything the runtime type declares. Off by default because on a
        /// FrameworkElement that is well over a hundred WPF members, which buries the handful this app
        /// declared.
        /// </param>
        public static bool IsInterestingMember(MemberInfo member, bool includeFrameworkMembers)
        {
            if (member is PropertyInfo property)
            {
                // An indexer is reached with [ ] rather than by name, so it cannot be listed usefully.
                if (property.GetIndexParameters().Length > 0)
                {
                    return false;
                }
                if (property.GetMethod == null || !property.GetMethod.IsPublic)
                {
                    return false;
                }
            }

            if (includeFrameworkMembers)
            {
                return true;
            }

            if (AlwaysInterestingMembers.Contains(member.Name))
            {
                return true;
            }

            return !IsForeignType(member.DeclaringType);
        }

        /// <summary>
        /// Whether a type belongs to the framework or a third-party library rather than to this app.
        /// </summary>
        public static bool IsForeignType(Type type)
        {
            string ns = type?.Namespace;
            if (string.IsNullOrEmpty(ns))
            {
                return false;
            }

            foreach (string prefix in ForeignNamespacePrefixes)
            {
                if (ns.Equals(prefix, StringComparison.Ordinal) || ns.StartsWith(prefix + ".", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reads a member without letting a throwing getter break the listing.
        /// </summary>
        public static object TryRead(MemberInfo member, object owner)
        {
            try
            {
                if (member is PropertyInfo property)
                {
                    return property.GetMethod != null && property.GetMethod.IsPublic ? property.GetValue(owner) : null;
                }
                if (member is FieldInfo field)
                {
                    return field.GetValue(owner);
                }
            }
            catch (Exception)
            {
                // Reported as unreadable by the caller rather than aborting the whole listing. A getter
                // that throws when its dependencies are not ready is common in this app's view models.
            }
            return null;
        }

        /// <summary>
        /// Reads a member, distinguishing a throwing getter from a null value.
        /// </summary>
        public static bool TryReadDetailed(MemberInfo member, object owner, out object value, out string error)
        {
            value = null;
            error = null;

            try
            {
                if (member is PropertyInfo property)
                {
                    value = property.GetValue(owner);
                    return true;
                }
                if (member is FieldInfo field)
                {
                    value = field.GetValue(owner);
                    return true;
                }
                error = "not a readable member";
                return false;
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                error = $"{inner.GetType().Name}: {inner.Message}";
                return false;
            }
            catch (Exception ex)
            {
                error = $"{ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// The declared type of a member, spelled the way a caller would recognise it.
        /// </summary>
        public static string DescribeType(Type type)
        {
            if (type == null)
            {
                return "unknown";
            }

            Type underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                return DescribeType(underlying) + "?";
            }

            if (!type.IsGenericType)
            {
                return type.Name;
            }

            // Rendered as ObservableCollection<ChatMessageViewModel> rather than the mangled `1 form.
            StringBuilder builder = new StringBuilder();
            string name = type.Name;
            int tick = name.IndexOf('`');
            builder.Append(tick > 0 ? name.Substring(0, tick) : name);
            builder.Append('<');

            Type[] arguments = type.GetGenericArguments();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(DescribeType(arguments[i]));
            }

            builder.Append('>');
            return builder.ToString();
        }

        /// <summary>
        /// Describes what a target is, for echoing back what a call actually acted on.
        /// </summary>
        public static string DescribeTarget(object target)
        {
            if (target == null)
            {
                return "null";
            }

            string type = target.GetType().Name;

            if (target is DependencyObject element)
            {
                string name = ElementDescriber.GetName(element);
                return string.IsNullOrEmpty(name) ? type : $"{type} #{name}";
            }

            string label = DeriveLabel(target);
            return label != null ? $"{type} {label}" : type;
        }

        /// <summary>
        /// Counts a sequence without enumerating it when it can be avoided.
        /// </summary>
        public static bool TryCount(IEnumerable sequence, out int count)
        {
            count = 0;

            try
            {
                if (sequence is ICollection collection)
                {
                    count = collection.Count;
                    return true;
                }

                // ThreadSafeObservableCollection<T> marshals its own enumeration through the dispatcher,
                // so this stays correct as long as it is reached on the UI thread, which the gate
                // guarantees.
                foreach (object unused in sequence)
                {
                    if (++count > 100000)
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The prefix a handle for this object should carry. Element handles must resolve to a
        /// DependencyObject, since that is what the tree tools require of them.
        /// </summary>
        public static string HandlePrefix(object value)
        {
            return value is DependencyObject ? "e" : "d";
        }

        /// <summary>
        /// Whether an object is worth minting a handle for. A scalar has nothing to reach into.
        /// </summary>
        public static bool DeservesHandle(object value)
        {
            return value != null && !IsScalar(value.GetType()) && !SecretGuard.IsSecretValue(value);
        }

        private static string SafeToString(object value)
        {
            try
            {
                return value.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Shorten(string value)
        {
            if (value == null)
            {
                return null;
            }

            string collapsed = value.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
            while (collapsed.Contains("  "))
            {
                collapsed = collapsed.Replace("  ", " ");
            }

            return collapsed.Length <= MaxValueLength ? collapsed : collapsed.Substring(0, MaxValueLength) + "…";
        }
    }
}
