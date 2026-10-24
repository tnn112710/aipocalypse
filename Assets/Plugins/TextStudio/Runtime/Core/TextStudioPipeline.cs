using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Orchestrates the full text pipeline: source → tokenize → resolve → segment → layout.
    /// Rebuilds only what's dirty via version-checked stages.
    /// </summary>
    internal sealed partial class TextStudioPipeline
    {
        readonly TextStudioTokenizer _tokenizer = new TextStudioTokenizer();
        readonly TextSegmenter _segmenter = new TextSegmenter();
        readonly StyleResolver _styleResolver = new StyleResolver();
        readonly TextStudioCompatibilityTranslator _compatibilityTranslator =
            new TextStudioCompatibilityTranslator();
        readonly StringBuilder _resolvedBuilder = new StringBuilder(32);
        readonly StringBuilder _displayBuilder = new StringBuilder(32);
        readonly TextStudioRangeLedger _rangeLedger = new TextStudioRangeLedger();
        readonly List<int> _resolvedSourceIndices = new List<int>(8);
        readonly List<int> _displayResolvedIndices = new List<int>(8);
        readonly List<TextStudioTextUnit> _nativeTextAtoms = new List<TextStudioTextUnit>();

        TextStudioCompiledPlan _plan;
        uint _lastSourceVersion;
        // Registry revisions when the current markup compile started reading them; an append state needs them unchanged at its end.
        RegistryRevisions _markupCompileRegistries;
        TextStudioDefaultEffectSettings _defaultEffects;
        TextStudioMarkupSyntax _lastMarkupSyntax;
        int _lastMarkupSyntaxRevision;
        bool _lastRenderMarkupLiterally;
        TextStudioComposedEffectAsset _lastLocalEffect;
        string _lastPreviewSoloLayerId;
        bool _lastProcessedUnitsOnly;
        // Every style input change goes through Invalidate, which advances this revision.
        uint _styleInputRevision;
        PlainTextStyleResolution _plainTextStyles;

        // Pooled token array
        TextToken[] _tokenBuffer;
        int _tokenBufferCapacity;

        // Pooled single-element GlyphRun array
        readonly GlyphRun[] _singleRunBuffer = new GlyphRun[1];

        internal const string LiteralMarkupPrefix = "<noparse>";
        internal const string LiteralMarkupSuffix = "</noparse>";

        public TextStudioCompiledPlan Plan => _plan;
        internal ITextStudioWordBoundaryProvider WordBoundaryProvider
        {
            get => _segmenter.WordBoundaryProvider;
            set
            {
                if (ReferenceEquals(_segmenter.WordBoundaryProvider, value))
                    return;
                _segmenter.WordBoundaryProvider = value;
                Invalidate();
            }
        }
        internal bool LastProcessUsedPlainTextFastPath { get; private set; }
        internal bool LastProcessUsedPlainTextAppendFastPath { get; private set; }
        // Text without markup whose default, transition or supplemental effects resolved without tokenizing.
        internal bool LastProcessUsedPlainTextStylePath { get; private set; }
        // A plain style path text that kept the resolution of the plain text before it (TryKeepPlainTextStyles).
        internal bool LastProcessKeptPlainTextStyles { get; private set; }
        // Parity tests compile through the tokenizer to compare it with the plain text paths.
        internal bool ForceTokenizerForTests { get; set; }
        internal IReadOnlyList<TextStudioCompatibilityDiagnostic> CompatibilityDiagnostics =>
            _compatibilityTranslator.Diagnostics;
        internal bool LastProcessDetectedTMPEffects => _compatibilityTranslator.TMPEffectsDetected;
        internal int LastCompatibilityTranslationCount => _compatibilityTranslator.TranslatedTagCount;

        public TextStudioPipeline()
        {
            _plan = new TextStudioCompiledPlan();
            _tokenizer.ActionTagResolver = _styleResolver.IsActionTag;
        }

        public void SetLocalActions(IReadOnlyList<ITextStudioAction> actions)
        {
            _styleResolver.SetLocalActions(actions);
            Invalidate();
        }

        public void ApplyBehaviorProfile(TextStudioBehaviorProfile profile)
        {
            ApplyDefaultEffects(
                TextStudioDefaultEffectSettings.FromBehaviorProfile(profile));
        }

        public void ApplyDefaultEffects(TextStudioDefaultEffectSettings settings)
        {
            if (_defaultEffects.Equals(settings))
                return;

            _defaultEffects = settings;
            Invalidate();
        }

        internal bool ApplySupplementalEffects(string expression)
        {
            if (!_styleResolver.ApplySupplementalEffects(expression))
                return false;
            Invalidate();
            return true;
        }

        public void ApplyTypewriterTransitionStyle(
            string revealStyle,
            string hideStyle,
            TextStudioDefaultEffectMode mode,
            ITextStudioEffectDefinition revealEffect = null,
            ITextStudioEffectDefinition hideEffect = null)
        {
            if (_styleResolver.ApplyTypewriterTransitionStyle(
                    revealStyle,
                    hideStyle,
                    mode,
                    revealEffect,
                    hideEffect))
                Invalidate();
        }

        /// <summary>
        /// Run the full pipeline on the given source text.
        /// Returns true if anything changed (plan was rebuilt).
        /// </summary>
        public bool Process(ITextStudioTextSource source)
        {
            return Process(
                source,
                null,
                false,
                TextStudioProjectSettings.ActiveMarkupSyntax);
        }

        internal bool Process(
            ITextStudioTextSource source,
            TextStudioComposedEffectAsset localEffect,
            string previewSoloLayerId = null)
        {
            return Process(
                source,
                localEffect,
                false,
                TextStudioProjectSettings.ActiveMarkupSyntax,
                previewSoloLayerId);
        }

        internal bool Process(
            ITextStudioTextSource source,
            bool renderMarkupLiterally)
        {
            return Process(
                source,
                null,
                renderMarkupLiterally,
                TextStudioProjectSettings.ActiveMarkupSyntax);
        }

        internal bool Process(
            ITextStudioTextSource source,
            bool renderMarkupLiterally,
            TextStudioMarkupSyntax markupSyntax)
        {
            return Process(
                source,
                null,
                renderMarkupLiterally,
                markupSyntax);
        }

        internal bool Process(
            ITextStudioTextSource source,
            TextStudioComposedEffectAsset localEffect,
            bool renderMarkupLiterally,
            TextStudioMarkupSyntax markupSyntax,
            string previewSoloLayerId = null,
            bool unitsOnly = false)
        {
            previewSoloLayerId = localEffect != null ? previewSoloLayerId ?? string.Empty : string.Empty;
            uint version = source.GetVersion();
            uint previousSourceVersion = _lastSourceVersion;
            int markupSyntaxRevision = markupSyntax != null ? markupSyntax.Revision : 0;
            if (version == _lastSourceVersion &&
                markupSyntax == _lastMarkupSyntax &&
                markupSyntaxRevision == _lastMarkupSyntaxRevision &&
                renderMarkupLiterally == _lastRenderMarkupLiterally &&
                localEffect == _lastLocalEffect &&
                string.Equals(previewSoloLayerId, _lastPreviewSoloLayerId, StringComparison.Ordinal) &&
                (unitsOnly || !_lastProcessedUnitsOnly) &&
                _plan.CanonicalText != null)
                return false;

            _lastProcessedUnitsOnly = false;
            bool canAppendMarkup = CanProcessAppendedMarkup(
                markupSyntax,
                markupSyntaxRevision,
                renderMarkupLiterally,
                localEffect,
                unitsOnly);
            // Only a markup compile that ends below records an append state again.
            _markupAppend.IsValid = false;
            LastProcessAppendedMarkup = false;
            _segmenter.NoteKeptUnits(null, 0, 0);

            bool previousUsedPlainTextFastPath = LastProcessUsedPlainTextFastPath;
            LastProcessUsedPlainTextFastPath = false;
            LastProcessUsedPlainTextAppendFastPath = false;
            LastProcessUsedPlainTextStylePath = false;
            LastProcessKeptPlainTextStyles = false;
            // Every other path may rewrite the plan's style arrays, so only a resolution recorded by the previous process is kept.
            bool plainTextStylesWereRecorded = _plainTextStyles.IsValid;
            _plainTextStyles.IsValid = false;
            int appendStart = -1;
            bool explicitAppend =
                source is StringTextSource stringSource &&
                stringSource.TryConsumeAppendRange(
                    previousSourceVersion,
                    version,
                    out appendStart);
            // Appended text keeps the segmented units its next append can resume after (TextSegmenter).
            _segmenter.RetainsAppendPrefix = explicitAppend;
            _lastSourceVersion = version;
            _lastMarkupSyntax = markupSyntax;
            _lastMarkupSyntaxRevision = markupSyntaxRevision;
            _lastRenderMarkupLiterally = renderMarkupLiterally;
            _lastLocalEffect = localEffect;
            _lastPreviewSoloLayerId = previewSoloLayerId;
            string canonicalText = source.GetCanonicalText() ?? string.Empty;
            _plan.UnprojectedResolvedCharToResolvedBoundary = null;
            string appendValidatedText = canonicalText;
            if (renderMarkupLiterally)
            {
                BuildLiteralPlan(canonicalText, version);
                return true;
            }

            _plan.IsLiteralMarkup = false;

            if (canAppendMarkup && explicitAppend)
            {
                // CanProcessAppendedMarkup found every registry at the revision the append state recorded.
                _markupCompileRegistries = _markupAppend.Registries;
                if (TryProcessAppendedMarkup(canonicalText, version, appendStart, markupSyntax))
                    return true;
            }

            if (markupSyntax != null)
                canonicalText = markupSyntax.Normalize(canonicalText);
            TextStudioAuthoringProfile profile = TextStudioMarkupSyntax.ResolveProfile(markupSyntax);
            string text = profile == TextStudioAuthoringProfile.Compatibility ||
                          profile == TextStudioAuthoringProfile.Native ||
                          profile == TextStudioAuthoringProfile.Custom
                ? TextStudioTagExpansionProcessor.Expand(canonicalText, TextStudioRegistry.TagExpansions)
                : canonicalText;
            text = _compatibilityTranslator.Translate(text, profile);
            TextStudioTextProjection protectedProjection = GetTextProjection(text);
            bool canTrustAppendStart =
                explicitAppend &&
                ReferenceEquals(text, appendValidatedText);

            PlainTextKind plainText = ForceTokenizerForTests || localEffect != null ||
                protectedProjection != null && !string.Equals(protectedProjection.ResolvedText, text, StringComparison.Ordinal)
                ? PlainTextKind.Markup
                : ClassifyPlainText(text);
            if (plainText != PlainTextKind.Markup)
            {
                if (!ResolvesPlainTextStyles)
                {
                    if (explicitAppend &&
                        previousUsedPlainTextFastPath &&
                        TryBuildPlainTextAppendPlan(
                            text,
                            version,
                            canTrustAppendStart ? appendStart : -1))
                    {
                        LastProcessUsedPlainTextAppendFastPath = true;
                    }
                    else
                    {
                        BuildPlainTextPlan(text, version, plainText == PlainTextKind.Ascii);
                    }

                    LastProcessUsedPlainTextFastPath = true;
                    return true;
                }

                // Default, transition and supplemental effects bind whole glyph ranges, which the resolver derives from the plain plan exactly as from tokens of text without markup. Only the token list stays empty.
                BuildPlainTextPlan(text, version, plainText == PlainTextKind.Ascii);
                LastProcessUsedPlainTextStylePath = true;
                if (unitsOnly)
                {
                    _lastProcessedUnitsOnly = true;
                    return true;
                }

                if (plainTextStylesWereRecorded && TryKeepPlainTextStyles())
                {
                    LastProcessKeptPlainTextStyles = true;
                    return true;
                }

                _styleResolver.Resolve(_plan, _defaultEffects, localEffect, previewSoloLayerId, _rangeLedger);
                RecordPlainTextStyles();
                return true;
            }

            // Stage 1: Tokenize
            // Tokenizing and resolving read the registries, which an append state needs unchanged at the end of this compile.
            _markupCompileRegistries = RegistryRevisions.Capture();
            _tokenizer.Tokenize(text, protectedProjection);
            _plan.CanonicalText = text;
            _plan.SourceVersion = version;
            CopyTokensToPlan(_tokenizer);
            _plan.TokenCount = _tokenizer.TokenCount;

            _rangeLedger.Build(text, _plan.Tokens, _plan.TokenCount, _tokenizer.Names);

            // Stage 2: Resolve text (strip markup, build resolved string)
            ResolveText(text);
            // Glyph change maps compare only units and resolved text, so they skip styles and segments.
            if (unitsOnly)
            {
                _lastProcessedUnitsOnly = true;
                return true;
            }

            // Stage 3: Resolve styles, effects, actions
            _styleResolver.Resolve(_plan, _defaultEffects, localEffect, previewSoloLayerId, _rangeLedger, _tokenizer.Names);

            // Stage 4: Segment into words/lines on first read
            _plan.DeferSegmentation(_segmenter);

            // Appended text can build on this plan when every text transform returned the canonical text itself.
            if (localEffect == null && ReferenceEquals(text, appendValidatedText))
                RecordMarkupAppendState(text, markupSyntax);
            return true;
        }

        enum PlainTextKind : byte
        {
            Markup,
            Ascii,
            Unicode
        }

        // One pass over translated text: a tag or a tokenizer escape (\\ or \>) needs the tokenizer, and only ASCII without carriage returns gives every character its own unit.
        static PlainTextKind ClassifyPlainText(string text)
        {
            bool ascii = true;
            int last = text.Length - 1;
            for (int i = 0; i <= last; i++)
            {
                char value = text[i];
                if (value == '<')
                    return PlainTextKind.Markup;
                if (value == '\\' && i < last && (text[i + 1] == '\\' || text[i + 1] == '>'))
                    return PlainTextKind.Markup;
                if (value > 127 || value == '\r')
                    ascii = false;
            }

            return ascii ? PlainTextKind.Ascii : PlainTextKind.Unicode;
        }

        bool ResolvesPlainTextStyles =>
            _defaultEffects.Mode != TextStudioDefaultEffectMode.None ||
            _styleResolver.HasTypewriterTransitionEffects ||
            _styleResolver.HasSupplementalEffects;

        void BuildPlainTextPlan(string text, uint sourceVersion, bool asciiIdentity)
        {
            _plan.CanonicalText = text;
            _plan.SourceVersion = sourceVersion;
            _plan.Tokens = Array.Empty<TextToken>();
            _plan.TokenCount = 0;
            _plan.ResolvedText = text;
            _plan.DisplayText = text;

            FinalizeUnstyledPlan(text.Length, true, true, asciiIdentity);
        }

        bool TryBuildPlainTextAppendPlan(
            string text,
            uint sourceVersion,
            int appendStart)
        {
            string previousText = _plan.ResolvedText;
            if (appendStart < 0 ||
                previousText == null ||
                _plan.CanonicalText != previousText ||
                _plan.DisplayText != previousText ||
                appendStart != previousText.Length ||
                text.Length < appendStart)
            {
                return false;
            }

            int previousTextLength = previousText.Length;
            uint previousSourceVersion = _plan.SourceVersion;
            _plan.CanonicalText = text;
            _plan.SourceVersion = sourceVersion;
            _plan.Tokens = Array.Empty<TextToken>();
            _plan.TokenCount = 0;
            _plan.ResolvedText = text;
            _plan.DisplayText = text;
            if (!TextStudioTextUnitBuilder.TryAppendAsciiIdentity(
                    _plan,
                    previousTextLength))
            {
                return false;
            }

            ExtendIdentityDisplayCharacterMap(previousTextLength);
            FinalizeUnstyledPlanMetadata(text.Length);
            // ASCII identity units: every character of the text before the append keeps its glyph.
            AppendKeptGlyphCount = previousTextLength;
            AppendBaseSourceVersion = previousSourceVersion;
            return true;
        }

        void BuildLiteralPlan(string canonicalText, uint sourceVersion)
        {
            _plan.IsLiteralMarkup = true;
            _plan.CanonicalText = canonicalText;
            _plan.SourceVersion = sourceVersion;
            _plan.Tokens = Array.Empty<TextToken>();
            _plan.TokenCount = 0;
            _plan.ResolvedText = canonicalText;
            _plan.DisplayText = WrapLiteralMarkupForTMP(canonicalText);

            _displayResolvedIndices.Clear();
            for (int i = 0; i < LiteralMarkupPrefix.Length; i++)
                _displayResolvedIndices.Add(-1);
            for (int i = 0; i < canonicalText.Length; i++)
            {
                _displayResolvedIndices.Add(i);
                if (canonicalText[i] == '<')
                    for (int j = 0; j < LiteralMarkupSuffix.Length + LiteralMarkupPrefix.Length; j++)
                        _displayResolvedIndices.Add(-1);
            }
            for (int i = 0; i < LiteralMarkupSuffix.Length; i++)
                _displayResolvedIndices.Add(-1);

            FinalizeUnstyledPlan(
                canonicalText.Length,
                true,
                false,
                TextStudioTextUnitBuilder.IsAsciiIdentityText(canonicalText, 0));
        }

        void FinalizeUnstyledPlan(
            int sourceLength,
            bool identitySourceMapping,
            bool identityDisplayMapping,
            bool asciiIdentity)
        {
            if (asciiIdentity)
            {
                TextStudioTextUnitBuilder.BuildAsciiIdentity(_plan);
            }
            else
            {
                TextStudioTextUnitBuilder.Build(
                    _plan,
                    identitySourceMapping ? null : _resolvedSourceIndices);
            }

            if (identityDisplayMapping)
                BuildIdentityDisplayCharacterMap(asciiIdentity);
            else
                BuildDisplayCharacterMap();
            FinalizeUnstyledPlanMetadata(sourceLength);
        }

        void FinalizeUnstyledPlanMetadata(int sourceLength)
        {
            _plan.VisibleGlyphCount = _plan.TextUnitCount;
            if (_plan.VisibleGlyphCount > 0)
            {
                _singleRunBuffer[0] = new GlyphRun
                {
                    startIndex = 0,
                    count = _plan.VisibleGlyphCount,
                    sourceStart = 0,
                    sourceEnd = sourceLength,
                    styleIndex = 0
                };
                _plan.GlyphRuns = _singleRunBuffer;
                _plan.GlyphRunCount = 1;
            }
            else
            {
                _plan.GlyphRuns = Array.Empty<GlyphRun>();
                _plan.GlyphRunCount = 0;
            }

            _plan.AppearanceSpanCount = 0;
            _plan.EffectBindingCount = 0;
            _plan.SourceContextRequirements = TextStudioSourceContextRequirements.None;
            _plan.CharacterSubstitutionBindingCount = 0;
            _plan.ActionMarkerCount = 0;
            _plan.ExtensionTagCount = 0;
            _plan.InstantRangeCount = 0;
            _plan.RegionMapCount = 0;
            _plan.CharDecorations = null;
            _plan.DeferSegmentation(_segmenter);
        }

        void BuildIdentityDisplayCharacterMap(bool identityUnits)
        {
            int displayLength = _plan.DisplayText != null
                ? _plan.DisplayText.Length
                : 0;
            if (displayLength <= 0)
            {
                _plan.DisplayCharToResolvedCharIndex = Array.Empty<int>();
                _plan.DisplayCharToTextUnitIndex = Array.Empty<int>();
                return;
            }

            int identityCount = identityUnits ? _plan.IdentityDisplayCount : 0;
            int[] displayCharToResolved = _plan.DisplayCharToResolvedCharIndex;
            if (displayCharToResolved == null ||
                displayCharToResolved.Length < displayLength)
            {
                displayCharToResolved =
                    new int[TextStudioBufferCapacity.ForCount(displayLength)];
                identityCount = 0;
            }

            int[] displayCharToUnit = _plan.DisplayCharToTextUnitIndex;
            if (displayCharToUnit == null ||
                displayCharToUnit.Length < displayLength)
            {
                displayCharToUnit =
                    new int[TextStudioBufferCapacity.ForCount(displayLength)];
                identityCount = 0;
            }

            if (identityUnits)
            {
                // Each character is its own unit, so both maps are the identity and slots that already hold it are kept.
                for (int i = identityCount; i < displayLength; i++)
                {
                    displayCharToResolved[i] = i;
                    displayCharToUnit[i] = i;
                }
            }
            else
            {
                for (int i = 0; i < displayLength; i++)
                {
                    displayCharToResolved[i] = i;
                    displayCharToUnit[i] = _plan.GetUnitIndexForResolvedChar(i);
                }
            }

            _plan.DisplayCharToResolvedCharIndex = displayCharToResolved;
            _plan.DisplayCharToTextUnitIndex = displayCharToUnit;
            _plan.IdentityDisplayCount = identityUnits ? Math.Max(identityCount, displayLength) : 0;
        }

        void ExtendIdentityDisplayCharacterMap(int previousDisplayLength)
        {
            int displayLength = _plan.DisplayText != null
                ? _plan.DisplayText.Length
                : 0;
            if (previousDisplayLength < 0 || previousDisplayLength > displayLength)
            {
                BuildIdentityDisplayCharacterMap(false);
                return;
            }

            bool prefixIsIdentity = _plan.IdentityDisplayCount >= previousDisplayLength;
            int[] displayCharToResolved = _plan.DisplayCharToResolvedCharIndex;
            if (displayCharToResolved == null ||
                displayCharToResolved.Length < displayLength)
            {
                int[] expanded =
                    new int[TextStudioBufferCapacity.ForCount(displayLength)];
                if (displayCharToResolved != null && previousDisplayLength > 0)
                {
                    Array.Copy(
                        displayCharToResolved,
                        expanded,
                        Math.Min(previousDisplayLength, displayCharToResolved.Length));
                }

                displayCharToResolved = expanded;
            }

            int[] displayCharToUnit = _plan.DisplayCharToTextUnitIndex;
            if (displayCharToUnit == null ||
                displayCharToUnit.Length < displayLength)
            {
                int[] expanded =
                    new int[TextStudioBufferCapacity.ForCount(displayLength)];
                if (displayCharToUnit != null && previousDisplayLength > 0)
                {
                    Array.Copy(
                        displayCharToUnit,
                        expanded,
                        Math.Min(previousDisplayLength, displayCharToUnit.Length));
                }

                displayCharToUnit = expanded;
            }

            for (int i = previousDisplayLength; i < displayLength; i++)
            {
                displayCharToResolved[i] = i;
                displayCharToUnit[i] = i;
            }

            _plan.DisplayCharToResolvedCharIndex = displayCharToResolved;
            _plan.DisplayCharToTextUnitIndex = displayCharToUnit;
            _plan.IdentityDisplayCount = prefixIsIdentity ? displayLength : 0;
        }

        internal static string WrapLiteralMarkupForTMP(string canonicalText)
        {
            // TMP scans tag attributes before honoring noparse. End the scan at the
            // next opening bracket so arbitrary literal tags cannot overflow it.
            string protectedText = (canonicalText ?? string.Empty).Replace(
                "<", "<" + LiteralMarkupSuffix + LiteralMarkupPrefix);
            return LiteralMarkupPrefix + protectedText + LiteralMarkupSuffix;
        }

        /// <summary>
        /// Force a full re-process on next call.
        /// </summary>
        public void Invalidate()
        {
            _lastSourceVersion = unchecked(_lastSourceVersion - 1);
            _styleInputRevision = unchecked(_styleInputRevision + 1);
        }

        // Without markup the resolver reads only the unit count, the style inputs, the effect and substitution registries
        // and effect asset content, and writes only the effect bindings, the character substitutions with their region maps
        // and the source context requirements. A plain replacement that keeps the unit count and all of those inputs would
        // resolve the same values into the arrays the previous resolution left in the plan, so it restores their counts.
        struct PlainTextStyleResolution
        {
            internal bool IsValid;
            internal int UnitCount;
            internal uint StyleInputRevision;
            internal uint EffectRegistryRevision;
            internal uint SubstitutionRegistryRevision;
            internal uint EffectContentRevision;
            internal EffectBinding[] EffectBindings;
            internal int EffectBindingCount;
            internal TextStudioSourceContextRequirements SourceContextRequirements;
            internal CharacterSubstitutionBinding[] CharacterSubstitutionBindings;
            internal int CharacterSubstitutionBindingCount;
            internal RegionToGlyphMap[] RegionMaps;
            internal int RegionMapCount;
        }

        void RecordPlainTextStyles()
        {
            if (_plan.AppearanceSpanCount != 0 ||
                _plan.ActionMarkerCount != 0 ||
                _plan.ExtensionTagCount != 0 ||
                _plan.InstantRangeCount != 0 ||
                _plan.CharDecorations != null ||
                !HasReplayableEffectBindings(_plan))
            {
                return;
            }

            _plainTextStyles = new PlainTextStyleResolution
            {
                IsValid = true,
                UnitCount = _plan.VisibleGlyphCount,
                StyleInputRevision = _styleInputRevision,
                EffectRegistryRevision = TextStudioRegistry.Effects.Revision,
                SubstitutionRegistryRevision = TextStudioRegistry.CharacterSubstitutions.Revision,
                EffectContentRevision = TextStudioEffectDefinitionAsset.ContentRevision,
                EffectBindings = _plan.EffectBindings,
                EffectBindingCount = _plan.EffectBindingCount,
                SourceContextRequirements = _plan.SourceContextRequirements,
                CharacterSubstitutionBindings = _plan.CharacterSubstitutionBindings,
                CharacterSubstitutionBindingCount = _plan.CharacterSubstitutionBindingCount,
                RegionMaps = _plan.RegionMaps,
                RegionMapCount = _plan.RegionMapCount
            };
        }

        bool TryKeepPlainTextStyles()
        {
            ref PlainTextStyleResolution kept = ref _plainTextStyles;
            if (kept.UnitCount != _plan.VisibleGlyphCount ||
                kept.StyleInputRevision != _styleInputRevision ||
                kept.EffectRegistryRevision != TextStudioRegistry.Effects.Revision ||
                kept.SubstitutionRegistryRevision != TextStudioRegistry.CharacterSubstitutions.Revision ||
                kept.EffectContentRevision != TextStudioEffectDefinitionAsset.ContentRevision ||
                !ReferenceEquals(kept.EffectBindings, _plan.EffectBindings) ||
                !ReferenceEquals(kept.CharacterSubstitutionBindings, _plan.CharacterSubstitutionBindings) ||
                !ReferenceEquals(kept.RegionMaps, _plan.RegionMaps))
            {
                return false;
            }

            // BuildPlainTextPlan cleared every count; the arrays still hold the recorded resolution.
            _plan.EffectBindingCount = kept.EffectBindingCount;
            _plan.SourceContextRequirements = kept.SourceContextRequirements;
            _plan.CharacterSubstitutionBindingCount = kept.CharacterSubstitutionBindingCount;
            _plan.RegionMapCount = kept.RegionMapCount;
            kept.IsValid = true;
            return true;
        }

        // A resolution creates an evaluator only for a direct effect whose definition is a factory. A kept binding then
        // reuses that evaluator, which matches a new one only for stateless built-in code effects; shared definitions and
        // composition layers are the same objects in every resolution. Writing layers also register their stroke
        // properties while resolving, so they always resolve again.
        static bool HasReplayableEffectBindings(TextStudioCompiledPlan plan)
        {
            for (int i = 0; i < plan.EffectBindingCount; i++)
            {
                ITextStudioEffect effect = plan.EffectBindings[i].effect;
                if (effect == null ||
                    effect is BuiltIns.CodeEffectBase ||
                    effect is TextStudioCompositionLayer layer && layer.Kind != TextStudioCompositionLayerKind.Writing ||
                    effect is ITextStudioEffectDefinition && !(effect is ITextStudioEffectFactory))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        void ResolveText(string source)
        {
            _resolvedBuilder.Clear();
            _displayBuilder.Clear();
            _resolvedSourceIndices.Clear();
            _displayResolvedIndices.Clear();
            _nativeTextAtoms.Clear();
            _hasNativeFormatting = false;
            AppendTokenText(source, 0, _plan.TokenCount, _tokenizer.Names);

            _plan.ResolvedText = _resolvedBuilder.ToString();
            _plan.DisplayText = _hasNativeFormatting ? _displayBuilder.ToString() : _plan.ResolvedText;
            TextStudioTextUnitBuilder.Build(_plan, _resolvedSourceIndices);
            ApplyNativeTextAtoms(0);
            BuildDisplayCharacterMap();
            ApplyTextProjection();
            FinishResolvedGlyphs(source.Length);
        }

        // An inline object occupies one resolved character but retains its complete authored tag span.
        void ApplyNativeTextAtoms(int resolvedFrom)
        {
            for (int i = 0; i < _nativeTextAtoms.Count; i++)
            {
                TextStudioTextUnit atom = _nativeTextAtoms[i];
                if (atom.resolvedStart < resolvedFrom)
                    continue;
                int unitIndex = _plan.GetUnitIndexForResolvedChar(atom.resolvedStart);
                _plan.TextUnits[unitIndex] = new TextStudioTextUnit(
                    unitIndex, atom.resolvedStart, 1, atom.sourceStart, atom.sourceLength);
            }
        }

        void FinishResolvedGlyphs(int sourceLength)
        {
            _plan.VisibleGlyphCount = _plan.TextUnitCount;

            // Build a single glyph run spanning the whole text
            if (_plan.VisibleGlyphCount > 0)
            {
                _singleRunBuffer[0] = new GlyphRun
                {
                    startIndex = 0,
                    count = _plan.VisibleGlyphCount,
                    sourceStart = 0,
                    sourceEnd = sourceLength,
                    styleIndex = 0
                };
                _plan.GlyphRuns = _singleRunBuffer;
                _plan.GlyphRunCount = 1;
            }
            else
            {
                _plan.GlyphRuns = Array.Empty<GlyphRun>();
                _plan.GlyphRunCount = 0;
            }
        }

        // Appends the resolved and display text of tokens [start, end) with their character maps.
        void AppendTokenText(string source, int start, int end, string[] names)
        {
            TextToken[] tokens = _plan.Tokens;
            for (int i = start; i < end; i++)
            {
                ref var token = ref tokens[i];
                switch (token.kind)
                {
                    case TokenKind.TextSpan:
                    case TokenKind.EscapedLiteral:
                    case TokenKind.Whitespace:
                    {
                        int length = token.sourceEnd - token.sourceStart;
                        int resolvedStart = _resolvedBuilder.Length;
                        _resolvedBuilder.Append(source, token.sourceStart, length);
                        _displayBuilder.Append(source, token.sourceStart, length);
                        for (int s = token.sourceStart; s < token.sourceEnd; s++)
                        {
                            _resolvedSourceIndices.Add(s);
                            _displayResolvedIndices.Add(resolvedStart + s - token.sourceStart);
                        }
                        break;
                    }

                    case TokenKind.StyleOpen:
                    case TokenKind.StyleClose:
                    {
                        // Native rich text formatting (underline, strikethrough, bold,
                        // italic, color, …) passes through verbatim so TMP handles
                        // it natively. Decoration runs (u / s) are later
                        // subdivided per-character in the hub's mesh pass so they
                        // follow curved placements without touching this stream.
                        string tagName;
                        if (names != null)
                            tagName = names[i];
                        else
                        {
                            int nameStart = token.sourceStart;
                            int nameEnd = token.nameEnd > token.sourceStart ? token.nameEnd : token.sourceEnd;
                            tagName = source.Substring(nameStart, nameEnd - nameStart).Trim();
                        }
                        TextStudioRangeEntry ownership = _rangeLedger[i];
                        bool isNativeOpen = token.kind == TokenKind.StyleOpen && ownership.Owner == TextStudioRangeOwner.Native;
                        bool orphanNativeClose = token.kind == TokenKind.StyleClose && ownership.Owner == TextStudioRangeOwner.None &&
                            BuiltIns.BuiltInStyles.IsNativeRichTextFormatting(tagName);
                        bool isNativeClose = token.kind == TokenKind.StyleClose &&
                            (ownership.Owner == TextStudioRangeOwner.Native || orphanNativeClose);
                        // Native orphan closers are part of the backend stream even without a source opening.
                        string emittedCloseName = orphanNativeClose ? tagName : ownership.Name;
                        if (isNativeOpen || isNativeClose)
                        {
                            int displayStart = _displayBuilder.Length;
                            _displayBuilder.Append('<');
                            if (token.kind == TokenKind.StyleClose)
                                _displayBuilder.Append('/');
                            if (token.kind == TokenKind.StyleOpen &&
                                TryAppendNormalizedNativeColorTag(_displayBuilder, source, token, tagName))
                            {
                                _hasNativeFormatting = true;
                                _displayBuilder.Append('>');
                                AppendUnmappedDisplayCharacters(displayStart);
                                break;
                            }

                            if (token.kind == TokenKind.StyleClose)
                                _displayBuilder.Append(emittedCloseName);
                            else
                                _displayBuilder.Append(source, token.sourceStart, token.sourceEnd - token.sourceStart);
                            _displayBuilder.Append('>');
                            if (isNativeOpen && IsNativeSprite(tagName))
                            {
                                int resolvedStart = _resolvedBuilder.Length;
                                _resolvedBuilder.Append('\uFFFC');
                                _resolvedSourceIndices.Add(token.outerStart);
                                _nativeTextAtoms.Add(new TextStudioTextUnit(
                                    0, resolvedStart, 1, token.outerStart, token.outerEnd - token.outerStart));
                                AppendDisplayCharacterMapping(displayStart, resolvedStart);
                            }
                            else
                                AppendUnmappedDisplayCharacters(displayStart);
                            _hasNativeFormatting = true;
                        }
                        // Non-native style tags are consumed here - StyleResolver turns
                        // them into effect bindings.
                        break;
                    }

                    // Other markup doesn't contribute to either resolved or display text
                    case TokenKind.EffectTag:
                    case TokenKind.Marker:
                    case TokenKind.Wait:
                    case TokenKind.Action:
                    case TokenKind.ExtensionTag:
                    case TokenKind.ExtensionClose:
                        break;
                }
            }
        }

        void AppendUnmappedDisplayCharacters(int displayStart)
        {
            AppendDisplayCharacterMapping(displayStart, -1);
        }

        void AppendDisplayCharacterMapping(int displayStart, int resolvedIndex)
        {
            int appendedCount = _displayBuilder.Length - displayStart;
            for (int i = 0; i < appendedCount; i++)
                _displayResolvedIndices.Add(resolvedIndex);
        }

        // Display characters before displayFrom keep the entries the build before this one wrote for them.
        void BuildDisplayCharacterMap(int displayFrom = 0)
        {
            int displayLength = _plan.DisplayText != null ? _plan.DisplayText.Length : 0;
            if (displayLength <= 0)
            {
                _plan.DisplayCharToResolvedCharIndex = Array.Empty<int>();
                _plan.DisplayCharToTextUnitIndex = Array.Empty<int>();
                return;
            }

            int[] displayCharToResolved = TextStudioBufferCapacity.EnsureKeeping(_plan.DisplayCharToResolvedCharIndex, displayLength, displayFrom);
            int[] displayCharToUnit = TextStudioBufferCapacity.EnsureKeeping(_plan.DisplayCharToTextUnitIndex, displayLength, displayFrom);

            for (int i = displayFrom; i < displayLength; i++)
            {
                int resolvedIndex = i < _displayResolvedIndices.Count
                    ? _displayResolvedIndices[i]
                    : -1;
                displayCharToResolved[i] = resolvedIndex;
                displayCharToUnit[i] = resolvedIndex >= 0
                    ? _plan.GetUnitIndexForResolvedChar(resolvedIndex)
                    : -1;
            }

            _plan.DisplayCharToResolvedCharIndex = displayCharToResolved;
            _plan.DisplayCharToTextUnitIndex = displayCharToUnit;
            _plan.IdentityDisplayCount = 0;
        }

        internal static bool IsNativeSprite(string tagName)
        {
            return string.Equals(tagName, "sprite", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsNativeRichTextInvocation(
            string source,
            in TextToken token,
            string tagName)
        {
            bool hasParameter = token.parameterStart > token.sourceStart;
            bool usesDirectAssignment = TextStudioAppearanceTagSyntax.UsesDirectAssignment(source, in token);
            if (string.Equals(tagName, "style", StringComparison.OrdinalIgnoreCase) && !usesDirectAssignment)
            {
                TextStudioTagParameters parameters = TextStudioAppearanceTagSyntax.GetParameters(source, in token);
                if (TextStudioAppearanceTagSyntax.HasLegacyNameCollision(tagName) ||
                    TextStudioAppearanceTagSyntax.TryClassify(tagName, false, in parameters, out _, out _)) return false;
            }
            return BuiltIns.BuiltInStyles.IsNativeRichTextFormattingInvocation(tagName, hasParameter, usesDirectAssignment);
        }

        static bool TryAppendNormalizedNativeColorTag(
            StringBuilder builder,
            string source,
            in TextToken token,
            string tagName)
        {
            if (BuiltIns.BuiltInStyles.IsNativeColorShorthand(tagName) &&
                TextStudioColorUtility.TryNormalizeToHex(tagName, out string shorthandHex))
            {
                builder.Append("color=");
                builder.Append(shorthandHex);
                return true;
            }

            if (!IsNativeColorTag(tagName) ||
                token.parameterStart <= 0 ||
                token.parameterEnd <= token.parameterStart)
                return false;

            string rawColor = source.Substring(token.parameterStart, token.parameterEnd - token.parameterStart).Trim();
            if (!TextStudioColorUtility.TryNormalizeToHex(rawColor, out string hex))
                return false;

            builder.Append(tagName);
            builder.Append('=');
            builder.Append(hex);
            return true;
        }

        static bool IsNativeColorTag(string tagName)
        {
            return string.Equals(tagName, "color", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(tagName, "mark", StringComparison.OrdinalIgnoreCase);
        }

        // Tokens before start keep the copy made of them before.
        void CopyTokensToPlan(TextStudioTokenizer tokenizer, int start = 0)
        {
            int count = tokenizer.TokenCount;
            _tokenBuffer = TextStudioBufferCapacity.EnsureKeeping(_tokenBuffer, count, start);

            var source = tokenizer.Tokens;
            for (int i = start; i < count; i++)
                _tokenBuffer[i] = source[i];

            _plan.Tokens = _tokenBuffer;
        }

    }

    /// <summary>
    /// Resolves style references, effect bindings, and action markers from tokens.
    /// </summary>
    internal sealed class StyleResolver
    {
        readonly TextStudioRangeLedger _fallbackRangeLedger = new TextStudioRangeLedger();
        readonly List<TextStudioAppearanceSpan> _appearanceSpans = new List<TextStudioAppearanceSpan>();
        readonly List<EffectBinding> _effectBindings = new List<EffectBinding>();
        readonly List<CharacterSubstitutionBinding> _characterSubstitutionBindings = new List<CharacterSubstitutionBinding>();
        int _explicitCharacterSubstitutionBindingCount;
        readonly List<ActionMarker> _actionMarkers = new List<ActionMarker>();
        readonly List<TextStudioExtensionTag> _extensionTags = new List<TextStudioExtensionTag>();
        readonly List<TextStudioInstantRange> _instantRanges = new List<TextStudioInstantRange>();
        readonly List<RegionToGlyphMap> _regionMaps = new List<RegionToGlyphMap>();
        readonly TextStudioEffectResolver _effectResolver = new TextStudioEffectResolver();
        readonly List<TextStudioResolvedEffectBinding> _resolvedEffectBindings = new List<TextStudioResolvedEffectBinding>();

        // Stack for tracking open style/effect tags with their glyph start positions
        readonly struct OpenTag
        {
            public readonly string name;
            public readonly int glyphStart;
            public readonly int sourceStart;
            public readonly TextStudioTagParameters parameters;
            public readonly AnimationPhase phase;
            public readonly PhaseParams phaseParams;
            public readonly string wrapperName;
            public readonly int wrapperGroup;
            public readonly float revealActiveBlendDuration;

            public OpenTag(string name, int glyphStart)
                : this(name, glyphStart, -1, TextStudioTagParameters.Empty)
            {
            }

            public OpenTag(string name, int glyphStart, int sourceStart)
                : this(name, glyphStart, sourceStart, TextStudioTagParameters.Empty)
            {
            }

            public OpenTag(string name, int glyphStart, int sourceStart, TextStudioTagParameters parameters)
                : this(name, glyphStart, sourceStart, parameters, AnimationPhase.None, PhaseParams.Default, string.Empty, 0)
            {
            }

            public OpenTag(
                string name,
                int glyphStart,
                int sourceStart,
                TextStudioTagParameters parameters,
                AnimationPhase phase,
                PhaseParams phaseParams,
                string wrapperName,
                int wrapperGroup,
                float revealActiveBlendDuration = 0f)
            {
                this.name = name;
                this.glyphStart = glyphStart;
                this.sourceStart = sourceStart;
                this.parameters = parameters.Raw.Length > 0 ||
                                  parameters.PositionalCount > 0 ||
                                  parameters.NamedCount > 0
                    ? parameters
                    : TextStudioTagParameters.Empty;
                this.phase = phase;
                this.phaseParams = phaseParams;
                this.wrapperName = wrapperName ?? string.Empty;
                this.wrapperGroup = wrapperGroup;
                this.revealActiveBlendDuration = revealActiveBlendDuration;
            }
        }

        readonly struct OpenExtensionTag
        {
            public readonly TextStudioExtensionTagDefinition definition;
            public readonly string authoredName;
            public readonly int sourceStart;
            public readonly int glyphStart;
            public readonly TextStudioTagParameters parameters;

            public OpenExtensionTag(
                TextStudioExtensionTagDefinition definition,
                string authoredName,
                int sourceStart,
                int glyphStart,
                TextStudioTagParameters parameters)
            {
                this.definition = definition;
                this.authoredName = authoredName;
                this.sourceStart = sourceStart;
                this.glyphStart = glyphStart;
                this.parameters = parameters;
            }
        }

        readonly List<OpenTag> _openTagStack = new List<OpenTag>();
        readonly List<OpenTag> _characterSubstitutionTagStack = new List<OpenTag>();
        readonly List<OpenTag> _instantTagStack = new List<OpenTag>();
        readonly List<OpenExtensionTag> _openExtensionTagStack = new List<OpenExtensionTag>();
        // Parallel stack for Text Studio custom decoration tags (<uu>, <o>).
        // They're tracked separately from effects so we can OR the bit onto
        // plan.CharDecorations without fighting effect-binding pairing.
        readonly List<OpenTag> _decorationTagStack = new List<OpenTag>();
        readonly List<string> _phaseEffectNames = new List<string>();
        readonly List<TextStudioEffectInvocation> _compiledInvocationBuffer =
            new List<TextStudioEffectInvocation>();
        readonly List<ITextStudioAction> _localActions = new List<ITextStudioAction>();
        TextStudioCompiledEffectInvocationSource _profileDefaultEffects =
            TextStudioCompiledEffectInvocationSource.Empty(
                TextStudioEffectInvocationSourceKind.DefaultAll,
                TextStudioPhaseMask.All);
        TextStudioCompiledEffectInvocationSource _profileRevealEffects =
            TextStudioCompiledEffectInvocationSource.Empty(
                TextStudioEffectInvocationSourceKind.DefaultReveal,
                TextStudioPhaseMask.Reveal);
        TextStudioCompiledEffectInvocationSource _profileActiveEffects =
            TextStudioCompiledEffectInvocationSource.Empty(
                TextStudioEffectInvocationSourceKind.DefaultActive,
                TextStudioPhaseMask.Active);
        TextStudioCompiledEffectInvocationSource _profileHideEffects =
            TextStudioCompiledEffectInvocationSource.Empty(
                TextStudioEffectInvocationSourceKind.DefaultHide,
                TextStudioPhaseMask.Hide);
        TextStudioCompiledEffectInvocationSource _transitionRevealEffects =
            TextStudioCompiledEffectInvocationSource.Empty(
                TextStudioEffectInvocationSourceKind.TypewriterReveal,
                TextStudioPhaseMask.Reveal);
        TextStudioCompiledEffectInvocationSource _transitionHideEffects =
            TextStudioCompiledEffectInvocationSource.Empty(
                TextStudioEffectInvocationSourceKind.TypewriterHide,
                TextStudioPhaseMask.Hide);
        TextStudioDefaultEffectSettings _profileInvocationSource;
        TextStudioCompiledEffectInvocationSource _supplementalEffects =
            TextStudioCompiledEffectInvocationSource.Empty(
                TextStudioEffectInvocationSourceKind.Supplemental,
                TextStudioPhaseMask.All);
        TextStudioDefaultEffectMode _transitionEffectMode = TextStudioDefaultEffectMode.None;
        int _nextPhaseWrapperGroup;
        // Set by the end of a resolution that a text append can extend (TryResolveAppended).
        bool _canResolveAppended;
        // Set while resolving an explicit binding that a full resolution would resolve to other objects, another phase blend
        // group or a missing-entry warning; such text resolves in full.
        bool _bindingBlocksAppend;
        static readonly IComparer<TextStudioExtensionTag> ExtensionTagOrder =
            Comparer<TextStudioExtensionTag>.Create(CompareExtensionTags);
        TextStudioComposedEffectAsset _localEffect;
        string _previewSoloLayerId;

        public bool HasTypewriterTransitionEffects =>
            _transitionEffectMode != TextStudioDefaultEffectMode.None &&
            (_transitionRevealEffects.HasContent || _transitionHideEffects.HasContent);

        internal bool HasSupplementalEffects => _supplementalEffects.HasContent;

        internal bool ApplySupplementalEffects(string expression)
        {
            expression ??= string.Empty;
            if (string.Equals(_supplementalEffects.Expression, expression, StringComparison.Ordinal))
                return false;
            TextStudioCompiledEffectInvocationSource compiled = CompileInvocationSource(
                TextStudioEffectInvocationSourceKind.Supplemental, TextStudioPhaseMask.All, expression);
            if (!compiled.IsValid)
                throw new ArgumentException(compiled.Error, nameof(expression));
            _supplementalEffects = compiled;
            return true;
        }

        public void SetLocalActions(IReadOnlyList<ITextStudioAction> actions)
        {
            _localActions.Clear();
            if (actions == null)
                return;

            for (int i = 0; i < actions.Count; i++)
            {
                ITextStudioAction action = actions[i];
                if (action != null && !_localActions.Contains(action))
                    _localActions.Add(action);
            }
        }

        public bool IsActionTag(string tagName)
        {
            return TryGetLocalActionId(tagName, out _) ||
                   TryGetRegisteredActionId(tagName, out _);
        }

        public bool ApplyTypewriterTransitionStyle(
            string revealStyle,
            string hideStyle,
            TextStudioDefaultEffectMode mode,
            ITextStudioEffectDefinition revealEffect = null,
            ITextStudioEffectDefinition hideEffect = null)
        {
            revealStyle = revealEffect != null ? string.Empty : revealStyle ?? string.Empty;
            hideStyle = hideEffect != null ? string.Empty : hideStyle ?? string.Empty;
            if (mode == TextStudioDefaultEffectMode.None &&
                (revealEffect != null || hideEffect != null))
            {
                mode = TextStudioDefaultEffectMode.Constant;
            }

            if (string.Equals(
                    _transitionRevealEffects.Expression,
                    revealStyle,
                    StringComparison.Ordinal) &&
                string.Equals(
                    _transitionHideEffects.Expression,
                    hideStyle,
                    StringComparison.Ordinal) &&
                ReferenceEquals(_transitionRevealEffects.DirectEffect, revealEffect) &&
                ReferenceEquals(_transitionHideEffects.DirectEffect, hideEffect) &&
                _transitionEffectMode == mode)
            {
                return false;
            }

            _transitionEffectMode = mode;
            _transitionRevealEffects = revealEffect != null
                ? TextStudioCompiledEffectInvocationSource.CompileEffect(
                    TextStudioEffectInvocationSourceKind.TypewriterReveal,
                    TextStudioPhaseMask.Reveal,
                    revealEffect)
                : CompileInvocationSource(
                    TextStudioEffectInvocationSourceKind.TypewriterReveal,
                    TextStudioPhaseMask.Reveal,
                    revealStyle);
            _transitionHideEffects = hideEffect != null
                ? TextStudioCompiledEffectInvocationSource.CompileEffect(
                    TextStudioEffectInvocationSourceKind.TypewriterHide,
                    TextStudioPhaseMask.Hide,
                    hideEffect)
                : CompileInvocationSource(
                    TextStudioEffectInvocationSourceKind.TypewriterHide,
                    TextStudioPhaseMask.Hide,
                    hideStyle);
            return true;
        }

        public void Resolve(
            TextStudioCompiledPlan plan,
            TextStudioDefaultEffectSettings defaultEffects)
        {
            Resolve(plan, defaultEffects, null);
        }

        /// <param name="names">The tokenizer's tag name per token of the plan, or null to read each name from the source.</param>
        internal void Resolve(
            TextStudioCompiledPlan plan,
            TextStudioDefaultEffectSettings defaultEffects,
            TextStudioComposedEffectAsset localEffect,
            string previewSoloLayerId = null,
            TextStudioRangeLedger rangeLedger = null,
            string[] names = null)
        {
            if (rangeLedger == null)
            {
                _fallbackRangeLedger.Build(plan.CanonicalText, plan.Tokens, plan.TokenCount, names);
                rangeLedger = _fallbackRangeLedger;
            }
            _appearanceSpans.Clear();
            _localEffect = localEffect;
            _previewSoloLayerId = previewSoloLayerId;
            _effectBindings.Clear();
            _characterSubstitutionBindings.Clear();
            _actionMarkers.Clear();
            _extensionTags.Clear();
            _instantRanges.Clear();
            _regionMaps.Clear();
            _openTagStack.Clear();
            _characterSubstitutionTagStack.Clear();
            _instantTagStack.Clear();
            _openExtensionTagStack.Clear();
            _decorationTagStack.Clear();
            _phaseEffectNames.Clear();
            _nextPhaseWrapperGroup = 0;
            _bindingBlocksAppend = false;

            // Lazily allocate / resize CharDecorations only if we actually see
            // a decoration tag below. The common case (no <uu>/<o> markers)
            // leaves the array null so the decoration subsystem skips this
            // track entirely.
            DecorationBits[] charDecorations = null;
            int resolvedIndex = ResolveTokens(plan, rangeLedger, names, 0, 0, 0, ref charDecorations);
            FinishResolve(plan, defaultEffects, resolvedIndex, charDecorations, default);
        }

        /// <summary>
        /// Resolves the tokens a text append added from <paramref name="startToken"/> on, after a resolution of this plan that
        /// closed every tag and added no default effects (<see cref="CanResolveAppended"/>), and keeps that resolution's
        /// results for the tokens before. <paramref name="resolvedCharIndex"/> is the resolved text length before
        /// <paramref name="startToken"/>, whose units the append kept. Returns false when the appended tokens open appearance
        /// or decoration scopes, or resolve bindings an append does not keep; the plan then needs a full resolution.
        /// </summary>
        internal bool TryResolveAppended(
            TextStudioCompiledPlan plan,
            TextStudioDefaultEffectSettings defaultEffects,
            TextStudioRangeLedger rangeLedger,
            string[] names,
            int startToken,
            int resolvedCharIndex)
        {
            if (!_canResolveAppended ||
                defaultEffects.Mode != TextStudioDefaultEffectMode.None ||
                HasTypewriterTransitionEffects ||
                HasSupplementalEffects)
            {
                return false;
            }

            AppendedResolutionStart previous = new AppendedResolutionStart(this);
            int phaseBlendGroupState = _effectResolver.PhaseBlendGroupState;
            _canResolveAppended = false;
            DecorationBits[] charDecorations = null;
            int resolvedIndex = ResolveTokens(
                plan,
                rangeLedger,
                names,
                startToken,
                resolvedCharIndex,
                plan.GetUnitBoundaryForUnprojectedResolvedChar(resolvedCharIndex),
                ref charDecorations);
            if (_bindingBlocksAppend || _appearanceSpans.Count > 0 || charDecorations != null)
            {
                // The full resolution that follows then allocates the phase blend groups it would have allocated without this attempt.
                _effectResolver.PhaseBlendGroupState = phaseBlendGroupState;
                return false;
            }

            FinishResolve(plan, defaultEffects, resolvedIndex, null, previous);
            return true;
        }

        // Whether the last resolution of the plan can take a text append (TryResolveAppended).
        internal bool CanResolveAppended => _canResolveAppended;

        // The entry counts before an append's tokens, whose entries the plan arrays hold already.
        readonly struct AppendedResolutionStart
        {
            internal readonly bool IsAppend;
            internal readonly int EffectBindings;
            internal readonly int CharacterSubstitutionBindings;
            internal readonly int ActionMarkers;
            internal readonly int ExtensionTags;
            internal readonly int InstantRanges;
            internal readonly int RegionMaps;

            internal AppendedResolutionStart(StyleResolver resolver)
            {
                IsAppend = true;
                EffectBindings = resolver._effectBindings.Count;
                CharacterSubstitutionBindings = resolver._characterSubstitutionBindings.Count;
                ActionMarkers = resolver._actionMarkers.Count;
                ExtensionTags = resolver._extensionTags.Count;
                InstantRanges = resolver._instantRanges.Count;
                RegionMaps = resolver._regionMaps.Count;
            }
        }

        // Resolves the tokens from startToken on, starting at the resolved character and unit index before it, and returns the unit index after the last token.
        int ResolveTokens(
            TextStudioCompiledPlan plan,
            TextStudioRangeLedger rangeLedger,
            string[] names,
            int startToken,
            int resolvedCharIndex,
            int resolvedIndex,
            ref DecorationBits[] charDecorations)
        {
            int visibleGlyphCount = plan.VisibleGlyphCount;
            string source = plan.CanonicalText;

            for (int i = startToken; i < plan.TokenCount; i++)
            {
                ref var token = ref plan.Tokens[i];

                switch (token.kind)
                {
                    case TokenKind.TextSpan:
                    case TokenKind.EscapedLiteral:
                    case TokenKind.Whitespace:
                        AdvanceResolvedIndex(plan, token.sourceEnd - token.sourceStart, ref resolvedCharIndex, ref resolvedIndex);
                        break;

                    case TokenKind.StyleOpen:
                    {
                        string tagName = names != null ? names[i] : ExtractTagName(source, token);
                        TextStudioRangeEntry ownership = rangeLedger[i];
                        if (ownership.Owner == TextStudioRangeOwner.Appearance)
                        {
                            _appearanceSpans.Add(new TextStudioAppearanceSpan(ownership.AppearanceId, ownership.Parameters,
                                token.outerStart, token.selfClosing ? token.outerEnd - token.outerStart : source.Length - token.outerStart,
                                resolvedIndex, token.selfClosing ? 0 : visibleGlyphCount - resolvedIndex,
                                ownership.ParentAppearanceIndex, ownership.AppearanceIndex + 1, ownership.IsAdHoc));
                            break;
                        }
                        // Native rich text formatting is handled by the backend directly, so we
                        // don't want to open an effect binding for it - those would be
                        // empty no-op regions that still cost a list entry.
                        bool isNativeRichText = ownership.Owner == TextStudioRangeOwner.Native;
                        bool isSelfContainedNative = !ownership.IsRange;
                        if (isNativeRichText)
                        {
                            if (TextStudioPipeline.IsNativeSprite(tagName))
                                AdvanceResolvedIndex(plan, 1, ref resolvedCharIndex, ref resolvedIndex);
                            if (!isSelfContainedNative)
                            {
                                DecorationBits nativeDecoration = ResolveNativeDecorationBit(tagName);
                                if (nativeDecoration != DecorationBits.None)
                                    _decorationTagStack.Add(new OpenTag(tagName, resolvedIndex, token.outerStart));
                            }
                            break;
                        }
                        if (token.selfClosing) break;
                        if (BuiltIns.BuiltInStyles.IsCustomDecorationTag(tagName))
                        {
                            // Custom decoration - tracked on its own stack so it
                            // doesn't turn into an effect binding.
                            _decorationTagStack.Add(new OpenTag(tagName, resolvedIndex, token.outerStart));
                            break;
                        }
                        TextStudioTagParameters parameters = ExtractTagParameters(
                            source,
                            token,
                            SupportsNumericParameterMultiplication(tagName));
                        if (TryOpenPhaseWrapper(tagName, resolvedIndex, token.outerStart, parameters))
                            break;
                        if (BuiltIns.BuiltInCharacterSubstitutions.TryGetCanonicalTagId(tagName, out string substitutionId))
                        {
                            _characterSubstitutionTagStack.Add(new OpenTag(
                                substitutionId,
                                resolvedIndex,
                                token.outerStart,
                                BuiltIns.BuiltInCharacterSubstitutions.CanonicalizeParameters(substitutionId, in parameters)));
                            break;
                        }
                        if (IsInstantTag(tagName))
                        {
                            _instantTagStack.Add(new OpenTag(tagName, resolvedIndex, token.outerStart, parameters));
                            break;
                        }
                        parameters = NormalizeTextAnimatorPlaybackParameters(parameters);
                        _openTagStack.Add(new OpenTag(
                            tagName,
                            resolvedIndex,
                            token.outerStart,
                            parameters,
                            AnimationPhase.None,
                            CreatePersistentPhaseParams(parameters),
                            string.Empty,
                            0));
                        break;
                    }

                    case TokenKind.StyleClose:
                    {
                        TextStudioRangeEntry ownership = rangeLedger[i];
                        if (ownership.OpeningTokenIndex < 0) break;
                        string closeName = ownership.Name;
                        if (ownership.Owner == TextStudioRangeOwner.Appearance)
                        {
                            int spanIndex = ownership.AppearanceIndex;
                            _appearanceSpans[spanIndex] = _appearanceSpans[spanIndex].Close(token.outerEnd, resolvedIndex);
                            break;
                        }
                        if (ownership.Owner == TextStudioRangeOwner.Extension)
                        {
                            ApplyExtensionClose(plan.Tokens[ownership.OpeningTokenIndex].outerStart, token.outerEnd, resolvedIndex);
                            break;
                        }
                        if (ownership.Owner == TextStudioRangeOwner.Native)
                        {
                            DecorationBits bit = ResolveNativeDecorationBit(closeName);
                            if (bit != DecorationBits.None)
                                ApplyDecorationRange(_decorationTagStack, closeName, resolvedIndex, visibleGlyphCount, bit, ref charDecorations);
                            break;
                        }
                        if (BuiltIns.BuiltInStyles.IsCustomDecorationTag(closeName))
                        {
                            ApplyDecorationRange(
                                _decorationTagStack,
                                closeName,
                                resolvedIndex,
                                visibleGlyphCount,
                                BuiltIns.BuiltInStyles.ResolveDecorationBit(closeName),
                                ref charDecorations);
                            break;
                        }
                        if (BuiltIns.BuiltInCharacterSubstitutions.TryGetCanonicalTagId(closeName, out string substitutionId))
                        {
                            ApplyCharacterSubstitutionRange(substitutionId, resolvedIndex);
                            break;
                        }
                        if (IsInstantTag(closeName))
                        {
                            ApplyInstantRange(resolvedIndex);
                            break;
                        }
                        if (TextStudioPhaseTagUtility.IsPhaseTag(closeName))
                        {
                            ApplyPhaseWrapperClose(closeName, resolvedIndex);
                            break;
                        }
                        // Find matching open tag (search from end)
                        for (int j = _openTagStack.Count - 1; j >= 0; j--)
                        {
                            if (string.Equals(_openTagStack[j].name, closeName, StringComparison.OrdinalIgnoreCase))
                            {
                                OpenTag open = _openTagStack[j];
                                _openTagStack.RemoveAt(j);
                                ApplyEffectRange(open, resolvedIndex);
                                break;
                            }
                        }
                        break;
                    }

                    case TokenKind.Wait:
                    case TokenKind.Action:
                    {
                        string actionName = names != null ? names[i] : ExtractTagName(source, token);
                        if (BuiltIns.BuiltInActions.TryGetCanonicalActionId(actionName, out string canonicalActionName))
                            actionName = canonicalActionName;
                        else if (TryGetLocalActionId(actionName, out string canonicalLocalActionName))
                            actionName = canonicalLocalActionName;
                        else if (TryGetRegisteredActionId(actionName, out string canonicalRegisteredActionName))
                            actionName = canonicalRegisteredActionName;
                        TextStudioTagParameters parameters = ExtractTagParameters(source, token);
                        string[] positionalParameters = ExtractParameters(actionName, in parameters);
                        _actionMarkers.Add(new ActionMarker(actionName, resolvedIndex, positionalParameters, parameters));
                        break;
                    }

                    case TokenKind.Marker:
                    {
                        string markerName = names != null ? names[i] : ExtractTagName(source, token);
                        if (BuiltIns.BuiltInActions.TryGetCanonicalActionId(markerName, out string canonicalMarkerName))
                            markerName = canonicalMarkerName;
                        else if (TryGetLocalActionId(markerName, out string canonicalLocalMarkerName))
                            markerName = canonicalLocalMarkerName;
                        else if (TryGetRegisteredActionId(markerName, out string canonicalRegisteredMarkerName))
                            markerName = canonicalRegisteredMarkerName;
                        TextStudioTagParameters parameters = ExtractTagParameters(source, token);
                        string[] positionalParameters = ExtractParameters(markerName, in parameters);
                        _actionMarkers.Add(new ActionMarker(markerName, resolvedIndex, positionalParameters, parameters));
                        break;
                    }

                    case TokenKind.ExtensionTag:
                    {
                        string tagName = names != null ? names[i] : ExtractTagName(source, token);
                        if (!TextStudioTagUtility.TryGetExtensionTag(tagName, token.parameterStart > 0, out TextStudioExtensionTagDefinition definition))
                            break;

                        TextStudioTagParameters parameters = ExtractTagParameters(source, token);
                        if (definition.Shape == TextStudioExtensionTagShape.Range && !token.selfClosing)
                        {
                            _openExtensionTagStack.Add(new OpenExtensionTag(
                                definition,
                                tagName,
                                token.outerStart,
                                resolvedIndex,
                                parameters));
                        }
                        else
                        {
                            _extensionTags.Add(new TextStudioExtensionTag(
                                definition,
                                tagName,
                                token.outerStart,
                                token.outerEnd,
                                resolvedIndex,
                                0,
                                TextStudioExtensionTagBoundary.Point,
                                parameters));
                        }
                        break;
                    }

                    case TokenKind.ExtensionClose:
                    {
                        TextStudioRangeEntry ownership = rangeLedger[i];
                        if (ownership.OpeningTokenIndex >= 0)
                            ApplyExtensionClose(plan.Tokens[ownership.OpeningTokenIndex].outerStart, token.outerEnd, resolvedIndex);
                        break;
                    }
                }
            }

            return resolvedIndex;
        }

        // Closes the tags the end of the text left open, adds the default effects and hands every list to the plan. The plan
        // arrays of an append hold the entries before it already.
        void FinishResolve(
            TextStudioCompiledPlan plan,
            TextStudioDefaultEffectSettings defaultEffects,
            int resolvedIndex,
            DecorationBits[] charDecorations,
            AppendedResolutionStart previous)
        {
            int visibleGlyphCount = plan.VisibleGlyphCount;
            string source = plan.CanonicalText;
            bool openAtTextEnd =
                _openTagStack.Count > 0 ||
                _characterSubstitutionTagStack.Count > 0 ||
                _instantTagStack.Count > 0 ||
                _decorationTagStack.Count > 0 ||
                _openExtensionTagStack.Count > 0;
            ApplyRemainingEffectRanges(resolvedIndex);
            ApplyRemainingCharacterSubstitutionRanges(resolvedIndex);
            ApplyRemainingInstantRanges(resolvedIndex);
            ApplyRemainingDecorationRanges(resolvedIndex, visibleGlyphCount, ref charDecorations);
            ApplyRemainingExtensionRanges(source.Length, resolvedIndex);
            int explicitEffectBindingCount = _effectBindings.Count;
            _explicitCharacterSubstitutionBindingCount = _characterSubstitutionBindings.Count;
            ApplyProfileDefaultEffects(plan, defaultEffects, explicitEffectBindingCount);
            ApplyTypewriterTransitionDefaultEffects(plan, explicitEffectBindingCount);
            // Supplemental layers must not change the fallback coverage of authored defaults.
            ApplyDefaultEffectSource(plan, _supplementalEffects, TextStudioDefaultEffectMode.Constant, explicitEffectBindingCount);

            if (_appearanceSpans.Count > 0 && (plan.AppearanceSpans == null || plan.AppearanceSpans.Length < _appearanceSpans.Count))
                plan.AppearanceSpans = new TextStudioAppearanceSpan[TextStudioBufferCapacity.ForCount(_appearanceSpans.Count)];
            for (int i = 0; i < _appearanceSpans.Count; i++) plan.AppearanceSpans[i] = _appearanceSpans[i];
            plan.AppearanceSpanCount = _appearanceSpans.Count;

            // Copy to plan arrays (reuse when possible)
            int bindingStart = previous.EffectBindings;
            if (previous.IsAppend)
                plan.EffectBindings = KeepForAppend(plan.EffectBindings, _effectBindings.Count, bindingStart);
            else if (_effectBindings.Count > 0 &&
                     (plan.EffectBindings == null || plan.EffectBindings.Length < _effectBindings.Count))
                plan.EffectBindings = new EffectBinding[Math.Max(_effectBindings.Count, 8)];
            for (int i = bindingStart; i < _effectBindings.Count; i++)
                plan.EffectBindings[i] = _effectBindings[i];
            plan.EffectBindingCount = _effectBindings.Count;
            if (!previous.IsAppend)
                plan.SourceContextRequirements = TextStudioSourceContextRequirements.None;
            for (int i = bindingStart; i < _effectBindings.Count; i++)
            {
                EffectBinding binding = _effectBindings[i];
                TextStudioEffectScopeChain scope = binding.scope;
                if (scope != null)
                    plan.SourceContextRequirements |= scope.Requirements;
                if (RequiresSourceTextBounds(in binding))
                {
                    plan.SourceContextRequirements |=
                        TextStudioSourceContextRequirements.SourceTextNormalizedPosition;
                }
            }

            plan.CharacterSubstitutionBindings = CopyToPlan(
                _characterSubstitutionBindings, plan.CharacterSubstitutionBindings, previous.CharacterSubstitutionBindings, 4, previous.IsAppend);
            plan.CharacterSubstitutionBindingCount = _characterSubstitutionBindings.Count;
            plan.ActionMarkers = CopyToPlan(_actionMarkers, plan.ActionMarkers, previous.ActionMarkers, 8, previous.IsAppend);
            plan.ActionMarkerCount = _actionMarkers.Count;

            // Appended tags sort after every earlier one: they start at or after its glyphs and after its source.
            if (previous.IsAppend)
                _extensionTags.Sort(previous.ExtensionTags, _extensionTags.Count - previous.ExtensionTags, ExtensionTagOrder);
            else
                _extensionTags.Sort(CompareExtensionTags);
            plan.ExtensionTags = CopyToPlan(_extensionTags, plan.ExtensionTags, previous.ExtensionTags, 8, previous.IsAppend);
            plan.ExtensionTagCount = _extensionTags.Count;
            plan.InstantRanges = CopyToPlan(_instantRanges, plan.InstantRanges, previous.InstantRanges, 4, previous.IsAppend);
            plan.InstantRangeCount = _instantRanges.Count;
            plan.RegionMaps = CopyToPlan(_regionMaps, plan.RegionMaps, previous.RegionMaps, 8, previous.IsAppend);
            plan.RegionMapCount = _regionMaps.Count;

            // Hand the collected decoration bits to the plan. Null when the
            // source contained no custom decoration tags - a cheap signal for
            // the decoration subsystem to skip this track.
            plan.CharDecorations = charDecorations;

            // An append keeps these results only while nothing it adds can reach back into them: every tag is closed, no default
            // effect spans the whole text, and no binding would resolve to other objects or warn again when resolved anew.
            _canResolveAppended =
                !openAtTextEnd &&
                !_bindingBlocksAppend &&
                _appearanceSpans.Count == 0 &&
                charDecorations == null &&
                _effectBindings.Count == explicitEffectBindingCount &&
                _characterSubstitutionBindings.Count == _explicitCharacterSubstitutionBindingCount &&
                _localEffect == null;
        }

        // Grows an append's plan array, keeping its entries before start; like a full resolution, no entries leave it as it is.
        static T[] KeepForAppend<T>(T[] array, int count, int start)
        {
            return count > (array != null ? array.Length : 0)
                ? TextStudioBufferCapacity.EnsureKeeping(array, count, start)
                : array;
        }

        // Copies the list to the plan array from start on. A full resolution sizes a new array as before; an append keeps the entries before start.
        static T[] CopyToPlan<T>(List<T> list, T[] array, int start, int minimumCapacity, bool append)
        {
            int count = list.Count;
            if (append)
                array = KeepForAppend(array, count, start);
            else if (count > 0 && (array == null || array.Length < count))
                array = new T[Math.Max(count, minimumCapacity)];
            for (int i = start; i < count; i++)
                array[i] = list[i];
            return array;
        }

        static void AdvanceResolvedIndex(
            TextStudioCompiledPlan plan,
            int resolvedCharCount,
            ref int resolvedCharIndex,
            ref int resolvedUnitIndex)
        {
            if (resolvedCharCount <= 0)
                return;

            resolvedCharIndex += resolvedCharCount;
            resolvedUnitIndex = plan != null
                ? plan.GetUnitBoundaryForUnprojectedResolvedChar(resolvedCharIndex)
                : resolvedCharIndex;
        }

        void ApplyProfileDefaultEffects(
            TextStudioCompiledPlan plan,
            TextStudioDefaultEffectSettings defaultEffects,
            int explicitEffectBindingCount)
        {
            if (defaultEffects.Mode == TextStudioDefaultEffectMode.None ||
                plan.VisibleGlyphCount <= 0)
            {
                return;
            }

            RebuildProfileInvocationLists(defaultEffects);
            ApplyDefaultEffectSource(
                plan,
                _profileDefaultEffects,
                defaultEffects.Mode,
                explicitEffectBindingCount);
            ApplyDefaultEffectSource(
                plan,
                _profileRevealEffects,
                defaultEffects.Mode,
                explicitEffectBindingCount);
            ApplyDefaultEffectSource(
                plan,
                _profileActiveEffects,
                defaultEffects.Mode,
                explicitEffectBindingCount);
            ApplyDefaultEffectSource(
                plan,
                _profileHideEffects,
                defaultEffects.Mode,
                explicitEffectBindingCount);
        }

        void ApplyTypewriterTransitionDefaultEffects(
            TextStudioCompiledPlan plan,
            int explicitEffectBindingCount)
        {
            if (_transitionEffectMode == TextStudioDefaultEffectMode.None ||
                plan.VisibleGlyphCount <= 0)
            {
                return;
            }

            ApplyDefaultEffectSource(
                plan,
                _transitionRevealEffects,
                _transitionEffectMode,
                explicitEffectBindingCount);
            ApplyDefaultEffectSource(
                plan,
                _transitionHideEffects,
                _transitionEffectMode,
                explicitEffectBindingCount);
        }

        void RebuildProfileInvocationLists(TextStudioDefaultEffectSettings defaultEffects)
        {
            if (_profileInvocationSource.Equals(defaultEffects))
                return;

            _profileInvocationSource = defaultEffects;
            _profileDefaultEffects = CompileInvocationSource(
                TextStudioEffectInvocationSourceKind.DefaultAll,
                TextStudioPhaseMask.All,
                defaultEffects.AllPhaseEffects);
            _profileRevealEffects = CompileInvocationSource(
                TextStudioEffectInvocationSourceKind.DefaultReveal,
                TextStudioPhaseMask.Reveal,
                defaultEffects.RevealEffects);
            _profileActiveEffects = CompileInvocationSource(
                TextStudioEffectInvocationSourceKind.DefaultActive,
                TextStudioPhaseMask.Active,
                defaultEffects.ActiveEffects);
            _profileHideEffects = CompileInvocationSource(
                TextStudioEffectInvocationSourceKind.DefaultHide,
                TextStudioPhaseMask.Hide,
                defaultEffects.HideEffects);
        }

        TextStudioCompiledEffectInvocationSource CompileInvocationSource(
            TextStudioEffectInvocationSourceKind kind,
            TextStudioPhaseMask phaseMask,
            string expression)
        {
            return TextStudioCompiledEffectInvocationSource.Compile(
                kind,
                phaseMask,
                expression,
                _compiledInvocationBuffer);
        }

        void ApplyDefaultEffectSource(
            TextStudioCompiledPlan plan,
            TextStudioCompiledEffectInvocationSource source,
            TextStudioDefaultEffectMode mode,
            int explicitEffectBindingCount)
        {
            if (source == null || !source.HasContent)
                return;

            if (mode == TextStudioDefaultEffectMode.Constant)
            {
                AddDefaultEffectRange(
                    source,
                    0,
                    plan.VisibleGlyphCount);
                return;
            }

            int rangeStart = -1;
            for (int glyphIndex = 0; glyphIndex < plan.VisibleGlyphCount; glyphIndex++)
            {
                bool covered = HasExplicitEffectAtGlyph(
                    glyphIndex,
                    source.PhaseMask,
                    explicitEffectBindingCount);
                if (!covered)
                {
                    if (rangeStart < 0)
                        rangeStart = glyphIndex;
                    continue;
                }

                if (rangeStart >= 0)
                {
                    AddDefaultEffectRange(
                        source,
                        rangeStart,
                        glyphIndex - rangeStart);
                    rangeStart = -1;
                }
            }

            if (rangeStart >= 0)
            {
                AddDefaultEffectRange(
                    source,
                    rangeStart,
                    plan.VisibleGlyphCount - rangeStart);
            }
        }

        void AddDefaultEffectRange(
            TextStudioCompiledEffectInvocationSource source,
            int glyphStart,
            int glyphCount)
        {
            if (glyphCount <= 0)
                return;

            if (source.HasDirectEffect)
            {
                _resolvedEffectBindings.Clear();
                _effectResolver.TryResolve(
                    source.DirectEffect,
                    source.PhaseMask,
                    _resolvedEffectBindings,
                    null);
                AppendResolvedEffectRange(glyphStart, glyphCount);
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                TextStudioEffectInvocation invocation = source[i];
                TextStudioPhaseMask phaseMask = IntersectPhaseMasks(
                    source.PhaseMask,
                    invocation.PhaseMask);
                if (phaseMask == TextStudioPhaseMask.None)
                    continue;

                // Character substitutions own presentation glyphs rather than animation effect bindings.
                // Only all-phase defaults have the same lifetime as an inline substitution tag.
                if (phaseMask == TextStudioPhaseMask.All &&
                    BuiltIns.BuiltInCharacterSubstitutions.TryGetCanonicalTagId(invocation.EffectId, out string substitutionId))
                {
                    TextStudioTagParameters parameters = BuiltIns.BuiltInCharacterSubstitutions.CanonicalizeParameters(
                        substitutionId, in invocation.Parameters);
                    ApplyCharacterSubstitutionRange(
                        new OpenTag(substitutionId, glyphStart, 0, parameters),
                        glyphStart + glyphCount);
                    continue;
                }

                _resolvedEffectBindings.Clear();
                _effectResolver.TryResolve(
                    invocation.EffectId,
                    invocation.Parameters,
                    phaseMask,
                    _resolvedEffectBindings,
                    null,
                    invocation.Intensity,
                    invocation.TimeOffset,
                    _localEffect,
                    _previewSoloLayerId);
                AppendResolvedEffectRange(glyphStart, glyphCount);
            }
        }

        void AppendResolvedEffectRange(int glyphStart, int glyphCount)
        {
            for (int i = 0; i < _resolvedEffectBindings.Count; i++)
            {
                TextStudioResolvedEffectBinding resolved = _resolvedEffectBindings[i];
                _effectBindings.Add(new EffectBinding
                {
                    effectId = resolved.EffectId,
                    effect = resolved.Effect,
                    glyphStart = glyphStart,
                    glyphCount = glyphCount,
                    phase = AnimationPhase.None,
                    phaseMask = resolved.PhaseMask,
                    blendMode = resolved.BlendMode,
                    phaseParams = resolved.HasAuthoringOverrides
                        ? resolved.PhaseParams
                        : ApplyStaggerParameters(PhaseParams.NoOffset, resolved.Parameters),
                    intensity = resolved.Intensity,
                    timeOffset = resolved.TimeOffset,
                    parameters = resolved.Parameters,
                    area = resolved.HasAuthoringOverrides
                        ? resolved.Area
                        : CreateAreaSettings(resolved.EffectId, resolved.Parameters),
                    sequence = resolved.Sequence,
                    phaseBlendGroupId = resolved.PhaseBlendGroupId,
                    revealActiveBlendDuration = resolved.RevealActiveBlendDuration,
                    revealActiveBlendPhaseMask = resolved.RevealActiveBlendPhaseMask,
                    revealActiveBlendTimeOffset = resolved.RevealActiveBlendTimeOffset,
                    scope = resolved.Scope,
                    selectorScope = resolved.SelectorScope,
                    envelope = resolved.Envelope
                });
            }
        }

        bool HasExplicitEffectAtGlyph(int glyphIndex, TextStudioPhaseMask phaseMask, int explicitEffectBindingCount)
        {
            for (int i = 0; i < _explicitCharacterSubstitutionBindingCount; i++)
            {
                CharacterSubstitutionBinding binding = _characterSubstitutionBindings[i];
                if (glyphIndex >= binding.glyphStart && glyphIndex < binding.glyphStart + binding.glyphCount)
                    return true;
            }

            for (int i = 0; i < explicitEffectBindingCount; i++)
            {
                EffectBinding binding = _effectBindings[i];
                if (glyphIndex < binding.glyphStart ||
                    glyphIndex >= binding.glyphStart + binding.glyphCount)
                {
                    continue;
                }

                TextStudioPhaseMask bindingMask = binding.phase != AnimationPhase.None
                    ? ToPhaseMask(binding.phase)
                    : NormalizePhaseMask(binding.phaseMask);
                if ((bindingMask & phaseMask) != 0)
                    return true;
            }

            return false;
        }

        static TextStudioPhaseMask IntersectPhaseMasks(TextStudioPhaseMask a, TextStudioPhaseMask b)
        {
            return NormalizePhaseMask(a) & NormalizePhaseMask(b);
        }

        static TextStudioPhaseMask NormalizePhaseMask(TextStudioPhaseMask mask)
        {
            return mask == TextStudioPhaseMask.None ? TextStudioPhaseMask.All : mask;
        }

        void ApplyRemainingEffectRanges(int resolvedIndex)
        {
            for (int i = 0; i < _openTagStack.Count; i++)
                ApplyEffectRange(_openTagStack[i], resolvedIndex);

            _openTagStack.Clear();
        }

        void ApplyExtensionClose(int sourceStart, int sourceEnd, int resolvedIndex)
        {
            for (int i = _openExtensionTagStack.Count - 1; i >= 0; i--)
            {
                OpenExtensionTag open = _openExtensionTagStack[i];
                if (open.sourceStart != sourceStart) continue;
                _openExtensionTagStack.RemoveAt(i);
                _extensionTags.Add(new TextStudioExtensionTag(open.definition, open.authoredName,
                    open.sourceStart, sourceEnd, open.glyphStart, Mathf.Max(0, resolvedIndex - open.glyphStart),
                    TextStudioExtensionTagBoundary.Range, open.parameters));
                return;
            }
        }

        bool TryOpenPhaseWrapper(
            string tagName,
            int resolvedIndex,
            int sourceStart,
            TextStudioTagParameters parameters)
        {
            if (!TextStudioPhaseTagUtility.TryGetPhase(tagName, out AnimationPhase phase))
                return false;

            CollectPhaseEffectNames(parameters);
            if (_phaseEffectNames.Count == 0)
                return true;

            float revealActiveBlendDuration = phase == AnimationPhase.Appearance
                ? GetRevealActiveBlendDuration(parameters)
                : 0f;
            TextStudioTagParameters effectParameters = phase == AnimationPhase.Appearance
                ? RemoveNamedParameter(parameters, "blend")
                : parameters;
            PhaseParams phaseParams = CreatePhaseParams(effectParameters);
            int group = ++_nextPhaseWrapperGroup;
            string wrapperName = TextStudioPhaseTagUtility.GetTagName(phase);
            for (int i = 0; i < _phaseEffectNames.Count; i++)
            {
                _openTagStack.Add(new OpenTag(
                    _phaseEffectNames[i],
                    resolvedIndex,
                    sourceStart,
                    effectParameters,
                    phase,
                    phaseParams,
                    wrapperName,
                    group,
                    revealActiveBlendDuration));
            }

            return true;
        }

        static float GetRevealActiveBlendDuration(TextStudioTagParameters parameters)
        {
            const float defaultDuration = 1f;
            const float maximumDuration = 5f;
            if (!parameters.TryGetFloat("blend", out float duration) ||
                float.IsNaN(duration) ||
                float.IsInfinity(duration))
            {
                return defaultDuration;
            }

            return Mathf.Clamp(duration, 0f, maximumDuration);
        }

        static TextStudioTagParameters RemoveNamedParameter(
            TextStudioTagParameters parameters,
            string parameterName)
        {
            if (!parameters.TryGetNamedParameter(parameterName, out _))
                return parameters;

            List<TextStudioNamedTagParameter> filtered =
                new List<TextStudioNamedTagParameter>(parameters.NamedCount);
            for (int i = 0; i < parameters.NamedCount; i++)
            {
                TextStudioNamedTagParameter parameter = parameters.Named[i];
                if (!string.Equals(parameter.Name, parameterName, StringComparison.OrdinalIgnoreCase))
                    filtered.Add(parameter);
            }

            return TextStudioEffectParameterUtility.CreateCanonicalParameters(
                parameters.Positional,
                filtered);
        }

        void CollectPhaseEffectNames(TextStudioTagParameters parameters)
        {
            _phaseEffectNames.Clear();
            if (parameters.PositionalCount > 0)
            {
                for (int i = 0; i < parameters.PositionalCount; i++)
                    AddPhaseEffectNames(parameters.GetString(i));
                return;
            }

            string raw = parameters.Raw;
            if (string.IsNullOrEmpty(raw))
                return;

            int index = 0;
            while (index < raw.Length)
            {
                while (index < raw.Length && (char.IsWhiteSpace(raw[index]) || raw[index] == ','))
                    index++;
                if (index >= raw.Length)
                    break;

                int tokenStart = index;
                while (index < raw.Length && !char.IsWhiteSpace(raw[index]))
                {
                    if (raw[index] == '=')
                        break;
                    index++;
                }

                if (index < raw.Length && raw[index] == '=')
                    break;

                AddPhaseEffectNames(raw.Substring(tokenStart, index - tokenStart));
            }
        }

        void AddPhaseEffectNames(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            int start = 0;
            for (int i = 0; i <= value.Length; i++)
            {
                if (i < value.Length && value[i] != ',')
                    continue;

                string effectName = value.Substring(start, i - start).Trim();
                if (TextStudioPhaseTagUtility.TryResolveEffectId(effectName, out string effectId))
                    _phaseEffectNames.Add(effectId);
                start = i + 1;
            }
        }

        static PhaseParams CreatePhaseParams(TextStudioTagParameters parameters)
        {
            PhaseParams phaseParams = PhaseParams.Default;
            if (parameters.TryGetFloat("charOffset", out float charOffset))
                phaseParams.charOffset = Mathf.Max(0f, charOffset);
            if (parameters.TryGetFloat("wordOffset", out float wordOffset))
                phaseParams.wordOffset = Mathf.Max(0f, wordOffset);
            if (parameters.TryGetFloat("lineOffset", out float lineOffset))
                phaseParams.lineOffset = Mathf.Max(0f, lineOffset);
            if (parameters.TryGetFloat("speed", out float speed))
                phaseParams.speed = Mathf.Max(0.01f, speed);
            return phaseParams;
        }

        static PhaseParams CreatePersistentPhaseParams(TextStudioTagParameters parameters)
        {
            return ApplyStaggerParameters(PhaseParams.NoOffset, parameters);
        }

        static PhaseParams ApplyStaggerParameters(
            PhaseParams phaseParams,
            TextStudioTagParameters parameters)
        {
            if (parameters.TryGetFloat("charOffset", out float charOffset))
                phaseParams.charOffset = Mathf.Max(0f, charOffset);
            if (parameters.TryGetFloat("wordOffset", out float wordOffset))
                phaseParams.wordOffset = Mathf.Max(0f, wordOffset);
            if (parameters.TryGetFloat("lineOffset", out float lineOffset))
                phaseParams.lineOffset = Mathf.Max(0f, lineOffset);
            return phaseParams;
        }

        TextStudioEffectAreaSettings CreateAreaSettings(
            string effectId,
            TextStudioTagParameters parameters)
        {
            TextStudioEffectAreaSettings area = GetDefaultArea(effectId);

            if (parameters.TryGetString("area", out string rawArea) &&
                TryParseAreaMode(rawArea, out TextStudioEffectAreaMode parsedMode))
            {
                area = new TextStudioEffectAreaSettings(parsedMode, area.GroupSize);
            }

            if (parameters.TryGetInt("group", out int groupSize))
                area = new TextStudioEffectAreaSettings(area.Mode, Mathf.Max(1, groupSize));

            if (area.Mode == TextStudioEffectAreaMode.Range)
                area = TextStudioEffectAreaSettings.Range;

            return area;
        }

        TextStudioEffectAreaSettings GetDefaultArea(string effectId)
        {
            if (TextStudioRegistry.Effects.TryGet(effectId, out ITextStudioEffectDefinition effect) && effect != null)
                return effect.DefaultArea;
            // The lookup warns about the missing entry, which a full resolution of the next append would repeat.
            _bindingBlocksAppend = true;
            return TextStudioEffectAreaSettings.Default;
        }

        static bool TryParseAreaMode(string value, out TextStudioEffectAreaMode mode)
        {
            mode = TextStudioEffectAreaMode.Glyph;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value.Trim().ToLowerInvariant())
            {
                case "glyph":
                case "glyphs":
                case "character":
                case "characters":
                case "char":
                case "chars":
                    mode = TextStudioEffectAreaMode.Glyph;
                    return true;
                case "word":
                case "words":
                    mode = TextStudioEffectAreaMode.Word;
                    return true;
                case "line":
                case "lines":
                    mode = TextStudioEffectAreaMode.Line;
                    return true;
                case "paragraph":
                case "paragraphs":
                case "para":
                    mode = TextStudioEffectAreaMode.Paragraph;
                    return true;
                case "range":
                case "tag":
                case "tagged":
                    mode = TextStudioEffectAreaMode.Range;
                    return true;
                default:
                    return false;
            }
        }

        void ApplyPhaseWrapperClose(string closeName, int resolvedIndex)
        {
            string wrapperName = TextStudioPhaseTagUtility.TryGetPhase(closeName, out AnimationPhase phase)
                ? TextStudioPhaseTagUtility.GetTagName(phase)
                : closeName;
            int group = 0;
            for (int i = _openTagStack.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(_openTagStack[i].wrapperName, wrapperName, StringComparison.OrdinalIgnoreCase))
                    continue;

                group = _openTagStack[i].wrapperGroup;
                break;
            }

            if (group == 0)
                return;

            for (int i = _openTagStack.Count - 1; i >= 0; i--)
            {
                OpenTag open = _openTagStack[i];
                if (open.wrapperGroup != group)
                    continue;

                _openTagStack.RemoveAt(i);
                ApplyEffectRange(open, resolvedIndex);
            }
        }

        void ApplyEffectRange(OpenTag open, int resolvedIndex)
        {
            int glyphCount = resolvedIndex - open.glyphStart;
            if (glyphCount <= 0)
                return;

            string effectId = BuiltIns.BuiltInStyles.TryResolve(open.name, out string builtInEffectId)
                ? builtInEffectId
                : open.name;
            if (!TextStudioEffectInvocationParser.TryExtractInvocationControls(
                    effectId,
                    in open.parameters,
                    out TextStudioTagParameters effectParameters,
                    out TextStudioPhaseMask invocationPhases,
                    out float intensity,
                    out float timeOffset,
                    out bool hasRange,
                    out TextStudioTextRange selectionRange,
                    out _,
                    _localEffect))
                return;
            TextStudioPhaseMask phases = IntersectPhaseMasks(ToPhaseMask(open.phase), invocationPhases);
            if (phases == TextStudioPhaseMask.None)
                return;
            int selectedStart = open.glyphStart;
            int selectedCount = glyphCount;
            if (hasRange)
            {
                selectionRange.Resolve(glyphCount, out int localStart, out int localEnd);
                selectedStart += localStart;
                selectedCount = localEnd - localStart;
            }
            _resolvedEffectBindings.Clear();
            _effectResolver.TryResolve(
                effectId,
                effectParameters,
                phases,
                _resolvedEffectBindings,
                null,
                intensity,
                timeOffset,
                _localEffect,
                _previewSoloLayerId);

            for (int i = 0; i < _resolvedEffectBindings.Count; i++)
            {
                TextStudioResolvedEffectBinding resolved = _resolvedEffectBindings[i];
                bool writing = resolved.Effect is TextStudioCompositionLayer layer &&
                               layer.Kind == TextStudioCompositionLayerKind.Writing;
                int bindingStart = selectedStart;
                int bindingCount = selectedCount;
                resolved.RangeScope?.Resolve(ref bindingStart, ref bindingCount);
                bool wrapperOwnsRevealBlend = !writing && open.phase == AnimationPhase.Appearance &&
                                              open.wrapperGroup > 0;
                if (bindingCount <= 0) continue;
                AddExplicitBinding(new EffectBinding
                {
                    effectId = resolved.EffectId,
                    effect = resolved.Effect,
                    glyphStart = bindingStart,
                    glyphCount = bindingCount,
                    phase = writing ? AnimationPhase.None : open.phase,
                    phaseMask = resolved.PhaseMask,
                    blendMode = resolved.BlendMode,
                    phaseParams = resolved.HasAuthoringOverrides
                        ? resolved.PhaseParams
                        : ApplyStaggerParameters(open.phaseParams, resolved.Parameters),
                    intensity = resolved.Intensity,
                    timeOffset = resolved.TimeOffset,
                    parameters = resolved.Parameters,
                    area = resolved.HasAuthoringOverrides
                        ? resolved.Area
                        : CreateAreaSettings(resolved.EffectId, resolved.Parameters),
                    sequence = resolved.Sequence,
                    phaseBlendGroupId = wrapperOwnsRevealBlend
                        ? (open.revealActiveBlendDuration > 0f ? -open.wrapperGroup : 0)
                        : resolved.PhaseBlendGroupId,
                    revealActiveBlendDuration = wrapperOwnsRevealBlend
                        ? open.revealActiveBlendDuration
                        : resolved.RevealActiveBlendDuration,
                    revealActiveBlendPhaseMask = wrapperOwnsRevealBlend
                        ? TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Active
                        : resolved.RevealActiveBlendPhaseMask,
                    revealActiveBlendTimeOffset = wrapperOwnsRevealBlend
                        ? resolved.TimeOffset
                        : resolved.RevealActiveBlendTimeOffset,
                    scope = resolved.Scope,
                    selectorScope = resolved.SelectorScope,
                    envelope = resolved.Envelope
                });
            }

            _regionMaps.Add(new RegionToGlyphMap
            {
                glyphStart = open.glyphStart,
                regionName = open.name,
                glyphCount = glyphCount
            });
        }

        void AddExplicitBinding(in EffectBinding binding)
        {
            _effectBindings.Add(binding);
            if (BlocksAppend(in binding))
                _bindingBlocksAppend = true;
        }

        // Resolving the tag again creates new scope, selector and envelope chains and a new phase blend group, and writing
        // layers register their strokes while resolving. Effect objects are the shared definitions and composition layers of
        // every resolution, or stateless built-in evaluators (TextStudioPipeline.HasReplayableEffectBindings).
        static bool BlocksAppend(in EffectBinding binding)
        {
            ITextStudioEffect effect = binding.effect;
            bool sameEffect =
                effect == null ||
                effect is BuiltIns.CodeEffectBase ||
                effect is TextStudioCompositionLayer layer && layer.Kind != TextStudioCompositionLayerKind.Writing ||
                effect is ITextStudioEffectDefinition && !(effect is ITextStudioEffectFactory);
            return !sameEffect ||
                   binding.phaseBlendGroupId > 0 ||
                   binding.scope != null ||
                   binding.selectorScope != null ||
                   binding.envelope != null;
        }

        static TextStudioPhaseMask ToPhaseMask(AnimationPhase phase)
        {
            switch (phase)
            {
                case AnimationPhase.Appearance:
                    return TextStudioPhaseMask.Reveal;
                case AnimationPhase.Disappearance:
                    return TextStudioPhaseMask.Hide;
                case AnimationPhase.Persistent:
                    return TextStudioPhaseMask.Active;
                case AnimationPhase.None:
                default:
                    return TextStudioPhaseMask.All;
            }
        }

        void ApplyRemainingCharacterSubstitutionRanges(int resolvedIndex)
        {
            for (int i = 0; i < _characterSubstitutionTagStack.Count; i++)
                ApplyCharacterSubstitutionRange(_characterSubstitutionTagStack[i], resolvedIndex);

            _characterSubstitutionTagStack.Clear();
        }

        void ApplyRemainingInstantRanges(int resolvedIndex)
        {
            for (int i = 0; i < _instantTagStack.Count; i++)
                AddInstantRange(_instantTagStack[i].glyphStart, resolvedIndex);

            _instantTagStack.Clear();
        }

        void ApplyInstantRange(int resolvedIndex)
        {
            int lastIndex = _instantTagStack.Count - 1;
            if (lastIndex < 0)
                return;

            OpenTag open = _instantTagStack[lastIndex];
            _instantTagStack.RemoveAt(lastIndex);
            AddInstantRange(open.glyphStart, resolvedIndex);
        }

        void AddInstantRange(int glyphStart, int resolvedIndex)
        {
            int glyphCount = resolvedIndex - glyphStart;
            if (glyphCount <= 0)
                return;

            _instantRanges.Add(new TextStudioInstantRange
            {
                glyphStart = glyphStart,
                glyphCount = glyphCount
            });
        }

        void ApplyCharacterSubstitutionRange(string substitutionId, int resolvedIndex)
        {
            for (int i = _characterSubstitutionTagStack.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(_characterSubstitutionTagStack[i].name, substitutionId, StringComparison.OrdinalIgnoreCase))
                    continue;

                OpenTag open = _characterSubstitutionTagStack[i];
                _characterSubstitutionTagStack.RemoveAt(i);
                ApplyCharacterSubstitutionRange(open, resolvedIndex);
                return;
            }
        }

        void ApplyCharacterSubstitutionRange(OpenTag open, int resolvedIndex)
        {
            int glyphCount = resolvedIndex - open.glyphStart;
            if (glyphCount <= 0)
                return;

            _characterSubstitutionBindings.Add(new CharacterSubstitutionBinding
            {
                effectId = open.name,
                glyphStart = open.glyphStart,
                glyphCount = glyphCount,
                parameters = open.parameters
            });

            _regionMaps.Add(new RegionToGlyphMap
            {
                regionName = open.name,
                glyphStart = open.glyphStart,
                glyphCount = glyphCount
            });
        }

        void ApplyRemainingDecorationRanges(
            int resolvedIndex,
            int visibleGlyphCount,
            ref DecorationBits[] charDecorations)
        {
            while (_decorationTagStack.Count > 0)
            {
                int previousCount = _decorationTagStack.Count;
                OpenTag open = _decorationTagStack[_decorationTagStack.Count - 1];
                DecorationBits bit = ResolveNativeDecorationBit(open.name);
                if (bit == DecorationBits.None)
                    bit = BuiltIns.BuiltInStyles.ResolveDecorationBit(open.name);

                ApplyDecorationRange(_decorationTagStack, open.name, resolvedIndex, visibleGlyphCount, bit, ref charDecorations);
                if (_decorationTagStack.Count == previousCount)
                    _decorationTagStack.RemoveAt(_decorationTagStack.Count - 1);
            }
        }

        void ApplyRemainingExtensionRanges(int sourceEnd, int resolvedIndex)
        {
            for (int i = 0; i < _openExtensionTagStack.Count; i++)
            {
                OpenExtensionTag open = _openExtensionTagStack[i];
                _extensionTags.Add(new TextStudioExtensionTag(
                    open.definition,
                    open.authoredName,
                    open.sourceStart,
                    sourceEnd,
                    open.glyphStart,
                    Mathf.Max(0, resolvedIndex - open.glyphStart),
                    TextStudioExtensionTagBoundary.Range,
                    open.parameters));
            }

            _openExtensionTagStack.Clear();
        }

        static int CompareExtensionTags(TextStudioExtensionTag a, TextStudioExtensionTag b)
        {
            int glyph = a.GlyphStart.CompareTo(b.GlyphStart);
            if (glyph != 0)
                return glyph;
            return a.SourceStart.CompareTo(b.SourceStart);
        }

        static string ExtractTagName(string source, in TextToken token)
        {
            int nameEnd = token.nameEnd > token.sourceStart ? token.nameEnd : token.sourceEnd;
            if (nameEnd <= token.sourceStart) nameEnd = token.sourceEnd;

            // Trim whitespace manually to avoid Substring+Trim double allocation
            int start = token.sourceStart;
            int end = nameEnd;
            while (start < end && char.IsWhiteSpace(source[start])) start++;
            while (end > start && char.IsWhiteSpace(source[end - 1])) end--;

            if (end <= start) return string.Empty;
            return source.Substring(start, end - start);
        }

        static bool IsInstantTag(string tagName)
        {
            return string.Equals(tagName, "instant", StringComparison.OrdinalIgnoreCase);
        }

        static DecorationBits ResolveNativeDecorationBit(string tagName)
        {
            if (string.IsNullOrEmpty(tagName)) return DecorationBits.None;
            if (string.Equals(tagName, "u", StringComparison.OrdinalIgnoreCase)) return DecorationBits.Underline;
            if (string.Equals(tagName, "s", StringComparison.OrdinalIgnoreCase)) return DecorationBits.Strikethrough;
            return DecorationBits.None;
        }

        static void ApplyDecorationRange(
            List<OpenTag> stack,
            string closeName,
            int resolvedIndex,
            int visibleGlyphCount,
            DecorationBits bit,
            ref DecorationBits[] charDecorations)
        {
            if (bit == DecorationBits.None)
                return;

            for (int k = stack.Count - 1; k >= 0; k--)
            {
                if (!string.Equals(stack[k].name, closeName, StringComparison.OrdinalIgnoreCase))
                    continue;

                OpenTag open = stack[k];
                stack.RemoveAt(k);

                int glyphCount = resolvedIndex - open.glyphStart;
                if (glyphCount <= 0 || visibleGlyphCount <= 0)
                    return;

                if (charDecorations == null)
                    charDecorations = new DecorationBits[visibleGlyphCount];

                int start = Math.Max(0, open.glyphStart);
                int end = Math.Min(open.glyphStart + glyphCount, visibleGlyphCount);
                for (int g = start; g < end; g++)
                    charDecorations[g] |= bit;
                return;
            }
        }

        string[] ExtractParameters(string actionName, in TextStudioTagParameters parameters)
        {
            if (parameters.PositionalCount == 0 && parameters.NamedCount == 0)
                return Array.Empty<string>();

            if (parameters.NamedCount == 0)
                return CopyPositionalParameters(parameters);

            List<string> result = new List<string>(parameters.PositionalCount + parameters.NamedCount);
            for (int i = 0; i < parameters.PositionalCount; i++)
                result.Add(parameters.GetString(i));

            if (TryGetActionDefinition(actionName, out TextStudioActionDefinition definition))
            {
                for (int i = 0; i < definition.Parameters.Count; i++)
                {
                    TextStudioTagParameterDefinition parameter = definition.Parameters[i];
                    if (parameter.PositionalIndex < 0)
                        continue;

                    if (!TryGetNamedParameter(parameters, parameter, out string value))
                        continue;

                    while (result.Count <= parameter.PositionalIndex)
                        result.Add(string.Empty);
                    result[parameter.PositionalIndex] = value;
                }
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<string>();
        }

        static string[] CopyPositionalParameters(in TextStudioTagParameters parameters)
        {
            if (parameters.PositionalCount == 0)
                return Array.Empty<string>();

            string[] result = new string[parameters.PositionalCount];
            for (int i = 0; i < result.Length; i++)
                result[i] = parameters.GetString(i);
            return result;
        }

        static bool TryGetNamedParameter(
            in TextStudioTagParameters parameters,
            TextStudioTagParameterDefinition definition,
            out string value)
        {
            if (!string.IsNullOrEmpty(definition.Name) && parameters.TryGetString(definition.Name, out value))
                return true;

            IReadOnlyList<string> aliases = definition.Aliases;
            for (int i = 0; i < aliases.Count; i++)
            {
                if (parameters.TryGetString(aliases[i], out value))
                    return true;
            }

            value = string.Empty;
            return false;
        }

        bool TryGetActionDefinition(string actionName, out TextStudioActionDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(actionName))
                return false;

            if (TryGetLocalAction(actionName, out ITextStudioAction localAction) &&
                TextStudioActionDefinitionUtility.TryGetDefinition(localAction, out definition))
            {
                return true;
            }

            TextStudioRegistry.EnsureInitialized();
            if (TextStudioRegistry.Actions.TryGetQuiet(actionName, out ITextStudioAction action) &&
                TextStudioActionDefinitionUtility.TryGetDefinition(action, out definition))
            {
                return true;
            }

            return false;
        }

        bool TryGetLocalActionId(string tagName, out string actionId)
        {
            actionId = string.Empty;
            if (string.IsNullOrEmpty(tagName))
                return false;

            for (int i = 0; i < _localActions.Count; i++)
            {
                ITextStudioAction action = _localActions[i];
                if (!TextStudioActionDefinitionUtility.MatchesName(action, tagName))
                    continue;

                actionId = action.Id;
                return true;
            }

            return false;
        }

        static bool RequiresSourceTextBounds(in EffectBinding binding)
        {
            if (string.Equals(
                    binding.effectId,
                    TextStudioBuiltInEffectIds.Burn,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (binding.parameters.TryGetString("space", out string burnSpace))
                    return string.Equals(burnSpace, "text", StringComparison.OrdinalIgnoreCase);
                return true;
            }

            if (!string.Equals(
                    binding.effectId,
                    TextStudioBuiltInEffectIds.Dissolve,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return binding.parameters.TryGetString("space", out string dissolveSpace) &&
                   string.Equals(dissolveSpace, "text", StringComparison.OrdinalIgnoreCase);
        }

        bool TryGetLocalAction(string tagName, out ITextStudioAction action)
        {
            action = null;
            if (string.IsNullOrEmpty(tagName))
                return false;

            for (int i = 0; i < _localActions.Count; i++)
            {
                ITextStudioAction candidate = _localActions[i];
                if (!TextStudioActionDefinitionUtility.MatchesName(candidate, tagName))
                    continue;

                action = candidate;
                return true;
            }

            return false;
        }

        static bool TryGetRegisteredActionId(string tagName, out string actionId)
        {
            actionId = string.Empty;
            if (string.IsNullOrEmpty(tagName))
                return false;

            TextStudioRegistry.EnsureInitialized();
            if (TextStudioRegistry.Actions.TryGetQuiet(tagName, out ITextStudioAction exactAction))
            {
                actionId = exactAction.Id;
                return true;
            }

            if (TextStudioRegistry.Actions.TryFindFirst(s_matchesActionName, tagName, out ITextStudioAction action))
            {
                actionId = action.Id;
                return true;
            }

            return false;
        }

        static readonly Func<ITextStudioAction, string, bool> s_matchesActionName =
            TextStudioActionDefinitionUtility.MatchesName;

        static TextStudioTagParameters ExtractTagParameters(
            string source,
            in TextToken token,
            bool allowNumericModifiers = false)
        {
            if (token.parameterStart <= 0 || token.parameterEnd <= token.parameterStart)
                return TextStudioTagParameters.Empty;

            string raw = source.Substring(token.parameterStart, token.parameterEnd - token.parameterStart).Trim();
            return TextStudioTagParameterParser.Parse(raw, allowNumericModifiers);
        }

        static bool SupportsNumericParameterMultiplication(string tagName)
        {
            if (TextStudioPhaseTagUtility.IsPhaseTag(tagName))
                return true;

            string effectId = BuiltIns.BuiltInStyles.TryResolve(tagName, out string builtInEffectId)
                ? builtInEffectId
                : tagName;
            return TextStudioRegistry.Effects.TryGetQuiet(effectId, out ITextStudioEffectDefinition effect) &&
                   effect is ITextStudioEffectFactory;
        }

        static TextStudioTagParameters NormalizeTextAnimatorPlaybackParameters(TextStudioTagParameters parameters)
        {
            return BuiltIns.BuiltInPlaybacks.CanonicalizeTextAnimatorKeywordParameters(in parameters);
        }
    }
}
