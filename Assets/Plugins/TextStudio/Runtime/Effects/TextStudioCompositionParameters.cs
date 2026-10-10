using System;
using System.Globalization;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Serialized snapshot of one published Existing Effect layer parameter.</summary>
    [Serializable]
    internal sealed class TextStudioCompositionParameterEntry
    {
        [SerializeField] string _name = string.Empty;
        [SerializeField] string _value = string.Empty;
        [SerializeField] TextStudioTagParameterOperation _operation = TextStudioTagParameterOperation.Set;
        [SerializeField] string _baseline = string.Empty;

        internal string Name => _name ?? string.Empty;
        internal string Value => _value ?? string.Empty;
        internal TextStudioTagParameterOperation Operation => _operation;
        internal string Baseline => _baseline ?? string.Empty;

        internal TextStudioCompositionParameterEntry(
            string name,
            string value,
            TextStudioTagParameterOperation operation,
            string baseline)
        {
            _name = name ?? string.Empty;
            _value = value ?? string.Empty;
            _operation = operation;
            _baseline = baseline ?? string.Empty;
        }

        internal static TextStudioCompositionParameterEntry FromDefault(
            TextStudioTagParameterDefinition definition)
        {
            string value = definition != null ? definition.DefaultValue : string.Empty;
            return new TextStudioCompositionParameterEntry(
                definition != null ? definition.Name : string.Empty,
                value,
                TextStudioTagParameterOperation.Set,
                value);
        }

        internal void ResetTo(string value)
        {
            _value = value ?? string.Empty;
            _baseline = _value;
            _operation = TextStudioTagParameterOperation.Set;
        }

        internal void SetAuthored(
            string value,
            TextStudioTagParameterOperation operation,
            string baseline)
        {
            _value = value ?? string.Empty;
            _operation = operation;
            _baseline = baseline ?? string.Empty;
        }

        internal void CanonicalizeName(string name)
        {
            _name = name ?? string.Empty;
        }

        internal bool TryCreateRuntimeParameter(
            TextStudioTagParameterDefinition definition,
            out TextStudioNamedTagParameter parameter,
            out string error)
        {
            parameter = default;
            error = string.Empty;
            if (definition == null || string.IsNullOrWhiteSpace(Name))
            {
                error = "The parameter has no published definition.";
                return false;
            }
            if (definition.Required && string.IsNullOrWhiteSpace(Value))
            {
                error = "Parameter '" + definition.DisplayName + "' requires a value.";
                return false;
            }

            string runtimeValue = Value;
            if (_operation == TextStudioTagParameterOperation.Multiply)
            {
                if (!IsNumeric(definition.Kind) ||
                    !TryParseFinite(Baseline, out float baseline) ||
                    Mathf.Approximately(baseline, 0f) ||
                    !TryParseFinite(Value, out float factor))
                {
                    error = "Parameter '" + definition.DisplayName +
                        "' cannot multiply because its authored baseline is missing, zero, or non-numeric.";
                    return false;
                }

                float resolved = baseline * factor;
                if (float.IsNaN(resolved) || float.IsInfinity(resolved))
                {
                    error = "Parameter '" + definition.DisplayName + "' resolves to a non-finite value.";
                    return false;
                }
                runtimeValue = definition.Kind == TextStudioTagParameterKind.Integer
                    ? Mathf.RoundToInt(resolved).ToString(CultureInfo.InvariantCulture)
                    : resolved.ToString("R", CultureInfo.InvariantCulture);
            }

            parameter = new TextStudioNamedTagParameter(
                definition.Name,
                runtimeValue,
                false,
                TextStudioTagParameterOperation.Set);
            return true;
        }

        internal string GetAuthoredParameterSource()
        {
            TextStudioNamedTagParameter parameter = new TextStudioNamedTagParameter(
                Name,
                Value,
                false,
                Operation);
            return TextStudioEffectParameterUtility.BuildRawParameterString(
                Array.Empty<string>(),
                new[] { parameter });
        }

        internal static bool IsNumeric(TextStudioTagParameterKind kind)
        {
            return kind == TextStudioTagParameterKind.Float ||
                   kind == TextStudioTagParameterKind.Integer;
        }

        internal static bool TryParseFinite(string value, out float parsed)
        {
            return float.TryParse(
                       value,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out parsed) &&
                   !float.IsNaN(parsed) &&
                   !float.IsInfinity(parsed);
        }
    }
}
