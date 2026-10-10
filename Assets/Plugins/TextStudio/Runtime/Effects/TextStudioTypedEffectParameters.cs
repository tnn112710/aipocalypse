using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace TextStudio
{
#if UNITY_6000_7_OR_NEWER
    // Reflected field metadata is immutable for the lifetime of the loaded generic type.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioTypedEffectParameters<TParameters>
        where TParameters : struct
    {
        static readonly FieldInfo[] s_fields = GetPublishedFields();

        internal static TextStudioTagParameterDefinition[] CreateDefinitions(
            in TParameters defaults,
            out string error)
        {
            object boxedDefaults = defaults;
            List<TextStudioTagParameterDefinition> definitions =
                new List<TextStudioTagParameterDefinition>(s_fields.Length);
            List<string> unsupported = null;
            for (int i = 0; i < s_fields.Length; i++)
            {
                FieldInfo field = s_fields[i];
                if (!TryResolveKind(field.FieldType, out TextStudioTagParameterKind kind))
                {
                    unsupported ??= new List<string>();
                    unsupported.Add(field.Name + " (" + field.FieldType.Name + ")");
                    continue;
                }

                object defaultValue = field.GetValue(boxedDefaults);
                TextStudioRangeAttribute range = field.GetCustomAttribute<TextStudioRangeAttribute>();
                TooltipAttribute tooltip = field.GetCustomAttribute<TooltipAttribute>();
                string[] aliases = GetAliases(field);
                string[] allowedValues = field.FieldType.IsEnum
                    ? Enum.GetNames(field.FieldType)
                    : null;
                definitions.Add(new TextStudioTagParameterDefinition(
                    field.Name,
                    kind,
                    displayName: Nicify(field.Name),
                    description: tooltip != null ? tooltip.tooltip : string.Empty,
                    allowedValues: allowedValues,
                    aliases: aliases,
                    defaultValue: FormatValue(defaultValue, field.FieldType),
                    minimum: range != null ? range.Min : (float?)null,
                    maximum: range != null ? range.Max : (float?)null,
                    snap: range != null ? range.Snap : 0f,
                    suffix: range != null ? range.Suffix : null,
                    allowOverflow: range != null && range.AllowOverflow));
            }

            error = unsupported == null
                ? string.Empty
                : "Unsupported published parameter fields: " + string.Join(", ", unsupported) + ".";
            return definitions.ToArray();
        }

        internal static bool TryBind(
            ref TParameters target,
            in TextStudioTagParameters parameters,
            out string error)
        {
            error = string.Empty;
            if (!TextStudioEffectParameterUtility.HasAny(in parameters))
                return true;

            object boxed = target;
            IReadOnlyList<TextStudioNamedTagParameter> named = parameters.Named;
            for (int fieldIndex = 0; fieldIndex < s_fields.Length; fieldIndex++)
            {
                FieldInfo field = s_fields[fieldIndex];
                if (!TryResolveKind(field.FieldType, out _))
                    continue;

                string[] aliases = GetAliases(field);
                if (!TryFindParameter(named, field.Name, aliases, out TextStudioNamedTagParameter parameter))
                    continue;

                object baseline = field.GetValue(boxed);
                TextStudioRangeAttribute range = field.GetCustomAttribute<TextStudioRangeAttribute>();
                if (!TryParseValue(
                        field.FieldType,
                        baseline,
                        in parameter,
                        range,
                        out object parsed,
                        out error))
                {
                    error = "Parameter '" + field.Name + "': " + error;
                    return false;
                }

                field.SetValue(boxed, parsed);
            }

            target = (TParameters)boxed;
            return true;
        }

        static FieldInfo[] GetPublishedFields()
        {
            FieldInfo[] all = typeof(TParameters).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            List<FieldInfo> fields = new List<FieldInfo>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                FieldInfo field = all[i];
                if (field.IsStatic || field.IsNotSerialized)
                    continue;
                bool serialized = field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
                if (!serialized || field.GetCustomAttribute<HideInInspector>() != null)
                    continue;
                fields.Add(field);
            }

            fields.Sort((left, right) => left.MetadataToken.CompareTo(right.MetadataToken));
            return fields.ToArray();
        }

        static bool TryResolveKind(Type type, out TextStudioTagParameterKind kind)
        {
            if (type == typeof(float)) kind = TextStudioTagParameterKind.Float;
            else if (type == typeof(int)) kind = TextStudioTagParameterKind.Integer;
            else if (type == typeof(bool)) kind = TextStudioTagParameterKind.Boolean;
            else if (type == typeof(string)) kind = TextStudioTagParameterKind.String;
            else if (type.IsEnum) kind = TextStudioTagParameterKind.Enum;
            else if (type == typeof(Color) || type == typeof(Color32)) kind = TextStudioTagParameterKind.Color;
            else if (type == typeof(Vector2)) kind = TextStudioTagParameterKind.Vector2;
            else if (type == typeof(Vector3)) kind = TextStudioTagParameterKind.Vector3;
            else
            {
                kind = default;
                return false;
            }
            return true;
        }

        static bool TryFindParameter(
            IReadOnlyList<TextStudioNamedTagParameter> parameters,
            string name,
            IReadOnlyList<string> aliases,
            out TextStudioNamedTagParameter parameter)
        {
            for (int i = parameters.Count - 1; i >= 0; i--)
            {
                TextStudioNamedTagParameter candidate = parameters[i];
                if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    parameter = candidate;
                    return true;
                }

                for (int aliasIndex = 0; aliases != null && aliasIndex < aliases.Count; aliasIndex++)
                {
                    if (!string.Equals(candidate.Name, aliases[aliasIndex], StringComparison.OrdinalIgnoreCase))
                        continue;
                    parameter = candidate;
                    return true;
                }
            }

            parameter = default;
            return false;
        }

        static bool TryParseValue(
            Type type,
            object baseline,
            in TextStudioNamedTagParameter parameter,
            TextStudioRangeAttribute range,
            out object value,
            out string error)
        {
            value = null;
            error = string.Empty;
            if (type == typeof(float))
            {
                if (!TryParseFiniteFloat(parameter.Value, out float operand))
                    return Fail("expected a finite number.", out error);
                float resolved = parameter.Operation == TextStudioTagParameterOperation.Multiply
                    ? (float)baseline * operand
                    : operand;
                if (!TryApplyRange(resolved, range, out resolved))
                    return Fail("resolved to a non-finite number.", out error);
                value = resolved;
                return true;
            }

            if (type == typeof(int))
            {
                float resolved;
                if (parameter.Operation == TextStudioTagParameterOperation.Set &&
                    int.TryParse(parameter.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
                {
                    resolved = integer;
                }
                else if (TryParseFiniteFloat(parameter.Value, out float operand))
                {
                    resolved = parameter.Operation == TextStudioTagParameterOperation.Multiply
                        ? (int)baseline * operand
                        : operand;
                }
                else
                {
                    return Fail("expected an integer or finite numeric modifier.", out error);
                }

                if (!TryApplyRange(resolved, range, out resolved))
                    return Fail("resolved to a non-finite number.", out error);
                value = Mathf.RoundToInt(resolved);
                return true;
            }

            if (parameter.Operation == TextStudioTagParameterOperation.Multiply)
                return Fail("Multiply is supported only for numeric parameters.", out error);

            if (type == typeof(bool))
            {
                if (parameter.IsFlag)
                {
                    value = true;
                    return true;
                }
                if (TryParseBoolean(parameter.Value, out bool boolean))
                {
                    value = boolean;
                    return true;
                }
                return Fail("expected true or false.", out error);
            }

            if (parameter.IsFlag)
                return Fail("expected an assigned value.", out error);
            if (type == typeof(string))
            {
                value = parameter.Value ?? string.Empty;
                return true;
            }
            if (type.IsEnum)
            {
                if (Enum.TryParse(type, parameter.Value, true, out object enumValue))
                {
                    value = enumValue;
                    return true;
                }
                return Fail("expected one of " + string.Join(", ", Enum.GetNames(type)) + ".", out error);
            }
            if (type == typeof(Color) || type == typeof(Color32))
            {
                if (!ColorUtility.TryParseHtmlString(parameter.Value, out Color color))
                    return Fail("expected an HTML color such as #ffffffff.", out error);
                value = type == typeof(Color32) ? (object)(Color32)color : color;
                return true;
            }
            if (type == typeof(Vector2))
            {
                if (!TryParseVector(parameter.Value, 2, out Vector3 vector))
                    return Fail("expected two invariant components, for example 0,1.", out error);
                value = new Vector2(vector.x, vector.y);
                return true;
            }
            if (type == typeof(Vector3))
            {
                if (!TryParseVector(parameter.Value, 3, out Vector3 vector))
                    return Fail("expected three invariant components, for example 0,1,0.", out error);
                value = vector;
                return true;
            }

            return Fail("uses an unsupported type.", out error);
        }

        static bool TryApplyRange(float input, TextStudioRangeAttribute range, out float value)
        {
            value = input;
            if (float.IsNaN(value) || float.IsInfinity(value))
                return false;
            if (range == null)
                return true;
            if (!range.AllowOverflow)
                value = Mathf.Clamp(value, range.Min, range.Max);
            if (range.Snap > 0f)
            {
                float origin = range.AllowOverflow ? 0f : range.Min;
                value = origin + Mathf.Round((value - origin) / range.Snap) * range.Snap;
                if (!range.AllowOverflow)
                    value = Mathf.Clamp(value, range.Min, range.Max);
            }
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static bool TryParseFiniteFloat(string raw, out float value)
        {
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                   !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

        static bool TryParseBoolean(string raw, out bool value)
        {
            if (bool.TryParse(raw, out value))
                return true;
            if (string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }
            if (string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }
            value = false;
            return false;
        }

        static bool TryParseVector(string raw, int dimensions, out Vector3 value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            string[] parts = raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != dimensions ||
                !TryParseFiniteFloat(parts[0], out float x) ||
                !TryParseFiniteFloat(parts[1], out float y))
            {
                return false;
            }
            float z = 0f;
            if (dimensions == 3 && !TryParseFiniteFloat(parts[2], out z))
                return false;
            value = new Vector3(x, y, z);
            return true;
        }

        static string FormatValue(object value, Type type)
        {
            if (type == typeof(float))
                return ((float)value).ToString("R", CultureInfo.InvariantCulture);
            if (type == typeof(int))
                return ((int)value).ToString(CultureInfo.InvariantCulture);
            if (type == typeof(bool))
                return (bool)value ? "true" : "false";
            if (type == typeof(string))
                return value as string ?? string.Empty;
            if (type.IsEnum)
                return value != null ? value.ToString() : string.Empty;
            if (type == typeof(Color))
                return "#" + ColorUtility.ToHtmlStringRGBA((Color)value);
            if (type == typeof(Color32))
                return "#" + ColorUtility.ToHtmlStringRGBA((Color32)value);
            if (type == typeof(Vector2))
            {
                Vector2 vector = (Vector2)value;
                return FormatFloat(vector.x) + "," + FormatFloat(vector.y);
            }
            if (type == typeof(Vector3))
            {
                Vector3 vector = (Vector3)value;
                return FormatFloat(vector.x) + "," + FormatFloat(vector.y) + "," + FormatFloat(vector.z);
            }
            return string.Empty;
        }

        static string FormatFloat(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        static string[] GetAliases(FieldInfo field)
        {
            object[] attributes = field.GetCustomAttributes(typeof(TextStudioParameterAliasAttribute), true);
            if (attributes == null || attributes.Length == 0)
                return null;
            List<string> aliases = new List<string>(attributes.Length);
            for (int i = 0; i < attributes.Length; i++)
            {
                string alias = ((TextStudioParameterAliasAttribute)attributes[i]).Alias;
                if (!string.IsNullOrWhiteSpace(alias))
                    aliases.Add(alias.Trim());
            }
            return aliases.Count > 0 ? aliases.ToArray() : null;
        }

        static string Nicify(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1]))
                    builder.Append(' ');
                builder.Append(i == 0 ? char.ToUpperInvariant(c) : c);
            }
            return builder.ToString();
        }

        static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
