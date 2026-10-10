using System;
using System.Collections.Generic;
using System.Globalization;

namespace TextStudio
{
    internal static class TextStudioEffectInvocationParser
    {
        public static bool TryParse(
            string source,
            out TextStudioEffectInvocation invocation,
            out string error)
        {
            invocation = default;
            error = string.Empty;

            string trimmed = source != null ? source.Trim() : string.Empty;
            if (string.IsNullOrEmpty(trimmed))
            {
                error = "Effect invocation is empty.";
                return false;
            }

            if (!TryExtractInvocationBody(trimmed, out string body, out error))
                return false;

            if (!TrySplitNameAndParameters(body, out string effectId, out string rawParameters, out error))
                return false;

            if (TextStudioPhaseTagUtility.TryResolveEffectId(effectId, out string canonicalId))
                effectId = canonicalId;

            TextStudioTagParameters parsedParameters = TextStudioTagParameterParser.Parse(
                rawParameters,
                allowNumericModifiers: true);
            TextStudioTagParameters normalizedParameters =
                BuiltIns.BuiltInPlaybacks.CanonicalizeTextAnimatorKeywordParameters(in parsedParameters);
            if (!TryExtractInvocationControls(
                    effectId,
                    in normalizedParameters,
                    out TextStudioTagParameters effectParameters,
                    out TextStudioPhaseMask phaseMask,
                    out float intensity,
                    out float timeOffset,
                    out bool hasRange,
                    out TextStudioTextRange range,
                    out error))
            {
                return false;
            }

            invocation = new TextStudioEffectInvocation(
                effectId,
                effectParameters,
                phaseMask,
                intensity,
                timeOffset,
                hasRange,
                range);
            return true;
        }

        static bool TryExtractInvocationBody(string source, out string body, out string error)
        {
            body = string.Empty;
            error = string.Empty;

            if (source.IndexOf("</", StringComparison.Ordinal) >= 0)
            {
                error = "Effect invocation cannot include closing tags or wrapped content.";
                return false;
            }

            if (source[0] != '<')
            {
                if (source.IndexOf('<') >= 0 || source.IndexOf('>') >= 0)
                {
                    error = "Effect invocation cannot include literal tag content.";
                    return false;
                }

                body = source;
                return true;
            }

            int close = source.IndexOf('>');
            if (close < 0)
            {
                error = "Effect invocation tag is missing its closing '>'.";
                return false;
            }

            if (close != source.Length - 1)
            {
                error = "Effect invocation cannot include text content after the opening tag.";
                return false;
            }

            body = source.Substring(1, close - 1).Trim();
            if (body.StartsWith("/", StringComparison.Ordinal))
            {
                error = "Effect invocation cannot be a closing tag.";
                return false;
            }

            if (body.EndsWith("/", StringComparison.Ordinal))
                body = body.Substring(0, body.Length - 1).TrimEnd();

            return !string.IsNullOrEmpty(body) || Fail("Effect invocation tag is empty.", out error);
        }

        static bool TrySplitNameAndParameters(
            string body,
            out string effectId,
            out string rawParameters,
            out string error)
        {
            effectId = string.Empty;
            rawParameters = string.Empty;
            error = string.Empty;

            int nameEnd = 0;
            while (nameEnd < body.Length)
            {
                char c = body[nameEnd];
                if (char.IsWhiteSpace(c) || c == '=' || c == '/')
                    break;
                nameEnd++;
            }

            if (nameEnd <= 0)
            {
                error = "Effect invocation is missing an effect tag.";
                return false;
            }

            effectId = body.Substring(0, nameEnd).Trim();
            if (!IsValidEffectId(effectId))
            {
                error = "Effect invocation has an invalid effect tag.";
                return false;
            }

            int parameterStart = nameEnd;
            while (parameterStart < body.Length && char.IsWhiteSpace(body[parameterStart]))
                parameterStart++;

            if (parameterStart < body.Length && body[parameterStart] == '=')
            {
                parameterStart++;
                while (parameterStart < body.Length && char.IsWhiteSpace(body[parameterStart]))
                    parameterStart++;
            }

            if (parameterStart < body.Length)
                rawParameters = body.Substring(parameterStart).Trim();

            return true;
        }

        static bool IsValidEffectId(string effectId)
        {
            if (string.IsNullOrWhiteSpace(effectId))
                return false;

            for (int i = 0; i < effectId.Length; i++)
            {
                char c = effectId[i];
                if (char.IsWhiteSpace(c) || c == '<' || c == '>' || c == '/' || c == '=')
                    return false;
            }

            return true;
        }

