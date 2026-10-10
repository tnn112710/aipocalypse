using System;
using System.Globalization;
using System.Text;
using TextStudio.BuiltIns;
using UnityEngine;

namespace TextStudio.Editor
{
    internal sealed class TextStudioGuidedTagDraft
    {
        readonly TextStudioTagDefinition _definition;
        readonly string[] _values;
        readonly bool[] _included;

        public TextStudioGuidedTagDraft(TextStudioTagDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _values = new string[definition.Parameters.Count];
            _included = new bool[definition.Parameters.Count];
            InitializeFromExample();
            ApplyRecipeDefaults();
        }

        public TextStudioTagDefinition Definition => _definition;
        public int ParameterCount => _values.Length;

        public static bool CanConfigure(TextStudioTagDefinition definition)
        {
            if (definition == null || definition.Parameters.Count == 0)
                return false;

            if (definition.SemanticCategory == TextStudioTagCategory.Cue)
                return true;

            return definition.SemanticCategory == TextStudioTagCategory.Effect &&
                   BuiltInStyles.DefaultTagMap.ContainsKey(definition.Id);
        }

        public TextStudioTagParameterDefinition GetParameter(int index)
        {
            return _definition.Parameters[index];
        }

        public string GetValue(int index)
        {
            return index >= 0 && index < _values.Length ? _values[index] : string.Empty;
        }

        public bool IsIncluded(int index)
        {
            return index >= 0 && index < _included.Length && _included[index];
        }

        public void SetValue(int index, string value)
        {
            if (index < 0 || index >= _values.Length)
                return;

            _values[index] = value ?? string.Empty;
            if (!string.IsNullOrEmpty(_values[index]))
                _included[index] = true;
        }

        public void SetIncluded(int index, bool included)
        {
            if (index < 0 || index >= _included.Length)
                return;

            TextStudioTagParameterDefinition parameter = _definition.Parameters[index];
            _included[index] = parameter.Required || included;
        }

        public bool TryBuildSnippet(out string snippet, out string error)
        {
            snippet = string.Empty;
            error = string.Empty;

            StringBuilder builder = new StringBuilder(64);
            builder.Append('<');
            builder.Append(_definition.Id);

            for (int i = 0; i < _definition.Parameters.Count; i++)
            {
                TextStudioTagParameterDefinition parameter = _definition.Parameters[i];
                if (!_included[i] && !parameter.Required)
                    continue;

                string value = (_values[i] ?? string.Empty).Trim();
                if (!TryValidateValue(parameter, value, out error))
                    return false;

                builder.Append(' ');
                builder.Append(parameter.Name);
                builder.Append('=');
                AppendValue(builder, value);
            }

            builder.Append('>');
            if (_definition.Shape == TextStudioTagShape.Range)
            {
                builder.Append("Text</");
                builder.Append(_definition.Id);
                builder.Append('>');
            }

            snippet = builder.ToString();
            return true;
        }

        public bool IsPreviewSafe()
        {
            if (_definition.SemanticCategory == TextStudioTagCategory.Effect)
                return true;

            return TextStudioActions.TryGet(_definition.Id, out ITextStudioAction action) &&
                   action is ITextStudioEditModePreviewAction;
        }

        void InitializeFromExample()
        {
            if (_definition.Examples.Count == 0)
            {
                EnsureRequiredParametersIncluded();
                return;
            }

            string example = _definition.Examples[0] ?? string.Empty;
            int openingEnd = example.IndexOf('>');
            if (openingEnd <= 1)
            {
                EnsureRequiredParametersIncluded();
                return;
            }

            string opening = example.Substring(1, openingEnd - 1).Trim();
            if (!opening.StartsWith(_definition.Id, StringComparison.OrdinalIgnoreCase))
            {
                EnsureRequiredParametersIncluded();
                return;
            }

            string raw = opening.Substring(_definition.Id.Length).TrimStart();
            if (raw.StartsWith("=", StringComparison.Ordinal))
                raw = raw.Substring(1);

            TextStudioTagParameters parsed = TextStudioTagParameterParser.Parse(raw);
            for (int i = 0; i < _definition.Parameters.Count; i++)
            {
                TextStudioTagParameterDefinition parameter = _definition.Parameters[i];
                string value = FindParsedValue(in parsed, parameter);
                if (!string.IsNullOrEmpty(value))
                {
                    _values[i] = value;
                    _included[i] = true;
                }
                else if (parameter.Required)
                {
                    _included[i] = true;
                }
            }
        }

