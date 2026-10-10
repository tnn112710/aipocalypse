using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace TextStudio
{
    internal static class TextStudioEffectParameterUtility
    {
        static readonly string[] s_fromColorAliases = { "f" };
        static readonly string[] s_toColorAliases = { "t" };
        static readonly string[] s_speedAliases = { "s" };

        internal static bool HasAny(in TextStudioTagParameters parameters)
        {
            return parameters.Raw.Length > 0 ||
                   parameters.PositionalCount > 0 ||
                   parameters.NamedCount > 0;
        }

        internal static TextStudioTagParameters CreateCanonicalParameters(
            IReadOnlyList<string> positional,
            IReadOnlyList<TextStudioNamedTagParameter> named)
        {
            string[] positionalArray = CopyPositionals(positional);
            TextStudioNamedTagParameter[] namedArray = CopyNamed(named);
            if (positionalArray.Length == 0 && namedArray.Length == 0)
                return TextStudioTagParameters.Empty;

            string raw = BuildRawParameterString(positionalArray, namedArray);
            return new TextStudioTagParameters(raw, positionalArray, namedArray);
        }

        static string[] CopyPositionals(IReadOnlyList<string> positional)
        {
            if (positional == null || positional.Count == 0)
                return Array.Empty<string>();

            string[] result = new string[positional.Count];
            for (int i = 0; i < positional.Count; i++)
                result[i] = positional[i] ?? string.Empty;
            return result;
        }

        static TextStudioNamedTagParameter[] CopyNamed(IReadOnlyList<TextStudioNamedTagParameter> named)
        {
            if (named == null || named.Count == 0)
                return Array.Empty<TextStudioNamedTagParameter>();

            TextStudioNamedTagParameter[] result = new TextStudioNamedTagParameter[named.Count];
            for (int i = 0; i < named.Count; i++)
                result[i] = named[i];
            return result;
        }

        internal static string BuildRawParameterString(
            IReadOnlyList<string> positional,
            IReadOnlyList<TextStudioNamedTagParameter> named)
        {
            StringBuilder builder = new StringBuilder(64);
            for (int i = 0; positional != null && i < positional.Count; i++)
            {
                if (builder.Length > 0)
                    builder.Append(',');
                AppendParameterValue(builder, positional[i]);
            }

            for (int i = 0; named != null && i < named.Count; i++)
            {
                TextStudioNamedTagParameter parameter = named[i];
                if (string.IsNullOrEmpty(parameter.Name))
                    continue;

                if (builder.Length > 0)
                    builder.Append(' ');
                builder.Append(parameter.Name);
                if (parameter.IsFlag)
                    continue;

                builder.Append(parameter.Operation == TextStudioTagParameterOperation.Multiply ? '*' : '=');
                AppendParameterValue(builder, parameter.Value);
            }

            return builder.ToString();
        }

        static void AppendParameterValue(StringBuilder builder, string value)
        {
            value ??= string.Empty;
            bool needsQuotes = value.Length == 0;
            for (int i = 0; i < value.Length && !needsQuotes; i++)
            {
                char c = value[i];
                needsQuotes = char.IsWhiteSpace(c) || c == ',' || c == '"' || c == '\'';
            }

            if (!needsQuotes)
            {
                builder.Append(value);
                return;
            }

            builder.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '"')
                    builder.Append('\'');
                else
                    builder.Append(value[i]);
            }
            builder.Append('"');
        }

        internal static bool TryGetColor(
            in TextStudioTagParameters parameters,
            int positionalIndex,
            string name,
            out Color32 value)
        {
            if (TryGetColorByName(in parameters, name, out value))
                return true;

            return parameters.TryGetColor(positionalIndex, out value);
        }

        internal static bool TryGetFloat(
            in TextStudioTagParameters parameters,
            int positionalIndex,
            string name,
            out float value)
        {
            if (TryGetFloatByName(in parameters, name, out value))
                return true;

            return parameters.TryGetFloat(positionalIndex, out value);
        }

        internal static bool TryParseColor(string raw, out Color32 value)
        {
            return TextStudioColorUtility.TryParseColor(raw, out value);
        }

        static bool TryGetFloatByName(
            in TextStudioTagParameters parameters,
            string name,
            out float value)
        {
            return TryGetFloatByName(in parameters, name, GetSharedFloatAliases(name), out value);
        }

        static bool TryGetFloatByName(
            in TextStudioTagParameters parameters,
            string name,
            string[] aliases,
            out float value)
        {
            value = 0f;
            return TryGetNumericParameterByName(
                       in parameters,
                       name,
                       aliases,
                       out value,
                       out TextStudioTagParameterOperation operation) &&
                   operation == TextStudioTagParameterOperation.Set;
        }

        static bool TryGetNumericParameterByName(
            in TextStudioTagParameters parameters,
            string name,
            string[] aliases,
            out float value,
            out TextStudioTagParameterOperation operation)
        {
            value = 0f;
            operation = TextStudioTagParameterOperation.Set;
            if (!TryGetParameterByName(in parameters, name, aliases, out TextStudioNamedTagParameter parameter) ||
                parameter.IsFlag ||
                !float.TryParse(parameter.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                float.IsNaN(value) ||
                float.IsInfinity(value))
            {
                return false;
            }

            operation = parameter.Operation;
            return true;
        }

        static bool TryGetBoolByName(
            in TextStudioTagParameters parameters,
            string name,
            string[] aliases,
            out bool value)
        {
            value = false;
            if (!TryGetParameterByName(in parameters, name, aliases, out TextStudioNamedTagParameter parameter))
                return false;
            if (parameter.IsFlag)
            {
                value = true;
                return true;
            }
            if (parameter.Operation != TextStudioTagParameterOperation.Set)
                return false;
            if (bool.TryParse(parameter.Value, out value))
                return true;
            if (string.Equals(parameter.Value, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parameter.Value, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parameter.Value, "on", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }
            if (string.Equals(parameter.Value, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parameter.Value, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parameter.Value, "off", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            return false;
        }

        static bool TryGetColorByName(
            in TextStudioTagParameters parameters,
            string name,
            out Color32 value)
        {
            return TryGetColorByName(in parameters, name, GetSharedColorAliases(name), out value);
        }

        static bool TryGetColorByName(
            in TextStudioTagParameters parameters,
            string name,
            string[] aliases,
            out Color32 value)
        {
            value = default;
            return TryGetParameterByName(in parameters, name, aliases, out TextStudioNamedTagParameter parameter) &&
                   !parameter.IsFlag &&
                   parameter.Operation == TextStudioTagParameterOperation.Set &&
                   parameters.TryGetColor(parameter.Name, out value);
        }

        static bool TryGetStringByName(
            in TextStudioTagParameters parameters,
            string name,
            string[] aliases,
            out string value)
        {
            value = string.Empty;
            if (!TryGetParameterByName(in parameters, name, aliases, out TextStudioNamedTagParameter parameter) ||
                parameter.IsFlag ||
                parameter.Operation != TextStudioTagParameterOperation.Set)
            {
                return false;
            }

            value = parameter.Value;
            return true;
        }

        static bool TryGetParameterByName(
            in TextStudioTagParameters parameters,
            string name,
            string[] aliases,
            out TextStudioNamedTagParameter parameter)
        {
            IReadOnlyList<TextStudioNamedTagParameter> named = parameters.Named;
            for (int i = named.Count - 1; i >= 0; i--)
            {
                TextStudioNamedTagParameter candidate = named[i];
                if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    parameter = candidate;
                    return true;
                }

                for (int aliasIndex = 0; aliases != null && aliasIndex < aliases.Length; aliasIndex++)
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

        static string[] GetSharedColorAliases(string canonicalName)
        {
            if (string.Equals(canonicalName, "from", StringComparison.OrdinalIgnoreCase))
                return s_fromColorAliases;
            if (string.Equals(canonicalName, "to", StringComparison.OrdinalIgnoreCase))
                return s_toColorAliases;
            return Array.Empty<string>();
        }

        static string[] GetSharedFloatAliases(string canonicalName)
        {
            if (string.Equals(canonicalName, "speed", StringComparison.OrdinalIgnoreCase))
                return s_speedAliases;
            return Array.Empty<string>();
        }
    }
}
