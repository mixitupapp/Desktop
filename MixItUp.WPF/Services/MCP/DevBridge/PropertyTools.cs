#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using MixItUp.WPF.Services.MCP.Tools;
using ModelContextProtocol.Server;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    /// <summary>
    /// Reading and writing the state behind the UI, rather than the shape of it.
    /// </summary>
    /// <remarks>
    /// This is the half of the bridge that reaches past what is drawn. A screenshot says a button is
    /// greyed out and a tree dump says which one, but only a property read says that the view model behind
    /// it has no selected item, which is why.
    /// <para>
    /// <b>Reading is deliberately one level deep.</b> Nothing here recurses. A scalar comes back as a
    /// value, a collection comes back as a page of one-line item summaries, and an object comes back as a
    /// list of its properties summarized to one line each, with a handle so the next hop is a separate
    /// call. The model is a debugger's locals window, and it is what keeps this from undoing the density
    /// work in the tree tools: the alternative, expanding nested values, turns one read of a view model
    /// into a transitive dump of everything reachable from it.
    /// </para>
    /// <para>
    /// <b>Writing exists because this app's state mostly flows through two-way bindings</b> rather than
    /// through commands. There is no command for "type this into the chat box" or "select that tab", so a
    /// bridge that could only invoke commands could not drive the app to most of its screens.
    /// </para>
    /// </remarks>
    [McpServerToolType]
    public class PropertyTools
    {
        /// <summary>
        /// Default and maximum page size when listing a collection.
        /// </summary>
        private const int DefaultItemLimit = 50;

        private const int MaxItemLimit = 200;

        /// <summary>
        /// Default and maximum number of properties listed for an object.
        /// </summary>
        private const int DefaultPropertyLimit = 100;

        private const int MaxPropertyLimit = 400;

        [McpServerTool(Name = "ui_get", ReadOnly = true)]
        [Description("Read a property, or list what an object holds, anywhere in the running app's object graph. Reads one level only: a scalar comes back as a value, a collection as a page of item summaries with an index for each, and an object as a list of its properties with a handle so you can drill in with another call. This is how you answer questions a screenshot cannot, such as why a button is disabled. Prefer a 'd' view model handle as the target, since those survive list recycling.")]
        public static Task<GetResult> Get(
            [Description("What to read. Accepts a handle from a previous call ('e12' for an element, 'd5' for a view model), an x:Name prefixed with # (#MenuItemsListBox), or a type name (ChatControl). Omit to use the active window, falling back to the main window.")] string element = null,
            [Description("Dotted property path from the target. Supports nesting and indexing: 'DataContext', 'SelectedItem.Name', 'Items[3].Id', 'Commands[\"a-guid\"]'. Omit to list the target's own properties.")] string path = null,
            [Description("Where to start when the value is a collection. Default 0.")] int offset = 0,
            [Description("How many collection items to return. Clamped to 1-200, default 50.")] int limit = DefaultItemLimit,
            [Description("Include properties declared by WPF and other libraries rather than by this app. Default false, which is worth keeping: on any control that is well over a hundred framework members burying the handful the app declared. A curated set of framework properties that actually matter, including SelectedIndex, Visibility, IsEnabled, Items and DataContext, is always shown regardless.")] bool includeFrameworkProperties = false,
            [Description("How many properties to list. Clamped to 1-400, default 100.")] int maxProperties = DefaultPropertyLimit)
        {
            return DevBridgeGate.RunOnUI("ui_get", () =>
            {
                GetResult result = new GetResult();

                object target = UITools.ResolveObjectTarget(element, result);
                if (target == null)
                {
                    return result;
                }

                result.Target = ValueRenderer.DescribeTarget(target);
                result.TargetHandle = HandleRegistry.Instance.HandleFor(target, ValueRenderer.HandlePrefix(target));
                result.Path = string.IsNullOrWhiteSpace(path) ? null : path.Trim();

                PathResolution resolution = PropertyPath.Resolve(target, path);
                if (!resolution.IsOk)
                {
                    result.Status = resolution.Status;
                    result.Message = resolution.Message;
                    return result;
                }

                // Redaction is applied against the member that was named, so a path ending in AccessToken
                // is refused even though nothing about the control it came from looks like a secret. This
                // is the case the tree tools structurally could not have.
                if (resolution.HasFinalStep
                    && !resolution.FinalStep.IsIndex
                    && SecretGuard.ShouldRedact(resolution.Owner?.GetType(), resolution.FinalStep.Text))
                {
                    result.Kind = "redacted";
                    result.ValueType = ValueRenderer.DescribeType(resolution.DeclaredType);
                    result.Value = SecretGuard.RedactedText;
                    result.Message = "This property's name marks it as holding a credential, so its value is never returned. The app holds live OAuth tokens and API keys, and a tool result goes straight into agent context.";
                    return result;
                }

                Populate(result, resolution.Value, resolution.DeclaredType, offset, limit, includeFrameworkProperties, maxProperties);
                return result;
            });
        }

        [McpServerTool(Name = "ui_set", ReadOnly = false, Destructive = true)]
        [Description("Write a property anywhere in the running app's object graph. This is the main way to drive the app: most of its state flows through two-way bindings rather than commands, so selecting a page, typing into a box, or picking a list item is a property write. Setting a selector's SelectedIndex raises SelectionChanged exactly as a click would, so the app's own handlers run. The value is text and is converted to the property's real type, so pass '3', 'true' or an enum member name. Returns the previous value so you can confirm what changed and put it back.")]
        public static Task<SetResult> Set(
            [Description("What to write to. Accepts a handle from a previous call ('e12' for an element, 'd5' for a view model), an x:Name prefixed with # (#MenuItemsListBox), or a type name (ChatControl). Omit to use the active window, falling back to the main window.")] string element = null,
            [Description("Dotted path to the property to write, relative to the target. The last segment is the property that gets set: 'SelectedIndex', 'DataContext.IsEnabled', 'Items[2].Name'. Required.")] string property = null,
            [Description("The value, as text. It is converted to the property's declared type, so '3' for an int, 'true' for a bool, 'Collapsed' for a Visibility, or a member name for any enum. An empty string clears a nullable property. Types with a XAML type converter accept the same spelling they would in markup.")] string value = null,
            [Description("Set the property to null instead of using 'value'. Fails with coercion_failed on a non-nullable value type.")] bool setToNull = false)
        {
            if (string.IsNullOrWhiteSpace(property))
            {
                throw new ModelContextProtocol.McpException("The 'property' parameter is required. Pass the path to the property to write, for example 'SelectedIndex' or 'DataContext.IsEnabled'.");
            }

            return DevBridgeGate.RunOnUI("ui_set", () =>
            {
                SetResult result = new SetResult();

                object target = UITools.ResolveObjectTarget(element, result);
                if (target == null)
                {
                    return result;
                }

                result.Target = ValueRenderer.DescribeTarget(target);
                result.Property = property.Trim();

                PathResolution resolution = PropertyPath.Resolve(target, property);
                if (!resolution.IsOk)
                {
                    result.Status = resolution.Status;
                    result.Message = resolution.Message;
                    return result;
                }

                if (!resolution.HasFinalStep)
                {
                    result.Status = DevBridgeStatus.NotSettable;
                    result.Message = "The path resolved to the target itself, so there is no property to write. Name the property to set.";
                    return result;
                }

                Type declaredType = resolution.DeclaredType;
                result.PropertyType = ValueRenderer.DescribeType(declaredType);

                bool redact = SecretGuard.ShouldRedact(resolution.Owner?.GetType(), resolution.FinalStep.IsIndex ? null : resolution.FinalStep.Text);

                if (!PropertyPath.TryCoerce(value, declaredType, setToNull, out object coerced, out string coercionError))
                {
                    result.Status = DevBridgeStatus.CoercionFailed;
                    result.Message = coercionError;
                    return result;
                }

                // Captured before the write so the result can report what it replaced, which is what makes
                // a change reversible without a second read.
                result.PreviousValue = redact ? SecretGuard.RedactedText : ValueRenderer.Summarize(resolution.Value);

                PathResolution written = PropertyPath.Write(resolution, coerced);
                if (!written.IsOk)
                {
                    result.Status = written.Status;
                    result.Message = written.Message;
                    return result;
                }

                // Read back rather than echoing what was sent. A setter that coerces, clamps, or ignores
                // the value is common, and reporting the value asked for would hide that.
                object actual = ReadBack(resolution, out string readBackError);
                result.NewValue = redact ? SecretGuard.RedactedText : ValueRenderer.Summarize(actual);
                result.Changed = !string.Equals(result.PreviousValue, result.NewValue, StringComparison.Ordinal);

                if (readBackError != null)
                {
                    result.Message = $"The write succeeded but reading the value back failed: {readBackError}";
                }
                else if (!result.Changed)
                {
                    result.Message = "The write succeeded but the value did not change. Either it already held this value, or the property is bound to a source that immediately overwrote it, or its setter rejected the value silently.";
                }

                // Audited rather than logged at Information, because the default log level is Warning and
                // an audit trail that only exists on verbose builds is not one. The value itself is never
                // written to the log when the property looks like a credential.
                ToolHelpers.LogToolAction("ui_set", $"{result.Target}.{result.Property} = {(redact ? SecretGuard.RedactedText : result.NewValue)} (was {result.PreviousValue})");

                return result;
            });
        }

        /// <summary>
        /// Re-reads the property that was just written, so the result reports what the app now holds
        /// rather than what the caller asked for.
        /// </summary>
        private static object ReadBack(PathResolution resolution, out string error)
        {
            error = null;

            try
            {
                if (resolution.Member is PropertyInfo property)
                {
                    return property.GetMethod != null && property.GetMethod.IsPublic ? property.GetValue(resolution.Owner) : null;
                }
                if (resolution.Member is FieldInfo field)
                {
                    return field.GetValue(resolution.Owner);
                }

                // An indexed write has no MemberInfo, so go back through the path walker.
                PathResolution reread = PropertyPath.Resolve(resolution.Owner, "[" + resolution.FinalStep.Text + "]");
                if (!reread.IsOk)
                {
                    error = reread.Message;
                    return null;
                }
                return reread.Value;
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                error = $"{inner.GetType().Name}: {inner.Message}";
                return null;
            }
            catch (Exception ex)
            {
                error = $"{ex.GetType().Name}: {ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// Fills in the result according to what kind of thing the path landed on.
        /// </summary>
        private static void Populate(GetResult result, object value, Type declaredType, int offset, int limit, bool includeFrameworkProperties, int maxProperties)
        {
            result.ValueType = value != null ? ValueRenderer.DescribeType(value.GetType()) : ValueRenderer.DescribeType(declaredType);

            if (value == null)
            {
                result.Kind = "null";
                result.Value = "null";
                return;
            }

            if (SecretGuard.IsSecretValue(value))
            {
                result.Kind = "redacted";
                result.Value = SecretGuard.RedactedText;
                result.Message = "This value's type exists to carry credentials, so its contents are never returned.";
                return;
            }

            if (ValueRenderer.IsScalar(value.GetType()))
            {
                result.Kind = "scalar";
                result.Value = ValueRenderer.Summarize(value);
                return;
            }

            result.Handle = HandleRegistry.Instance.HandleFor(value, ValueRenderer.HandlePrefix(value));

            if (value is IEnumerable sequence && !(value is string))
            {
                PopulateCollection(result, sequence, offset, limit);
                return;
            }

            PopulateObject(result, value, includeFrameworkProperties, maxProperties);
        }

        private static void PopulateCollection(GetResult result, IEnumerable sequence, int offset, int limit)
        {
            result.Kind = "collection";
            result.Items = new List<ItemLine>();

            int start = Math.Max(0, offset);
            int take = Clamp(limit, 1, MaxItemLimit);

            result.Offset = start;

            if (ValueRenderer.TryCount(sequence, out int total))
            {
                result.ItemCount = total;
            }
            else
            {
                result.ItemCount = -1;
            }

            int index = 0;
            try
            {
                foreach (object item in sequence)
                {
                    if (index++ < start)
                    {
                        continue;
                    }

                    if (result.Items.Count >= take)
                    {
                        result.Truncated = true;
                        break;
                    }

                    result.Items.Add(new ItemLine()
                    {
                        Index = index - 1,
                        Type = item != null ? item.GetType().Name : "null",
                        // The derived label is what makes this usable. Most of this app's collections hold
                        // view models that do not override ToString, so without it every row of the menu
                        // that drives navigation would read as an identical type name.
                        Summary = ValueRenderer.Summarize(item),
                        Handle = ValueRenderer.DeservesHandle(item)
                            ? HandleRegistry.Instance.HandleFor(item, ValueRenderer.HandlePrefix(item))
                            : null,
                    });
                }
            }
            catch (Exception ex)
            {
                result.Message = $"Enumeration stopped after {result.Items.Count} items: {ex.GetType().Name}: {ex.Message}. A bound collection being mutated while it is read will do this, so retry.";
                return;
            }

            result.Value = (result.ItemCount < 0 ? "?" : result.ItemCount.ToString()) + " items";

            if (result.Truncated)
            {
                result.Hint = $"Showing items {start} to {start + result.Items.Count - 1}. Raise offset to page forward, or read one item directly with a path like '[{start + result.Items.Count}]'.";
            }
            else if (result.Items.Count == 0)
            {
                result.Hint = result.ItemCount > 0
                    ? $"The collection holds {result.ItemCount} items but offset {start} is past the end."
                    : "The collection is empty.";
            }
        }

        private static void PopulateObject(GetResult result, object value, bool includeFrameworkProperties, int maxProperties)
        {
            result.Kind = "object";
            result.Properties = new List<PropertyLine>();
            result.Value = ValueRenderer.Summarize(value);

            Type type = value.GetType();
            int cap = Clamp(maxProperties, 1, MaxPropertyLimit);

            List<MemberInfo> members = new List<MemberInfo>();
            int hidden = 0;

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (ValueRenderer.IsInterestingMember(property, includeFrameworkProperties))
                {
                    members.Add(property);
                }
                else if (property.GetIndexParameters().Length == 0)
                {
                    hidden++;
                }
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (ValueRenderer.IsInterestingMember(field, includeFrameworkProperties))
                {
                    members.Add(field);
                }
                else
                {
                    hidden++;
                }
            }

            members.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));

            foreach (MemberInfo member in members)
            {
                if (result.Properties.Count >= cap)
                {
                    result.Truncated = true;
                    break;
                }

                result.Properties.Add(Describe(member, value));
            }

            result.HiddenPropertyCount = hidden;

            List<string> hints = new List<string>();
            if (result.Truncated)
            {
                hints.Add($"Stopped at the {cap} property limit. Raise maxProperties or read one property directly with a path.");
            }
            if (hidden > 0 && !includeFrameworkProperties)
            {
                hints.Add($"{hidden} framework-declared properties were hidden. Pass includeFrameworkProperties=true to see them, or name one directly in a path, which always works.");
            }
            if (result.Properties.Count == 0 && hidden == 0)
            {
                hints.Add($"{type.Name} exposes no public properties or fields.");
            }

            if (hints.Count > 0)
            {
                result.Hint = string.Join(" ", hints);
            }
        }

        private static PropertyLine Describe(MemberInfo member, object owner)
        {
            PropertyInfo property = member as PropertyInfo;
            FieldInfo field = member as FieldInfo;

            PropertyLine line = new PropertyLine()
            {
                Name = member.Name,
                Type = ValueRenderer.DescribeType(property != null ? property.PropertyType : field.FieldType),
                Settable = property != null
                    ? property.SetMethod != null && property.SetMethod.IsPublic
                    : !(field.IsInitOnly || field.IsLiteral),
            };

            if (SecretGuard.ShouldRedact(member))
            {
                line.Value = SecretGuard.RedactedText;
                return line;
            }

            if (!ValueRenderer.TryReadDetailed(member, owner, out object memberValue, out string error))
            {
                // A throwing getter is reported rather than hidden. In this app's view models it usually
                // means a dependency is not initialized yet, which is itself the answer to why a screen
                // looks wrong.
                line.Value = $"<threw {error}>";
                return line;
            }

            line.Value = ValueRenderer.Summarize(memberValue);

            // A handle only for what can be reached into, so a listing does not mint one per string.
            if (ValueRenderer.DeservesHandle(memberValue))
            {
                line.Handle = HandleRegistry.Instance.HandleFor(memberValue, ValueRenderer.HandlePrefix(memberValue));
            }

            return line;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }

    /// <summary>
    /// One property of an object, summarized to a single line.
    /// </summary>
    public class PropertyLine
    {
        [Description("The property or field name. Append it to the path you used to read this object to reach it.")]
        public string Name { get; set; }

        [Description("Declared type, which is what ui_set converts a value to. Not necessarily the runtime type of the value.")]
        public string Type { get; set; }

        [Description("The value as one line. A collection reads as Type[count], an object as its type plus a derived label, and anything holding a credential as [REDACTED]. '<threw ...>' means the getter raised, which is often itself the finding.")]
        public string Value { get; set; }

        [Description("Whether ui_set can write this. False means there is no public setter, so look for the property this one is computed from.")]
        public bool Settable { get; set; }

        [Description("Handle for this value, present only when it is an object or collection worth reaching into. Pass it as 'element' to read it without repeating the path.")]
        public string Handle { get; set; }
    }

    /// <summary>
    /// One item of a collection, summarized to a single line.
    /// </summary>
    public class ItemLine
    {
        [Description("Position in the collection. This is what a selector's SelectedIndex expects, so it is the value to write to navigate.")]
        public int Index { get; set; }

        [Description("Runtime type name of the item.")]
        public string Type { get; set; }

        [Description("The item as one line. For a type with no meaningful ToString this is a label read off the item, such as Name=\"Commands\".")]
        public string Summary { get; set; }

        [Description("Handle for this item. A handle taken from a collection is keyed on the item itself, so unlike a container handle it survives list recycling.")]
        public string Handle { get; set; }
    }

    public class GetResult : DevBridgeResult
    {
        [Description("What the element reference resolved to.")]
        public string Target { get; set; }

        [Description("Handle for the target, so a follow-up call need not re-resolve it.")]
        public string TargetHandle { get; set; }

        [Description("The path that was read, echoed back. Null when the target itself was read.")]
        public string Path { get; set; }

        [Description("What kind of thing the path landed on: 'scalar', 'object', 'collection', 'null', or 'redacted'. Branch on this to know whether to look at Value, Properties, or Items.")]
        public string Kind { get; set; }

        [Description("Runtime type of the value, or the declared type when the value is null.")]
        public string ValueType { get; set; }

        [Description("The value for a scalar, or a one-line summary for anything else.")]
        public string Value { get; set; }

        [Description("Handle for the value itself, when it is an object or collection. Use it as 'element' in the next call rather than re-walking the path.")]
        public string Handle { get; set; }

        [Description("One line per property, when the value is an object. One level only: nested values are summarized and given handles rather than expanded.")]
        public List<PropertyLine> Properties { get; set; }

        [Description("How many properties were filtered out as framework-declared. They are still reachable by naming them directly in a path.")]
        public int HiddenPropertyCount { get; set; }

        [Description("A page of items, when the value is a collection.")]
        public List<ItemLine> Items { get; set; }

        [Description("Total items in the collection, or -1 when it could not be counted.")]
        public int ItemCount { get; set; }

        [Description("Where this page started.")]
        public int Offset { get; set; }

        [Description("Whether a limit was reached, so there is more to read.")]
        public bool Truncated { get; set; }

        [Description("What to do next, when the result needs paging or narrowing.")]
        public string Hint { get; set; }
    }

    public class SetResult : DevBridgeResult
    {
        [Description("What the element reference resolved to.")]
        public string Target { get; set; }

        [Description("The property path that was written, echoed back.")]
        public string Property { get; set; }

        [Description("Declared type of the property, which is what the value was converted to.")]
        public string PropertyType { get; set; }

        [Description("The value before the write, so the change can be undone without a separate read.")]
        public string PreviousValue { get; set; }

        [Description("The value after the write, read back from the property rather than echoed from the request. A setter that clamps or ignores the value shows up here.")]
        public string NewValue { get; set; }

        [Description("Whether the value actually changed. False with an 'ok' status means the write was accepted but had no effect, which usually means a binding overwrote it or the setter rejected it silently.")]
        public bool Changed { get; set; }
    }
}
