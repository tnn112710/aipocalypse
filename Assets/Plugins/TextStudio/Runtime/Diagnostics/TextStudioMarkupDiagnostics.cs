using System;
using System.Collections.Generic;
using TextStudio.BuiltIns;

namespace TextStudio
{
    public enum TextStudioDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum TextStudioDiagnosticCode
    {
        NativeRichTextTag,
        TextStudioEffectTag,
        ExtensionTag,
        ActionTag,
        MarkerTag,
        UnknownTag,
        MalformedTag,
        UnmatchedClosingTag,
        UnclosedTag,
        MissingParameter,
        InvalidParameter,
        UnknownParameter,
        UnsupportedParameterValue,
        MissingReference,
        UnsupportedBackend,
        InvalidEffectInvocation,
        ComposedEffectCycle,
        PhotosensitivityRisk,
        AmbiguousAppearanceTag
    }

    public enum TextStudioTagKind
    {
        NativeRichText,
        TextStudioEffect,
        Extension,
        Action,
        Marker,
        CustomDecoration,
        Typewriter,
        TagExpansion,
        Unknown,
        Appearance
    }

    /// <summary>One markup diagnostic with severity, stable code, source range, message, and suggested action.</summary>
    public readonly struct TextStudioDiagnostic
    {
        public readonly TextStudioDiagnosticSeverity Severity;
        public readonly TextStudioDiagnosticCode Code;
        public readonly string Message;
        public readonly string TagName;
        public readonly int SourceStart;
        public readonly int SourceEnd;

        /// <summary>Creates a diagnostic with normalized non-null message and action text.</summary>
        public TextStudioDiagnostic(
            TextStudioDiagnosticSeverity severity,
            TextStudioDiagnosticCode code,
            string message,
            string tagName,
            int sourceStart,
            int sourceEnd)
        {
            Severity = severity;
            Code = code;
            Message = message ?? string.Empty;
            TagName = tagName ?? string.Empty;
            SourceStart = sourceStart;
            SourceEnd = sourceEnd;
        }
    }

    /// <summary>Diagnostic annotation associated with one parsed tag occurrence.</summary>
    public readonly struct TextStudioTagDiagnostic
    {
        public readonly string Name;
        public readonly TextStudioTagKind Kind;
        public readonly int SourceStart;
        public readonly int SourceEnd;
        public readonly bool HasParameter;
        public readonly bool IsClosing;
        public readonly string RawParameter;
        public readonly string DefinitionId;

        /// <summary>Creates a diagnostic mapped to one source tag range.</summary>
        public TextStudioTagDiagnostic(
            string name,
            TextStudioTagKind kind,
            int sourceStart,
            int sourceEnd,
            bool hasParameter,
            bool isClosing,
            string rawParameter = null,
            string definitionId = null)
        {
            Name = name ?? string.Empty;
            Kind = kind;
            SourceStart = sourceStart;
            SourceEnd = sourceEnd;
            HasParameter = hasParameter;
            IsClosing = isClosing;
            RawParameter = rawParameter ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
        }
    }

    /// <summary>Immutable analyzed markup plus general and tag-specific diagnostics.</summary>
#if UNITY_6000_7_OR_NEWER
    // Empty is immutable diagnostic metadata shared by all callers.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed partial class TextStudioDiagnosticReport
    {
        public static readonly TextStudioDiagnosticReport Empty = new TextStudioDiagnosticReport(
            string.Empty,
            Array.Empty<TextStudioDiagnostic>(),
            Array.Empty<TextStudioTagDiagnostic>());

        public string Source { get; }
        public TextStudioDiagnostic[] Diagnostics { get; }
        public TextStudioTagDiagnostic[] Tags { get; }

        public int AuthoredTagCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Tags.Length; i++)
                {
                    if (!Tags[i].IsClosing)
                        count++;
                }

