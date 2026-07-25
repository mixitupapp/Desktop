#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    /// <summary>
    /// One step of a property path: either a named member or an indexer.
    /// </summary>
    internal readonly struct PathStep
    {
        public bool IsIndex { get; }

        public string Text { get; }

        public PathStep(bool isIndex, string text)
        {
            this.IsIndex = isIndex;
            this.Text = text;
        }

        public override string ToString() { return this.IsIndex ? "[" + this.Text + "]" : this.Text; }
    }

    /// <summary>
    /// The result of walking a path, carrying enough context to both read and write the final step.
    /// </summary>
    internal sealed class PathResolution
    {
        public string Status { get; set; } = DevBridgeStatus.Ok;

        public string Message { get; set; }

        public bool IsOk { get { return this.Status == DevBridgeStatus.Ok; } }

        /// <summary>The object holding the final step. Null when the path was empty.</summary>
        public object Owner { get; set; }

        /// <summary>The final step, when there was one.</summary>
        public PathStep FinalStep { get; set; }

        public bool HasFinalStep { get; set; }

        /// <summary>The value the path resolved to.</summary>
        public object Value { get; set; }

        /// <summary>
        /// The declared type of the final member, which is what a write has to coerce to. This is not the
        /// runtime type of <see cref="Value"/>: a property declared as object holding a string must still
        /// accept anything, and a property declared as an enum must not accept an arbitrary int.
        /// </summary>
        public Type DeclaredType { get; set; }

        /// <summary>The reflected final member, when the final step was a name rather than an index.</summary>
        public MemberInfo Member { get; set; }

        public static PathResolution Fail(string status, string message)
        {
            return new PathResolution() { Status = status, Message = message };
        }
    }

    /// <summary>
    /// Parses and walks dotted property paths against plain CLR objects.
    /// </summary>
    /// <remarks>
    /// Plain reflection is enough here because this app's MVVM is plain: view models are assigned
    /// imperatively in code-behind, there is no locator, no dependency injection container and no MVVM
    /// framework interposing itself, so there is nothing to satisfy beyond the CLR.
    /// <para>
    /// Paths cross the boundary between the visual tree and the view models on purpose, because that is
    /// where the answers are. <c>DataContext.Commands[0].Name</c> starts at a control and ends at a model,
    /// and nothing in the traversal needs to know where the boundary was.
    /// </para>
    /// </remarks>
    internal static class PropertyPath
    {
        /// <summary>
        /// How deep a single path may go. A path is written by hand, so anything beyond this is a runaway
        /// rather than a real request.
        /// </summary>
        private const int MaxSteps = 24;

        /// <summary>
        /// How far into a non-indexable sequence an index will be sought. Enumerating to reach an index is
        /// already the slow path, and a bound keeps a lazily-evaluated or infinite sequence from hanging
        /// the UI thread.
        /// </summary>
        private const int MaxEnumerationForIndex = 10000;

        /// <summary>
        /// Splits a path into steps. Returns null and sets <paramref name="error"/> when it is malformed.
        /// </summary>
        public static List<PathStep> Parse(string path, out string error)
        {
            error = null;
            List<PathStep> steps = new List<PathStep>();

            if (string.IsNullOrWhiteSpace(path))
            {
                return steps;
            }

            string text = path.Trim();
            int i = 0;

            // A leading dot is accepted so that both '.Name' and 'Name' work, since a caller composing a
            // path from a prefix will naturally produce the former.
            if (text[i] == '.')
            {
                i++;
            }

            StringBuilder token = new StringBuilder();

            while (i < text.Length)
            {
                char c = text[i];

                if (c == '.')
                {
                    if (token.Length > 0)
                    {
                        steps.Add(new PathStep(false, token.ToString()));
                        token.Clear();
                    }
                    i++;
                    continue;
                }

                if (c == '[')
                {
                    if (token.Length > 0)
                    {
                        steps.Add(new PathStep(false, token.ToString()));
                        token.Clear();
                    }

                    int close = text.IndexOf(']', i);
                    if (close < 0)
                    {
                        error = $"Path '{path}' has an unclosed '[' at position {i}.";
                        return null;
                    }

                    string key = text.Substring(i + 1, close - i - 1).Trim();
                    if (key.Length >= 2 && ((key[0] == '"' && key[key.Length - 1] == '"') || (key[0] == '\'' && key[key.Length - 1] == '\'')))
                    {
                        key = key.Substring(1, key.Length - 2);
                    }

                    if (key.Length == 0)
                    {
                        error = $"Path '{path}' has an empty indexer at position {i}. Pass an integer index like [0] or a key like [\"name\"].";
                        return null;
                    }

                    steps.Add(new PathStep(true, key));
                    i = close + 1;
                    continue;
                }

                token.Append(c);
                i++;
            }

            if (token.Length > 0)
            {
                steps.Add(new PathStep(false, token.ToString()));
            }

            if (steps.Count > MaxSteps)
            {
                error = $"Path '{path}' has {steps.Count} steps, more than the {MaxSteps} allowed.";
                return null;
            }

            return steps;
        }

        /// <summary>
        /// Walks <paramref name="path"/> from <paramref name="root"/>, reading every step but the last and
        /// then resolving the last so that it can be either read or written.
        /// </summary>
        public static PathResolution Resolve(object root, string path)
        {
            List<PathStep> steps = Parse(path, out string parseError);
            if (steps == null)
            {
                return PathResolution.Fail(DevBridgeStatus.NotFound, parseError);
            }

            if (steps.Count == 0)
            {
                return new PathResolution() { Value = root, HasFinalStep = false, DeclaredType = root?.GetType() };
            }

            object current = root;

            // Every step but the last, so the owner of the last is available for a write.
            for (int i = 0; i < steps.Count - 1; i++)
            {
                if (current == null)
                {
                    return PathResolution.Fail(DevBridgeStatus.NotFound, $"'{Describe(steps, i)}' is null, so '{path}' cannot be followed any further.");
                }

                ReadOutcome outcome = ReadStep(current, steps[i]);
                if (!outcome.IsOk)
                {
                    return PathResolution.Fail(outcome.Status, outcome.Message);
                }

                current = outcome.Value;
            }

            if (current == null)
            {
                return PathResolution.Fail(DevBridgeStatus.NotFound, $"'{Describe(steps, steps.Count - 1)}' is null, so '{path}' cannot be followed any further.");
            }

            PathStep finalStep = steps[steps.Count - 1];
            ReadOutcome finalOutcome = ReadStep(current, finalStep);
            if (!finalOutcome.IsOk)
            {
                return PathResolution.Fail(finalOutcome.Status, finalOutcome.Message);
            }

            return new PathResolution()
            {
                Owner = current,
                FinalStep = finalStep,
                HasFinalStep = true,
                Value = finalOutcome.Value,
                DeclaredType = finalOutcome.DeclaredType,
                Member = finalOutcome.Member,
            };
        }

        private readonly struct ReadOutcome
        {
            public string Status { get; }

            public string Message { get; }

            public object Value { get; }

            public Type DeclaredType { get; }

            public MemberInfo Member { get; }

            private ReadOutcome(string status, string message, object value, Type declaredType, MemberInfo member)
            {
                this.Status = status;
                this.Message = message;
                this.Value = value;
                this.DeclaredType = declaredType;
                this.Member = member;
            }

            public bool IsOk { get { return this.Status == DevBridgeStatus.Ok; } }

            public static ReadOutcome Ok(object value, Type declaredType, MemberInfo member)
            {
                return new ReadOutcome(DevBridgeStatus.Ok, null, value, declaredType, member);
            }

            public static ReadOutcome Fail(string status, string message)
            {
                return new ReadOutcome(status, message, null, null, null);
            }
        }

        private static ReadOutcome ReadStep(object owner, PathStep step)
        {
            return step.IsIndex ? ReadIndex(owner, step.Text) : ReadMember(owner, step.Text);
        }

        private static ReadOutcome ReadMember(object owner, string name)
        {
            Type type = owner.GetType();

            MemberInfo member = FindMember(type, name);
            if (member == null)
            {
                return ReadOutcome.Fail(DevBridgeStatus.NotFound, $"'{type.Name}' has no public property or field named '{name}'. Read the object without a path to list what it does have.");
            }

            try
            {
                if (member is PropertyInfo property)
                {
                    if (property.GetMethod == null || !property.GetMethod.IsPublic)
                    {
                        return ReadOutcome.Fail(DevBridgeStatus.NotFound, $"'{type.Name}.{name}' has no public getter.");
                    }
                    return ReadOutcome.Ok(property.GetValue(owner), property.PropertyType, property);
                }

                FieldInfo field = (FieldInfo)member;
                return ReadOutcome.Ok(field.GetValue(owner), field.FieldType, field);
            }
            catch (TargetInvocationException ex)
            {
                // A getter that throws is a real finding rather than a bridge failure, so the inner
                // exception is reported as-is: it is usually the answer to whatever was being diagnosed.
                Exception inner = ex.InnerException ?? ex;
                return ReadOutcome.Fail(DevBridgeStatus.Error, $"Reading '{type.Name}.{name}' threw {inner.GetType().Name}: {inner.Message}");
            }
            catch (Exception ex)
            {
                return ReadOutcome.Fail(DevBridgeStatus.Error, $"Reading '{type.Name}.{name}' threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static ReadOutcome ReadIndex(object owner, string key)
        {
            Type type = owner.GetType();

            // A dictionary is keyed before anything else, because Settings.Commands and Settings.Users are
            // dictionaries keyed by GUID and indexing them by position would be meaningless.
            if (owner is IDictionary dictionary)
            {
                object lookupKey = CoerceDictionaryKey(type, key);
                try
                {
                    if (lookupKey != null && dictionary.Contains(lookupKey))
                    {
                        return ReadOutcome.Ok(dictionary[lookupKey], null, null);
                    }
                }
                catch (Exception ex)
                {
                    return ReadOutcome.Fail(DevBridgeStatus.Error, $"Looking up key '{key}' in {type.Name} threw {ex.GetType().Name}: {ex.Message}");
                }

                return ReadOutcome.Fail(DevBridgeStatus.NotFound, $"{type.Name} has no entry for key '{key}'.");
            }

            bool isInteger = int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index);

            if (isInteger && owner is IList list)
            {
                try
                {
                    if (index < 0 || index >= list.Count)
                    {
                        return ReadOutcome.Fail(DevBridgeStatus.NotFound, $"Index {index} is out of range for {type.Name}, which holds {list.Count} items.");
                    }
                    return ReadOutcome.Ok(list[index], null, null);
                }
                catch (Exception ex)
                {
                    return ReadOutcome.Fail(DevBridgeStatus.Error, $"Indexing {type.Name} threw {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (isInteger && owner is IEnumerable sequence)
            {
                if (index < 0)
                {
                    return ReadOutcome.Fail(DevBridgeStatus.NotFound, $"Index {index} is negative.");
                }

                try
                {
                    int position = 0;
                    foreach (object item in sequence)
                    {
                        if (position == index)
                        {
                            return ReadOutcome.Ok(item, null, null);
                        }
                        if (++position > MaxEnumerationForIndex)
                        {
                            return ReadOutcome.Fail(DevBridgeStatus.NotFound, $"Stopped after enumerating {MaxEnumerationForIndex} items of {type.Name} looking for index {index}.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    return ReadOutcome.Fail(DevBridgeStatus.Error, $"Enumerating {type.Name} threw {ex.GetType().Name}: {ex.Message}");
                }

                return ReadOutcome.Fail(DevBridgeStatus.NotFound, $"Index {index} is out of range for {type.Name}.");
            }

            // A string-keyed indexer on something that is not a dictionary, for instance a custom
            // collection exposing this[string].
            PropertyInfo indexer = FindStringIndexer(type);
            if (indexer != null)
            {
                try
                {
                    return ReadOutcome.Ok(indexer.GetValue(owner, new object[] { key }), indexer.PropertyType, null);
                }
                catch (TargetInvocationException ex)
                {
                    Exception inner = ex.InnerException ?? ex;
                    return ReadOutcome.Fail(DevBridgeStatus.NotFound, $"Indexing {type.Name} with '{key}' threw {inner.GetType().Name}: {inner.Message}");
                }
                catch (Exception ex)
                {
                    return ReadOutcome.Fail(DevBridgeStatus.Error, $"Indexing {type.Name} with '{key}' threw {ex.GetType().Name}: {ex.Message}");
                }
            }

            return ReadOutcome.Fail(DevBridgeStatus.NotFound, isInteger
                ? $"{type.Name} is not a list or sequence, so it cannot be indexed by position."
                : $"{type.Name} has no indexer taking a string, so '[{key}]' cannot be resolved. Use an integer index for a list.");
        }

        /// <summary>
        /// Writes the final step of an already-resolved path.
        /// </summary>
        public static PathResolution Write(PathResolution resolution, object value)
        {
            if (!resolution.HasFinalStep)
            {
                return PathResolution.Fail(DevBridgeStatus.NotSettable, "No property was named, so there is nothing to write. Pass the property to set.");
            }

            object owner = resolution.Owner;
            PathStep step = resolution.FinalStep;

            try
            {
                if (step.IsIndex)
                {
                    if (owner is IDictionary dictionary)
                    {
                        object key = CoerceDictionaryKey(owner.GetType(), step.Text);
                        if (key == null)
                        {
                            return PathResolution.Fail(DevBridgeStatus.CoercionFailed, $"'{step.Text}' cannot be converted to the key type of {owner.GetType().Name}.");
                        }
                        dictionary[key] = value;
                        return resolution;
                    }

                    if (owner is IList list && int.TryParse(step.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                    {
                        if (index < 0 || index >= list.Count)
                        {
                            return PathResolution.Fail(DevBridgeStatus.NotFound, $"Index {index} is out of range for {owner.GetType().Name}, which holds {list.Count} items.");
                        }
                        list[index] = value;
                        return resolution;
                    }

                    return PathResolution.Fail(DevBridgeStatus.NotSettable, $"{owner.GetType().Name} does not support writing through an indexer.");
                }

                if (resolution.Member is PropertyInfo property)
                {
                    if (property.SetMethod == null || !property.SetMethod.IsPublic)
                    {
                        return PathResolution.Fail(DevBridgeStatus.NotSettable, $"'{property.DeclaringType.Name}.{property.Name}' is read-only: it has no public setter. Many view model properties are computed from others, so look for the property it derives from and set that instead.");
                    }
                    property.SetValue(owner, value);
                    return resolution;
                }

                if (resolution.Member is FieldInfo field)
                {
                    if (field.IsInitOnly || field.IsLiteral)
                    {
                        return PathResolution.Fail(DevBridgeStatus.NotSettable, $"'{field.DeclaringType.Name}.{field.Name}' is a read-only field.");
                    }
                    field.SetValue(owner, value);
                    return resolution;
                }

                return PathResolution.Fail(DevBridgeStatus.NotSettable, $"'{step}' is not a writable member.");
            }
            catch (TargetInvocationException ex)
            {
                // A setter that throws is frequently the point: a validating setter rejecting a value is
                // the app telling the caller why, so the inner exception is surfaced verbatim.
                Exception inner = ex.InnerException ?? ex;
                return PathResolution.Fail(DevBridgeStatus.Error, $"The setter threw {inner.GetType().Name}: {inner.Message}");
            }
            catch (Exception ex)
            {
                return PathResolution.Fail(DevBridgeStatus.Error, $"Writing '{step}' threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts the text a caller supplied into the type the target actually expects.
        /// </summary>
        /// <remarks>
        /// Values arrive as text because that is what survives a JSON tool call unambiguously, so
        /// everything a write needs to know about the type comes from reflection rather than from the
        /// caller. Enums accept both their name and their numeric value, since a caller reading a dump
        /// sees the name while one reading a raw property sees the number.
        /// </remarks>
        public static bool TryCoerce(string text, Type targetType, bool isNull, out object value, out string error)
        {
            value = null;
            error = null;

            if (targetType == null)
            {
                // Nothing declared the type, for instance an element of a non-generic list. Text is the
                // only safe assumption.
                value = isNull ? null : text;
                return true;
            }

            Type underlying = Nullable.GetUnderlyingType(targetType);
            bool isNullable = underlying != null;
            Type effective = underlying ?? targetType;

            if (isNull)
            {
                if (isNullable || !effective.IsValueType)
                {
                    value = null;
                    return true;
                }
                error = $"'{targetType.Name}' is a non-nullable value type, so it cannot be set to null.";
                return false;
            }

            if (text == null)
            {
                if (isNullable || !effective.IsValueType)
                {
                    value = null;
                    return true;
                }
                error = $"No value was supplied and '{targetType.Name}' cannot be null.";
                return false;
            }

            if (effective == typeof(string))
            {
                value = text;
                return true;
            }

            // An empty string means null for anything that can hold it, which is what a caller clearing a
            // field expects. Without this, clearing a nullable int would need the explicit null flag.
            if (text.Length == 0 && (isNullable || !effective.IsValueType))
            {
                value = null;
                return true;
            }

            try
            {
                if (effective.IsEnum)
                {
                    if (Enum.TryParse(effective, text, ignoreCase: true, out object parsed))
                    {
                        value = parsed;
                        return true;
                    }
                    error = $"'{text}' is not a valid {effective.Name}. Valid values are: {string.Join(", ", Enum.GetNames(effective))}.";
                    return false;
                }

                if (effective == typeof(bool))
                {
                    // The JSON-ish and numeric spellings a caller might reasonably send, on top of
                    // bool.Parse, which only accepts "true" and "false".
                    switch (text.Trim().ToLowerInvariant())
                    {
                        case "true": case "1": case "yes": case "on": value = true; return true;
                        case "false": case "0": case "no": case "off": value = false; return true;
                        default:
                            error = $"'{text}' is not a boolean. Pass true or false.";
                            return false;
                    }
                }

                if (effective == typeof(Guid))
                {
                    if (Guid.TryParse(text, out Guid guid))
                    {
                        value = guid;
                        return true;
                    }
                    error = $"'{text}' is not a valid GUID.";
                    return false;
                }

                if (effective == typeof(Uri))
                {
                    if (Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out Uri uri))
                    {
                        value = uri;
                        return true;
                    }
                    error = $"'{text}' is not a valid URI.";
                    return false;
                }

                if (effective == typeof(TimeSpan))
                {
                    if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out TimeSpan span))
                    {
                        value = span;
                        return true;
                    }
                    error = $"'{text}' is not a valid TimeSpan.";
                    return false;
                }

                if (effective == typeof(object))
                {
                    // Nothing to convert to. Text preserves what the caller sent.
                    value = text;
                    return true;
                }

                if (typeof(IConvertible).IsAssignableFrom(effective))
                {
                    value = Convert.ChangeType(text, effective, CultureInfo.InvariantCulture);
                    return true;
                }

                // The remaining useful case is a type with its own TypeConverter, which is how most WPF
                // types such as Visibility, Thickness and Brush are written in XAML. Reusing the same
                // converter means a value can be set here exactly as it would be spelled in markup.
                TypeConverter converter = TypeDescriptor.GetConverter(effective);
                if (converter != null && converter.CanConvertFrom(typeof(string)))
                {
                    value = converter.ConvertFromInvariantString(text);
                    return true;
                }

                error = $"There is no conversion from text to '{effective.Name}'. Only primitives, strings, enums, GUIDs, URIs, TimeSpans and types with a TypeConverter can be set this way.";
                return false;
            }
            catch (Exception ex)
            {
                error = $"'{text}' could not be converted to {effective.Name}: {ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Finds a public instance property or field, preferring an exact match and falling back to a
        /// case-insensitive one so a caller need not guess casing.
        /// </summary>
        public static MemberInfo FindMember(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

            PropertyInfo property = GetPropertySafely(type, name, flags);
            if (property != null)
            {
                return property;
            }

            FieldInfo field = type.GetField(name, flags);
            if (field != null)
            {
                return field;
            }

            foreach (PropertyInfo candidate in type.GetProperties(flags))
            {
                if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            foreach (FieldInfo candidate in type.GetFields(flags))
            {
                if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// GetProperty throws when a derived type shadows a base property with 'new'. Walking the
        /// hierarchy explicitly resolves that to the most derived declaration, which is what a caller
        /// naming the property on the runtime type means.
        /// </summary>
        private static PropertyInfo GetPropertySafely(Type type, string name, BindingFlags flags)
        {
            try
            {
                return type.GetProperty(name, flags);
            }
            catch (AmbiguousMatchException)
            {
                for (Type current = type; current != null; current = current.BaseType)
                {
                    PropertyInfo declared = current.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                    if (declared != null)
                    {
                        return declared;
                    }
                }
                return null;
            }
        }

        private static PropertyInfo FindStringIndexer(Type type)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                ParameterInfo[] parameters = property.GetIndexParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                {
                    return property;
                }
            }
            return null;
        }

        /// <summary>
        /// Converts an indexer key into whatever the dictionary is keyed by. GUID keys are the case that
        /// matters, because Settings.Commands and Settings.Users are both keyed that way.
        /// </summary>
        private static object CoerceDictionaryKey(Type dictionaryType, string key)
        {
            Type keyType = typeof(object);

            foreach (Type contract in dictionaryType.GetInterfaces())
            {
                if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                {
                    keyType = contract.GetGenericArguments()[0];
                    break;
                }
            }

            if (keyType == typeof(object) || keyType == typeof(string))
            {
                return key;
            }

            return TryCoerce(key, keyType, isNull: false, out object coerced, out _) ? coerced : null;
        }

        /// <summary>
        /// The path up to and including a given step, for an error message that says where the walk
        /// stopped rather than only that it did.
        /// </summary>
        private static string Describe(List<PathStep> steps, int throughIndex)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i <= throughIndex && i < steps.Count; i++)
            {
                if (!steps[i].IsIndex && builder.Length > 0)
                {
                    builder.Append('.');
                }
                builder.Append(steps[i].ToString());
            }
            return builder.Length > 0 ? builder.ToString() : "the target";
        }
    }
}