        internal static bool TryExtractInvocationControls(
            string effectId,
            in TextStudioTagParameters parameters,
            out TextStudioTagParameters effectParameters,
            out TextStudioPhaseMask phaseMask,
            out float intensity,
            out float timeOffset,
            out bool hasRange,
            out TextStudioTextRange range,
            out string error,
            ITextStudioEffectDefinition localDefinition = null)
        {
            phaseMask = TextStudioPhaseMask.All;
            intensity = 1f;
            timeOffset = 0f;
            hasRange = false;
            range = TextStudioTextRange.All;
            error = string.Empty;

            IReadOnlyList<string> positional = parameters.Positional;
            IReadOnlyList<TextStudioNamedTagParameter> named = parameters.Named;
            List<TextStudioNamedTagParameter> filteredNamed = null;
            IReadOnlyList<TextStudioTagParameterDefinition> declaredParameters =
                GetDeclaredParameters(effectId, localDefinition);

            for (int i = 0; i < named.Count; i++)
            {
                TextStudioNamedTagParameter parameter = named[i];
                if (string.Equals(parameter.Name, "range", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TextStudioTextRange.TryParse(parameter.Value, out range, out error))
                    {
                        error = "Effect range: " + error;
                        effectParameters = default;
                        return false;
                    }
                    hasRange = true;
                    continue;
                }
                // An effect's declared names and aliases belong to the effect. In particular,
                // a geometric offset must not be consumed as an invocation clock offset.
                if (IsDeclaredParameter(declaredParameters, parameter.Name))
                {
                    filteredNamed ??= new List<TextStudioNamedTagParameter>(named.Count);
                    filteredNamed.Add(parameter);
                    continue;
                }

                if (IsInvocationControl(parameter.Name, "phaseMask", "phase"))
                {
                    if (!TryParsePhaseMask(parameter.Value, out phaseMask))
                    {
                        error = "Effect invocation has an invalid phase value.";
                        effectParameters = default;
                        return false;
                    }

                    continue;
                }

                if (IsInvocationControl(parameter.Name, "intensityMultiplier", "intensity"))
                {
                    if (!TryParseFloat(parameter.Value, out intensity))
                    {
                        error = "Effect invocation has an invalid intensity value.";
                        effectParameters = default;
                        return false;
                    }

                    continue;
                }

                if (IsInvocationControl(parameter.Name, "timeOffset", "offset"))
                {
                    if (!TryParseFloat(parameter.Value, out timeOffset))
                    {
                        error = "Effect invocation has an invalid offset value.";
                        effectParameters = default;
                        return false;
                    }

                    continue;
                }

                filteredNamed ??= new List<TextStudioNamedTagParameter>(named.Count);
                filteredNamed.Add(parameter);
            }

            effectParameters = TextStudioEffectParameterUtility.CreateCanonicalParameters(
                positional,
                filteredNamed);
            return true;
        }

        static IReadOnlyList<TextStudioTagParameterDefinition> GetDeclaredParameters(
            string effectId,
            ITextStudioEffectDefinition localDefinition)
        {
            ITextStudioEffectDefinition definition = localDefinition;
            if (definition == null || !string.Equals(definition.Id, effectId, StringComparison.OrdinalIgnoreCase))
                TextStudioRegistry.Effects.TryGetQuiet(effectId, out definition);
            return (definition as ITextStudioEffectMetadataProvider)?.Metadata?.Parameters;
        }

        static bool IsDeclaredParameter(IReadOnlyList<TextStudioTagParameterDefinition> parameters, string name)
        {
            if (parameters == null) return false;
            for (int i = 0; i < parameters.Count; i++)
            {
                TextStudioTagParameterDefinition parameter = parameters[i];
                if (parameter == null) continue;
                if (string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
                for (int aliasIndex = 0; aliasIndex < parameter.Aliases.Count; aliasIndex++)
                    if (string.Equals(parameter.Aliases[aliasIndex], name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        static bool IsInvocationControl(string name, string control, string alias)
        {
            return string.Equals(name, control, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, alias, StringComparison.OrdinalIgnoreCase);
        }

        static bool TryParseFloat(string raw, out float value)
        {
            return float.TryParse(
                raw,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        static bool TryParsePhaseMask(string raw, out TextStudioPhaseMask phaseMask)
        {
            phaseMask = TextStudioPhaseMask.None;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string[] parts = raw.Split(new[] { ',', '|', '+' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (string.Equals(part, "all", StringComparison.OrdinalIgnoreCase))
                {
                    phaseMask |= TextStudioPhaseMask.All;
                    continue;
                }

                if (string.Equals(part, "reveal", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(part, "appear", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(part, "appearance", StringComparison.OrdinalIgnoreCase))
                {
                    phaseMask |= TextStudioPhaseMask.Reveal;
                    continue;
                }

                if (string.Equals(part, "active", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(part, "persist", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(part, "persistent", StringComparison.OrdinalIgnoreCase))
                {
                    phaseMask |= TextStudioPhaseMask.Active;
                    continue;
                }

                if (string.Equals(part, "hide", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(part, "disappear", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(part, "disappearance", StringComparison.OrdinalIgnoreCase))
                {
                    phaseMask |= TextStudioPhaseMask.Hide;
                    continue;
                }

                return false;
            }

            return phaseMask != TextStudioPhaseMask.None;
        }

        static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