                return count;
            }
        }

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Diagnostics.Length; i++)
                {
                    if (Diagnostics[i].Severity == TextStudioDiagnosticSeverity.Error)
                        return true;
                }

                return false;
            }
        }

        public bool HasWarnings
        {
            get
            {
                for (int i = 0; i < Diagnostics.Length; i++)
                {
                    if (Diagnostics[i].Severity == TextStudioDiagnosticSeverity.Warning)
                        return true;
                }

                return false;
            }
        }

        /// <summary>Creates a report that owns the supplied immutable diagnostic arrays.</summary>
        public TextStudioDiagnosticReport(
            string source,
            TextStudioDiagnostic[] diagnostics,
            TextStudioTagDiagnostic[] tags)
        {
            Source = source ?? string.Empty;
            Diagnostics = diagnostics ?? Array.Empty<TextStudioDiagnostic>();
            Tags = tags ?? Array.Empty<TextStudioTagDiagnostic>();
        }
    }

    /// <summary>Analyzes authored markup for syntax, registration, compatibility, and parameter problems without rendering it.</summary>
    public static class TextStudioMarkupDiagnostics
    {
        /// <summary>Analyzes markup against the default tag catalog and installed registrations.</summary>
        public static TextStudioDiagnosticReport Analyze(string source)
        {
            return Analyze(source, null);
        }

        /// <summary>Analyzes markup with caller-supplied backend, catalog, and suggestion context.</summary>
        public static TextStudioDiagnosticReport Analyze(string source, TextStudioTagAnalysisContext context)
        {
            if (string.IsNullOrEmpty(source))
                return TextStudioDiagnosticReport.Empty;

            List<TextStudioDiagnostic> diagnostics = new List<TextStudioDiagnostic>();
            List<TextStudioTagDiagnostic> tags = new List<TextStudioTagDiagnostic>();
            List<OpenTag> openTags = new List<OpenTag>();

            int i = 0;
            while (i < source.Length)
            {
                if (source[i] != '<' || TextStudioTagExpansionUtility.IsEscaped(source, i))
                {
                    i++;
                    continue;
                }

                if (TextStudioTagExpansionUtility.IsCommentStart(source, i))
                {
                    int commentEnd = source.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    i = commentEnd >= 0 ? commentEnd + 3 : source.Length;
                    continue;
                }
                int tagEnd = FindTagEnd(source, i + 1);
                if (tagEnd < 0)
                {
                    int malformedEnd = FindMalformedEnd(source, i + 1);
                    string brokenCloseName = ExtractBrokenClosingName(source, i + 1, malformedEnd);
                    diagnostics.Add(new TextStudioDiagnostic(
                        TextStudioDiagnosticSeverity.Error,
                        TextStudioDiagnosticCode.MalformedTag,
                        "Tag is missing a valid closing bracket.",
                        brokenCloseName,
                        i,
                        Math.Max(i + 1, malformedEnd)));

                    if (!string.IsNullOrEmpty(brokenCloseName))
                    {
                        diagnostics.Add(new TextStudioDiagnostic(
                            TextStudioDiagnosticSeverity.Error,
                            TextStudioDiagnosticCode.UnmatchedClosingTag,
                            "Closing tag has no matching open tag.",
                            brokenCloseName,
                            i,
                            Math.Max(i + 1, malformedEnd)));
                    }

                    i++;
                    continue;
                }

                AnalyzeTag(source, i, tagEnd, diagnostics, tags, openTags, context);
                bool noParse = string.Equals(source.Substring(i + 1, tagEnd - i - 1).Trim(), "noparse", StringComparison.OrdinalIgnoreCase);
                i = tagEnd + 1;
                if (noParse)
                {
                    int closing = source.IndexOf("</noparse>", i, StringComparison.OrdinalIgnoreCase);
                    i = closing >= 0 ? closing : source.Length;
                }
            }

            return new TextStudioDiagnosticReport(source, diagnostics.ToArray(), tags.ToArray());
        }

        static void AnalyzeTag(
            string source,
            int tagStart,
            int tagEnd,
            List<TextStudioDiagnostic> diagnostics,
            List<TextStudioTagDiagnostic> tags,
            List<OpenTag> openTags,
            TextStudioTagAnalysisContext context)
        {
            int innerStart = tagStart + 1;
            if (innerStart >= tagEnd) return;

            while (innerStart < tagEnd && char.IsWhiteSpace(source[innerStart])) innerStart++;
            if (innerStart >= tagEnd) return;

            bool isClosing = source[innerStart] == '/';
            if (isClosing)
            {
                innerStart++;
                while (innerStart < tagEnd && char.IsWhiteSpace(source[innerStart])) innerStart++;
            }

            int trimmedEnd = tagEnd;
            while (trimmedEnd > innerStart && char.IsWhiteSpace(source[trimmedEnd - 1])) trimmedEnd--;
            bool selfClosing = !isClosing && trimmedEnd > innerStart && source[trimmedEnd - 1] == '/';
            if (selfClosing)
            {
                trimmedEnd--;
                while (trimmedEnd > innerStart && char.IsWhiteSpace(source[trimmedEnd - 1])) trimmedEnd--;
            }

            int nameStart = innerStart;
            while (innerStart < trimmedEnd &&
                   !char.IsWhiteSpace(source[innerStart]) &&
                   source[innerStart] != '=' &&
                   source[innerStart] != '/')
                innerStart++;

            if (innerStart <= nameStart && !isClosing)
                return;

            string name = source.Substring(nameStart, innerStart - nameStart);
            bool usesDirectAssignment = UsesDirectParameterAssignment(source, innerStart, trimmedEnd);
            string rawParameter = ExtractRawParameter(source, innerStart, trimmedEnd);
            bool hasParameter = rawParameter.Length > 0;

            TextStudioTagParameters parsedParameters = TextStudioTagParameterParser.Parse(rawParameter);
            string appearanceId = string.Empty;
            bool appearance = !isClosing && TextStudioAppearanceTagSyntax.TryClassify(name, usesDirectAssignment,
                in parsedParameters, out appearanceId, out _);
            TextStudioTagKind kind = appearance ? TextStudioTagKind.Appearance : Classify(name, hasParameter, usesDirectAssignment, context);
            string definitionId = appearanceId;
            int matchingOpen = -1;
            if (isClosing)
            {
                string closeKey = TextStudioRangeLedger.CanonicalCloseName(name);
                for (int o = openTags.Count - 1; o >= 0; o--)
                {
                    if (name.Length > 0 && !string.Equals(openTags[o].Name, closeKey, StringComparison.OrdinalIgnoreCase)) continue;
                    matchingOpen = o;
                    kind = openTags[o].Kind;
                    definitionId = openTags[o].DefinitionId;
                    break;
                }
            }
            TextStudioTagDefinition knownDefinition = null;
            if (kind != TextStudioTagKind.Appearance && context != null && context.Catalog != null &&
                context.Catalog.TryGetDefinition(name, hasParameter, kind, out knownDefinition))
            {
                definitionId = knownDefinition.Id;
            }

            tags.Add(new TextStudioTagDiagnostic(name, kind, tagStart, tagEnd + 1, hasParameter, isClosing, rawParameter, definitionId));

            if (isClosing)
            {
                if (kind == TextStudioTagKind.Action || kind == TextStudioTagKind.Marker)
                    return;

                if (matchingOpen >= 0) openTags.RemoveAt(matchingOpen);
                else
                {
                    diagnostics.Add(new TextStudioDiagnostic(
                        TextStudioDiagnosticSeverity.Error,
                        TextStudioDiagnosticCode.UnmatchedClosingTag,
                        "Closing tag has no matching open tag.",
                        name,
                        tagStart,
                        tagEnd + 1));
                }

                return;
            }

            if (kind != TextStudioTagKind.Appearance)
                ValidateKnownTag(source, name, kind, rawParameter, hasParameter, tagStart, tagEnd + 1, context, diagnostics);
            bool appearanceSpelling = name.StartsWith("$", StringComparison.Ordinal) ||
                (string.Equals(name, "style", StringComparison.OrdinalIgnoreCase) && !usesDirectAssignment &&
                 (parsedParameters.TryGetNamedParameter("ref", out _) || HasAppearanceParameter(in parsedParameters)));
            if (!isClosing && appearanceSpelling && TextStudioAppearanceTagSyntax.HasLegacyNameCollision(name))
            {
                string id = name.StartsWith("$", StringComparison.Ordinal) ? name.Substring(1) : parsedParameters.GetString("ref");
                string alternative = TextStudioAppearanceTagSyntax.GetPreferredOpeningTag(id);
                diagnostics.Add(new TextStudioDiagnostic(TextStudioDiagnosticSeverity.Warning, TextStudioDiagnosticCode.AmbiguousAppearanceTag,
                    "This spelling belongs to an existing registration." + (alternative != null ? " Use " + alternative + " for the appearance." : " Both appearance spellings are occupied; rename the conflicting registration."),
                    name, tagStart, tagEnd + 1));
            }
            if (name.StartsWith("#", StringComparison.Ordinal) && !BuiltInStyles.IsNativeColorShorthand(name))
                diagnostics.Add(new TextStudioDiagnostic(TextStudioDiagnosticSeverity.Warning, TextStudioDiagnosticCode.InvalidParameter,
                    "Native color shorthand requires exactly 3, 4, 6, or 8 hexadecimal digits.", name, tagStart, tagEnd + 1));
            if (kind == TextStudioTagKind.TextStudioEffect)
            {
                TextStudioTagParameters parameters = TextStudioTagParameterParser.Parse(
                    rawParameter,
                    allowNumericModifiers: true);
                ValidatePhotosensitivityRisk(
                    name,
                    in parameters,
                    tagStart,
                    tagEnd + 1,
                    diagnostics);
                ValidateEffectCompatibility(
                    name,
                    rawParameter,
                    tagStart,
                    tagEnd + 1,
                    context,
                    diagnostics);
            }

            if (kind == TextStudioTagKind.Unknown)
            {
                diagnostics.Add(new TextStudioDiagnostic(
                    TextStudioDiagnosticSeverity.Warning,
                    TextStudioDiagnosticCode.UnknownTag,
                    "Tag is not a known TMP tag, Text Studio effect, typewriter tag, action, marker, decoration, or extension tag.",
                    name,
                    tagStart,
                    tagEnd + 1));
            }

            bool isSelfContainedNative = kind == TextStudioTagKind.NativeRichText &&
                                         BuiltInStyles.IsSelfContainedNativeRichTextFormatting(name, hasParameter);
            bool isRangeExtension = kind == TextStudioTagKind.Extension &&
                                    TextStudioRegistry.ExtensionTags.TryGet(name, out TextStudioExtensionTagDefinition definition) &&
                                    definition.Shape == TextStudioExtensionTagShape.Range;

            if ((kind == TextStudioTagKind.NativeRichText ||
                kind == TextStudioTagKind.TextStudioEffect ||
                kind == TextStudioTagKind.CustomDecoration ||
                kind == TextStudioTagKind.Typewriter ||
                kind == TextStudioTagKind.TagExpansion ||
                kind == TextStudioTagKind.Appearance ||
                kind == TextStudioTagKind.Unknown ||
                isRangeExtension) &&
                !isSelfContainedNative &&
                !selfClosing)
            {
                openTags.Add(new OpenTag(TextStudioRangeLedger.CanonicalCloseName(name), kind, definitionId));
            }
        }

        static void ValidateEffectCompatibility(
            string name,
            string rawParameter,
            int sourceStart,
            int sourceEnd,
            TextStudioTagAnalysisContext context,
            List<TextStudioDiagnostic> diagnostics)
        {
            if (!TextStudioRegistry.Effects.TryGetQuiet(name, out ITextStudioEffectDefinition effect))
                return;

            TextStudioRenderDomain requested = context?.RenderDomains ?? TextStudioRenderDomain.All;
            TextStudioEffectCompatibility compatibility =
                TextStudioEffectCompatibilityResolver.Resolve(effect, requested);
            if (compatibility.State == TextStudioEffectCompatibilityState.Unsupported ||
                compatibility.State == TextStudioEffectCompatibilityState.PartiallyCompatible)
            {
                diagnostics.Add(new TextStudioDiagnostic(
                    TextStudioDiagnosticSeverity.Warning,
                    TextStudioDiagnosticCode.UnsupportedBackend,
                    compatibility.Reason,
                    name,
                    sourceStart,
                    sourceEnd));
            }

            if (effect is not TextStudioComposedEffectAsset)
                return;

            TextStudioEffectResolver resolver = new TextStudioEffectResolver();
            List<TextStudioResolvedEffectBinding> output = new List<TextStudioResolvedEffectBinding>(8);
            TextStudioDiagnosticSink sink = new TextStudioDiagnosticSink(
                diagnostics,
                name,
                sourceStart,
                sourceEnd);
            resolver.TryResolve(
                name,
                TextStudioTagParameterParser.Parse(rawParameter),
                TextStudioPhaseMask.All,
                output,
                sink);
        }

        static TextStudioTagKind Classify(
            string name,
            bool hasParameter,
            bool usesDirectAssignment,
            TextStudioTagAnalysisContext context)
        {
            if (!usesDirectAssignment && string.Equals(name, "style", StringComparison.OrdinalIgnoreCase) &&
                TextStudioAppearanceTagSyntax.HasLegacyNameCollision(name))
            {
                if (TextStudioRegistry.TagExpansions.TryGet(name, out _)) return TextStudioTagKind.TagExpansion;
                if (TextStudioRegistry.Effects.TryGetQuiet(name, out _)) return TextStudioTagKind.TextStudioEffect;
                if (TextStudioRegistry.Actions.TryGetQuiet(name, out _)) return TextStudioTagKind.Action;
                return TextStudioTagKind.Extension;
            }
            if (BuiltInStyles.IsNativeRichTextFormattingInvocation(
                    name,
                    hasParameter,
                    usesDirectAssignment))
                return TextStudioTagKind.NativeRichText;
            if (BuiltInStyles.IsCustomDecorationTag(name))
                return TextStudioTagKind.CustomDecoration;
            if (string.Equals(name, "instant", StringComparison.OrdinalIgnoreCase))
                return TextStudioTagKind.Typewriter;
            if (TextStudioPhaseTagUtility.IsPhaseTag(name))
                return TextStudioTagKind.TextStudioEffect;
            if (BuiltInCharacterSubstitutions.IsCharacterSubstitutionTag(name))
                return TextStudioTagKind.TextStudioEffect;
            if (BuiltInStyles.TryResolve(name, out _))
                return TextStudioTagKind.TextStudioEffect;
            if (TextStudioRegistry.Effects.TryGetQuiet(name, out _))
                return TextStudioTagKind.TextStudioEffect;
            if (BuiltInActions.IsMarkerTag(name))
                return TextStudioTagKind.Marker;
            if (BuiltInActions.TryGetCanonicalActionId(name, out _))
                return TextStudioTagKind.Action;
            if (TextStudioRegistry.Actions.TryGetQuiet(name, out _))
                return TextStudioTagKind.Action;
            if (TextStudioRegistry.TagExpansions.TryGet(name, out _))
                return TextStudioTagKind.TagExpansion;
            if (context != null &&
                context.Catalog != null &&
                context.Catalog.TryGetDefinition(name, hasParameter, out TextStudioTagDefinition definition))
            {
                return definition.Kind;
            }
            if (TextStudioTagUtility.TryGetExtensionTag(name, hasParameter, out _))
                return TextStudioTagKind.Extension;
            return TextStudioTagKind.Unknown;
        }

        static string ExtractRawParameter(string source, int start, int end)
        {
            int index = start;
            while (index < end && char.IsWhiteSpace(source[index]))
                index++;

            if (index >= end)
                return string.Empty;

            if (source[index] == '=')
            {
                index++;
                while (index < end && char.IsWhiteSpace(source[index]))
                    index++;
            }

            return index < end ? source.Substring(index, end - index).Trim() : string.Empty;
        }

        static bool UsesDirectParameterAssignment(string source, int start, int end)
        {
            int index = start;
            while (index < end && char.IsWhiteSpace(source[index]))
                index++;

            return index < end && source[index] == '=';
        }

        static void ValidateKnownTag(
            string source,
            string name,
            TextStudioTagKind kind,
            string rawParameter,
            bool hasParameter,
            int sourceStart,
            int sourceEnd,
            TextStudioTagAnalysisContext context,
            List<TextStudioDiagnostic> diagnostics)
        {
            if (context == null || context.Catalog == null || kind == TextStudioTagKind.Unknown)
                return;

            if (!context.Catalog.TryGetDefinition(
                    name,
                    hasParameter,
                    kind,
                    out TextStudioTagDefinition definition))
                return;

            if (context.Backend != TextStudioTagBackend.None && !definition.SupportsBackend(context.Backend))
            {
                diagnostics.Add(new TextStudioDiagnostic(
                    TextStudioDiagnosticSeverity.Warning,
                    TextStudioDiagnosticCode.UnsupportedBackend,
                    "Tag is available, but the selected text backend does not fully support it.",
                    name,
                    sourceStart,
                    sourceEnd));
            }

            TextStudioRenderDomain requestedDomains = context.RenderDomains;
            if (requestedDomains != TextStudioRenderDomain.None &&
                (definition.SupportedRenderDomains & requestedDomains) == TextStudioRenderDomain.None)
            {
                diagnostics.Add(new TextStudioDiagnostic(
                    TextStudioDiagnosticSeverity.Warning,
                    TextStudioDiagnosticCode.UnsupportedBackend,
                    "Tag is available, but the selected renderer does not support it.",
                    name,
                    sourceStart,
                    sourceEnd));
            }

            IReadOnlyList<TextStudioTagParameterDefinition> parameterDefinitions = definition.Parameters;
            TextStudioTagParameters parameters = CanonicalizeParametersForDiagnostics(
                name,
                definition,
                TextStudioTagParameterParser.Parse(
                    rawParameter,
                    allowNumericModifiers: true));
            if (parameterDefinitions.Count == 0)
                return;
            bool isPhaseTag = TextStudioPhaseTagUtility.IsPhaseTag(name);
            bool supportsMultiplication = SupportsNumericParameterMultiplication(name);
            for (int i = 0; i < parameterDefinitions.Count; i++)
            {
                TextStudioTagParameterDefinition parameter = parameterDefinitions[i];
                bool present = TryGetParameterValue(
                    parameters,
                    parameter,
                    out string value,
                    out TextStudioTagParameterOperation operation);
                if (!present &&
                    isPhaseTag &&
                    string.Equals(parameter.Name, "effect", StringComparison.OrdinalIgnoreCase) &&
                    TryGetPhaseWrapperEffect(parameters, out value))
                {
                    present = true;
                }

                if (!present)
                {
                    if (parameter.Required)
                    {
                        diagnostics.Add(new TextStudioDiagnostic(
                            TextStudioDiagnosticSeverity.Warning,
                            TextStudioDiagnosticCode.MissingParameter,
                            "Tag is missing required parameter '" + parameter.DisplayName + "'.",
                            name,
                            sourceStart,
                            sourceEnd));
                    }

                    continue;
                }

                ValidateParameterValue(
                    name,
                    parameter,
                    value,
                    operation,
                    supportsMultiplication,
                    context,
                    sourceStart,
                    sourceEnd,
                    diagnostics);
            }

            if (isPhaseTag)
                return;

            for (int i = 0; i < parameters.NamedCount; i++)
            {
                TextStudioNamedTagParameter named = parameters.Named[i];
                if (definition.GetNamedParameter(named.Name) != null)
                    continue;
                if (IsColorContinuationFlag(parameters, definition, i))
                    continue;

                diagnostics.Add(new TextStudioDiagnostic(
                    TextStudioDiagnosticSeverity.Warning,
                    TextStudioDiagnosticCode.UnknownParameter,
                    "Parameter '" + named.Name + "' is not documented for this tag.",
                    name,
                    sourceStart,
                    sourceEnd));
            }
        }

        static void ValidatePhotosensitivityRisk(
            string effectId,
            in TextStudioTagParameters parameters,
            int sourceStart,
            int sourceEnd,
            List<TextStudioDiagnostic> diagnostics)
        {
            string resolvedId = BuiltInStyles.TryResolve(effectId, out string builtInId)
                ? builtInId
                : effectId;
            if (!TextStudioRegistry.Effects.TryGetQuiet(resolvedId, out ITextStudioEffectDefinition definition) ||
                definition is not ITextStudioEffectCapabilitiesProvider provider ||
                !provider.Capabilities.CanFlash)
            {
                return;
            }

            if (string.Equals(resolvedId, TextStudioBuiltInEffectIds.PixelGrid, StringComparison.OrdinalIgnoreCase))
            {
                if (!parameters.TryGetFloat("pulse", out float pixelSpeed) &&
                    !parameters.TryGetFloat("p", out pixelSpeed))
                {
                    return;
                }

                float activeAmount = parameters.TryGetFloat("active", out float parsedActive)
                    ? parsedActive
                    : parameters.TryGetFloat("a", out parsedActive) ? parsedActive : 1f;
                float minimum = parameters.TryGetFloat("min", out float parsedMinimum)
                    ? parsedMinimum
                    : 4f;
                float maximum = parameters.TryGetFloat("max", out float parsedMaximum)
                    ? parsedMaximum
                    : 10f;
                if (pixelSpeed <= 3f || activeAmount <= 0f || Math.Abs(maximum - minimum) <= 0.001f)
                    return;

                diagnostics.Add(new TextStudioDiagnostic(
                    TextStudioDiagnosticSeverity.Warning,
                    TextStudioDiagnosticCode.PhotosensitivityRisk,
                    "Pixel Grid pulses more than three times per second. Reduce Pulse Speed, disable active pulsing, or provide a non-flashing variant.",
                    effectId,
                    sourceStart,
                    sourceEnd));
                return;
            }

            if (!parameters.TryGetFloat("frequency", out float frequency) &&
                !parameters.TryGetFloat("f", out frequency))
            {
                return;
            }

            float activity = 1f;
            if (parameters.TryGetFloat("activity", out float parsedActivity))
                activity = parsedActivity;
            bool intermittentGlitch = string.Equals(
                                          resolvedId,
                                          TextStudioBuiltInEffectIds.Glitch,
                                          StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(
                                          resolvedId,
                                          TextStudioBuiltInEffectIds.ChromaticGlitch,
                                          StringComparison.OrdinalIgnoreCase);
            if (frequency <= 3f || (intermittentGlitch && activity < 0.25f))
                return;

            diagnostics.Add(new TextStudioDiagnostic(
                TextStudioDiagnosticSeverity.Warning,
                TextStudioDiagnosticCode.PhotosensitivityRisk,
                "This effect can flash more than three times per second. Reduce Frequency, reduce its active area, or provide a non-flashing variant.",
                effectId,
                sourceStart,
                sourceEnd));
        }

        static TextStudioTagParameters CanonicalizeParametersForDiagnostics(
            string tagName,
            TextStudioTagDefinition definition,
            TextStudioTagParameters parameters)
        {
            if (BuiltInCharacterSubstitutions.TryGetCanonicalTagId(tagName, out string substitutionId))
                return BuiltInCharacterSubstitutions.CanonicalizeParameters(substitutionId, in parameters);

            if (definition.GetNamedParameter("playback") != null)
                return BuiltInPlaybacks.CanonicalizeTextAnimatorKeywordParameters(in parameters);

            return parameters;
        }

        static bool TryGetPhaseWrapperEffect(TextStudioTagParameters parameters, out string value)
        {
            value = string.Empty;
            string raw = parameters.Raw;
            if (string.IsNullOrEmpty(raw))
                return false;

            int index = 0;
            while (index < raw.Length && char.IsWhiteSpace(raw[index]))
                index++;
            if (index >= raw.Length)
                return false;

            int start = index;
            while (index < raw.Length && !char.IsWhiteSpace(raw[index]) && raw[index] != ',')
            {
                if (raw[index] == '=')
                    return false;
                index++;
            }

            if (index <= start)
                return false;

            string candidate = raw.Substring(start, index - start);
            if (!TextStudioPhaseTagUtility.TryResolveEffectId(candidate, out _))
                return false;

            value = candidate;
            return true;
        }

        static bool TryGetParameterValue(
            TextStudioTagParameters parameters,
            TextStudioTagParameterDefinition definition,
            out string value,
            out TextStudioTagParameterOperation operation)
        {
            value = string.Empty;
            operation = TextStudioTagParameterOperation.Set;

            if (TryGetLastNamedParameter(parameters, definition, out TextStudioNamedTagParameter named))
            {
                operation = named.Operation;
                if (operation == TextStudioTagParameterOperation.Multiply)
                {
                    value = named.Value;
                    return true;
                }

                if (named.IsFlag)
                {
                    value = "true";
                    return true;
                }

                if (definition.Kind != TextStudioTagParameterKind.Color)
                {
                    value = named.Value;
                    return true;
                }
            }

            if (definition.Kind == TextStudioTagParameterKind.Color)
            {
                if (TryGetColorParameterString(parameters, definition, out value))
                    return true;

                if (definition.PositionalIndex >= 0 &&
                    parameters.TryGetColorParameterString(definition.PositionalIndex, out value))
                    return true;

                return false;
            }

            if (definition.PositionalIndex >= 0 && parameters.PositionalCount > definition.PositionalIndex)
            {
                value = parameters.GetString(definition.PositionalIndex);
                return true;
            }

            return false;
        }

        static bool TryGetColorParameterString(
            TextStudioTagParameters parameters,
            TextStudioTagParameterDefinition definition,
            out string value)
        {
            if (TryGetLastNamedParameter(parameters, definition, out TextStudioNamedTagParameter named) &&
                !named.IsFlag &&
                named.Operation == TextStudioTagParameterOperation.Set)
            {
                return parameters.TryGetColorParameterString(named.Name, out value);
            }

            value = string.Empty;
            return false;
        }

        static bool TryGetLastNamedParameter(
            TextStudioTagParameters parameters,
            TextStudioTagParameterDefinition definition,
            out TextStudioNamedTagParameter parameter)
        {
            IReadOnlyList<TextStudioNamedTagParameter> named = parameters.Named;
            IReadOnlyList<string> aliases = definition.Aliases;
            for (int i = named.Count - 1; i >= 0; i--)
            {
                TextStudioNamedTagParameter candidate = named[i];
                if (string.Equals(candidate.Name, definition.Name, StringComparison.OrdinalIgnoreCase))
                {
                    parameter = candidate;
                    return true;
                }

                for (int aliasIndex = 0; aliasIndex < aliases.Count; aliasIndex++)
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

        static bool IsColorContinuationFlag(
            TextStudioTagParameters parameters,
            TextStudioTagDefinition definition,
            int namedIndex)
        {
            if (namedIndex <= 0)
                return false;

            IReadOnlyList<TextStudioNamedTagParameter> namedParameters = parameters.Named;
            int previousIndex = namedIndex - 1;
            while (previousIndex >= 0 && namedParameters[previousIndex].IsFlag)
                previousIndex--;
            if (previousIndex < 0)
                return false;

            TextStudioTagParameterDefinition previousDefinition = definition.GetNamedParameter(
                namedParameters[previousIndex].Name);
            return parameters.IsColorContinuationFlag(namedIndex, previousDefinition);
        }

        static void ValidateParameterValue(
            string tagName,
            TextStudioTagParameterDefinition parameter,
            string value,
            TextStudioTagParameterOperation operation,
            bool supportsMultiplication,
            TextStudioTagAnalysisContext context,
            int sourceStart,
            int sourceEnd,
            List<TextStudioDiagnostic> diagnostics)
        {
            if (operation == TextStudioTagParameterOperation.Multiply)
            {
                if (!supportsMultiplication)
                {
                    diagnostics.Add(new TextStudioDiagnostic(
                        TextStudioDiagnosticSeverity.Warning,
                        TextStudioDiagnosticCode.InvalidParameter,
                        "Tag '" + tagName + "' does not support parameter multiplication.",
                        tagName,
                        sourceStart,
                        sourceEnd));
                    return;
                }

                bool numeric = parameter.Kind == TextStudioTagParameterKind.Float ||
                               parameter.Kind == TextStudioTagParameterKind.Integer;
                if (!numeric)
                {
                    diagnostics.Add(new TextStudioDiagnostic(
                        TextStudioDiagnosticSeverity.Warning,
                        TextStudioDiagnosticCode.InvalidParameter,
                        "Parameter '" + parameter.DisplayName + "' does not support multiplication.",
                        tagName,
                        sourceStart,
                        sourceEnd));
                    return;
                }

                if (!float.TryParse(
                        value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float factor) ||
                    float.IsNaN(factor) ||
                    float.IsInfinity(factor))
                {
                    diagnostics.Add(new TextStudioDiagnostic(
                        TextStudioDiagnosticSeverity.Warning,
                        TextStudioDiagnosticCode.InvalidParameter,
                        "Parameter '" + parameter.DisplayName + "' expects a finite numeric multiplication factor.",
                        tagName,
                        sourceStart,
                        sourceEnd));
                }
                return;
            }

            if (!IsParameterKindValid(parameter.Kind, value))
            {
                diagnostics.Add(new TextStudioDiagnostic(
                    TextStudioDiagnosticSeverity.Warning,
                    TextStudioDiagnosticCode.InvalidParameter,
                    "Parameter '" + parameter.DisplayName + "' expects " +
                    GetParameterKindDescription(parameter.Kind) + ".",
                    tagName,
                    sourceStart,
                    sourceEnd));
                return;
            }

            if (!parameter.IsValueAllowed(value) &&
                !IsRegisteredPlaybackValue(parameter, value))
            {
                diagnostics.Add(new TextStudioDiagnostic(
                    TextStudioDiagnosticSeverity.Warning,
                    TextStudioDiagnosticCode.UnsupportedParameterValue,
                    "Parameter '" + parameter.DisplayName + "' does not support value '" + value + "'.",
                    tagName,
                    sourceStart,
                    sourceEnd));
                return;
            }

            if (string.Equals(parameter.Name, "group", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(
                    value,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int groupSize) &&
                groupSize < 1)
            {
                diagnostics.Add(new TextStudioDiagnostic(
                    TextStudioDiagnosticSeverity.Warning,
                    TextStudioDiagnosticCode.UnsupportedParameterValue,
                    "Parameter '" + parameter.DisplayName + "' must be 1 or higher.",
                    tagName,
                    sourceStart,
                    sourceEnd));
                return;
            }

            if (!string.IsNullOrEmpty(parameter.SuggestionKey) &&
                context.TryGetSuggestionValues(parameter.SuggestionKey, out IReadOnlyList<string> values) &&
                values.Count > 0 &&
                !Contains(values, value))
            {
                diagnostics.Add(new TextStudioDiagnostic(
                    TextStudioDiagnosticSeverity.Warning,
                    TextStudioDiagnosticCode.MissingReference,
                    "Referenced value '" + value + "' was not found in the current inspector context.",
                    tagName,
                    sourceStart,
                    sourceEnd));
            }
        }

        static bool SupportsNumericParameterMultiplication(string tagName)
        {
            string effectId = BuiltInStyles.TryResolve(tagName, out string builtInEffectId)
                ? builtInEffectId
                : tagName;
            return TextStudioRegistry.Effects.TryGetQuiet(effectId, out ITextStudioEffectDefinition effect) &&
                   effect is ITextStudioEffectFactory;
        }

        static bool IsParameterKindValid(TextStudioTagParameterKind kind, string value)
        {
            switch (kind)
            {
                case TextStudioTagParameterKind.Float:
                    return float.TryParse(
                        value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out _);
                case TextStudioTagParameterKind.Integer:
                    return int.TryParse(
                        value,
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out _);
                case TextStudioTagParameterKind.Boolean:
                    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(value, "off", StringComparison.OrdinalIgnoreCase);
                case TextStudioTagParameterKind.Color:
                    return TextStudioEffectParameterUtility.TryParseColor(value, out _);
                case TextStudioTagParameterKind.Pivot:
                    return TextStudioPivotParser.TryParse(value, out _);
                case TextStudioTagParameterKind.RouteId:
                case TextStudioTagParameterKind.Enum:
                case TextStudioTagParameterKind.AssetReference:
                case TextStudioTagParameterKind.String:
                default:
                    return !string.IsNullOrEmpty(value);
            }
        }

        static string GetParameterKindDescription(TextStudioTagParameterKind kind)
        {
            switch (kind)
            {
                case TextStudioTagParameterKind.Float:
                    return "a number";
                case TextStudioTagParameterKind.Integer:
                    return "a whole number";
                case TextStudioTagParameterKind.Boolean:
                    return "true or false";
                case TextStudioTagParameterKind.Color:
                    return "an HTML color";
                case TextStudioTagParameterKind.Pivot:
                    return "a named pivot or two coordinates written as x,y";
                case TextStudioTagParameterKind.RouteId:
                    return "a route id";
                case TextStudioTagParameterKind.AssetReference:
                    return "a registered asset id";
                default:
                    return "a " + kind.ToString().ToLowerInvariant() + " value";
            }
        }

        static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static bool IsRegisteredPlaybackValue(TextStudioTagParameterDefinition parameter, string value)
        {
            return string.Equals(parameter.Name, "playback", StringComparison.OrdinalIgnoreCase) &&
                   TextStudioRegistry.Playbacks.TryGetQuiet(value, out IEffectPlayback playback) &&
                   playback != null;
        }

        static int FindTagEnd(string source, int start)
        {
            for (int i = start; i < source.Length; i++)
            {
                if (source[i] == '>') return i;
                if (source[i] == '<') return -1;
            }

            return -1;
        }

        static int FindMalformedEnd(string source, int start)
        {
            for (int i = start; i < source.Length; i++)
            {
                if (source[i] == '<' || source[i] == '>')
                    return i;
            }

            return source.Length;
        }

        static string ExtractBrokenClosingName(string source, int start, int end)
        {
            if (start >= end || source[start] != '/')
                return string.Empty;

            int nameStart = start + 1;
            int nameEnd = nameStart;
            while (nameEnd < end && !char.IsWhiteSpace(source[nameEnd]) && source[nameEnd] != '<' && source[nameEnd] != '>')
                nameEnd++;

            return nameEnd > nameStart ? source.Substring(nameStart, nameEnd - nameStart) : string.Empty;
        }

        static bool HasAppearanceParameter(in TextStudioTagParameters parameters)
        {
            for (int i = 0; i < parameters.NamedCount; i++)
                if (TextStudioAppearanceTagSyntax.IsAdHocParameter(parameters.Named[i].Name)) return true;
            return false;
        }

        readonly struct OpenTag
        {
            public readonly string Name;
            public readonly TextStudioTagKind Kind;
            public readonly string DefinitionId;

            public OpenTag(string name, TextStudioTagKind kind, string definitionId)
            {
                Name = name;
                Kind = kind;
                DefinitionId = definitionId;
            }
        }
    }
}