        void ApplyRecipeDefaults()
        {
            for (int i = 0; i < _definition.Parameters.Count; i++)
            {
                if (!string.IsNullOrEmpty(_values[i]))
                    continue;

                TextStudioTagParameterDefinition parameter = _definition.Parameters[i];
                _values[i] = !string.IsNullOrEmpty(parameter.DefaultValue)
                    ? parameter.DefaultValue
                    : parameter.ExampleValue;
            }

            if (string.Equals(_definition.Id, TextStudioBuiltInActionIds.Wait, StringComparison.OrdinalIgnoreCase))
                SetDefault("seconds", "0.5");
            else if (string.Equals(_definition.Id, TextStudioBuiltInActionIds.Speed, StringComparison.OrdinalIgnoreCase))
                SetDefault("multiplier", "1.25");
            else if (string.Equals(_definition.Id, TextStudioBuiltInActionIds.SpeedUp, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(_definition.Id, TextStudioBuiltInActionIds.SpeedDown, StringComparison.OrdinalIgnoreCase))
                SetDefault("factor", "2");
            else if (string.Equals(_definition.Id, TextStudioBuiltInActionIds.Event, StringComparison.OrdinalIgnoreCase))
                SetDefault("route", "eventRoute");

            EnsureRequiredParametersIncluded();
        }

        void SetDefault(string parameterName, string value)
        {
            for (int i = 0; i < _definition.Parameters.Count; i++)
            {
                if (!_definition.Parameters[i].MatchesName(parameterName) ||
                    !string.IsNullOrEmpty(_values[i]))
                {
                    continue;
                }

                _values[i] = value;
                _included[i] = true;
                return;
            }
        }

        void EnsureRequiredParametersIncluded()
        {
            for (int i = 0; i < _definition.Parameters.Count; i++)
                _included[i] |= _definition.Parameters[i].Required;
        }

        static string FindParsedValue(
            in TextStudioTagParameters parameters,
            TextStudioTagParameterDefinition definition)
        {
            if (parameters.TryGetString(definition.Name, out string named))
                return named;

            for (int i = 0; i < definition.Aliases.Count; i++)
            {
                if (parameters.TryGetString(definition.Aliases[i], out named))
                    return named;
            }

            return definition.PositionalIndex >= 0
                ? parameters.GetString(definition.PositionalIndex)
                : string.Empty;
        }

        static bool TryValidateValue(
            TextStudioTagParameterDefinition parameter,
            string value,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(value))
            {
                error = parameter.DisplayName + " requires a value.";
                return false;
            }

            if (value.IndexOf('<') >= 0 ||
                value.IndexOf('>') >= 0 ||
                value.IndexOf('\r') >= 0 ||
                value.IndexOf('\n') >= 0 ||
                (value.IndexOf('"') >= 0 && value.IndexOf('\'') >= 0))
            {
                error = parameter.DisplayName + " contains characters that cannot be represented safely in markup.";
                return false;
            }

            switch (parameter.Kind)
            {
                case TextStudioTagParameterKind.Float:
                    if (!float.TryParse(
                            value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out float floatValue) ||
                        float.IsNaN(floatValue) ||
                        float.IsInfinity(floatValue))
                    {
                        error = parameter.DisplayName + " must be a finite number.";
                        return false;
                    }
                    break;
                case TextStudioTagParameterKind.Integer:
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    {
                        error = parameter.DisplayName + " must be a whole number.";
                        return false;
                    }
                    break;
                case TextStudioTagParameterKind.Boolean:
                    if (!bool.TryParse(value, out _))
                    {
                        error = parameter.DisplayName + " must be true or false.";
                        return false;
                    }
                    break;
                case TextStudioTagParameterKind.Color:
                    if (!ColorUtility.TryParseHtmlString(value, out _))
                    {
                        error = parameter.DisplayName + " must be an HTML color such as #66CCFF.";
                        return false;
                    }
                    break;
                case TextStudioTagParameterKind.Pivot:
                    if (!TextStudioPivotParser.TryParse(value, out _))
                    {
                        error = parameter.DisplayName +
                                " must be a named pivot or two coordinates written as x,y.";
                        return false;
                    }
                    break;
                case TextStudioTagParameterKind.Enum:
                    if (!parameter.IsValueAllowed(value))
                    {
                        error = parameter.DisplayName + " must use one of its listed values.";
                        return false;
                    }
                    break;
            }

            return true;
        }

        static void AppendValue(StringBuilder builder, string value)
        {
            bool requiresQuotes =
                value.IndexOfAny(new[] { ' ', '\t', ',', '=' }) >= 0 ||
                value.IndexOf('"') >= 0 ||
                value.IndexOf('\'') >= 0;
            if (!requiresQuotes)
            {
                builder.Append(value);
                return;
            }

            char quote = value.IndexOf('"') >= 0 ? '\'' : '"';
            builder.Append(quote);
            builder.Append(value);
            builder.Append(quote);
        }
    }
}
