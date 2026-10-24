using System;

namespace TextStudio
{
    // Appended markup compiles only its own tokens when nothing it adds can change the plan before it: the text transforms leave
    // the whole text unchanged, the tokenizer resumes at its recorded point, every tag before the append is closed, the units
    // up to the previous text keep their extent, and the style resolution before it took no input an append could change.
    // Anything else compiles in full, and parity tests compare both paths (TextStudioMarkupAppendParityTests).
    internal sealed partial class TextStudioPipeline
    {
        // The inputs of the last markup compile that an append must still find unchanged.
        struct MarkupAppendState
        {
            internal bool IsValid;
            internal string Text;
            internal TextStudioMarkupSyntax MarkupSyntax;
            internal int MarkupSyntaxRevision;
            internal uint StyleInputRevision;
            internal RegistryRevisions Registries;
        }

        // Revisions of every registry that tokenizing and resolving markup reads.
        readonly struct RegistryRevisions : IEquatable<RegistryRevisions>
        {
            readonly uint _effects;
            readonly uint _actions;
            readonly uint _appearanceStyles;
            readonly uint _curves;
            readonly uint _playbacks;
            readonly uint _extensionTags;
            readonly uint _characterSubstitutions;
            readonly uint _tagExpansions;
            readonly uint _effectContent;

            RegistryRevisions(uint effects, uint actions, uint appearanceStyles, uint curves, uint playbacks,
                uint extensionTags, uint characterSubstitutions, uint tagExpansions, uint effectContent)
            {
                _effects = effects;
                _actions = actions;
                _appearanceStyles = appearanceStyles;
                _curves = curves;
                _playbacks = playbacks;
                _extensionTags = extensionTags;
                _characterSubstitutions = characterSubstitutions;
                _tagExpansions = tagExpansions;
                _effectContent = effectContent;
            }

            internal static RegistryRevisions Capture()
            {
                return new RegistryRevisions(
                    TextStudioRegistry.Effects.Revision,
                    TextStudioRegistry.Actions.Revision,
                    TextStudioRegistry.AppearanceStyles.Revision,
                    TextStudioRegistry.Curves.Revision,
                    TextStudioRegistry.Playbacks.Revision,
                    TextStudioRegistry.ExtensionTags.Revision,
                    TextStudioRegistry.CharacterSubstitutions.Revision,
                    TextStudioRegistry.TagExpansions.Revision,
                    TextStudioEffectDefinitionAsset.ContentRevision);
            }

            public bool Equals(RegistryRevisions other)
            {
                return _effects == other._effects &&
                       _actions == other._actions &&
                       _appearanceStyles == other._appearanceStyles &&
                       _curves == other._curves &&
                       _playbacks == other._playbacks &&
                       _extensionTags == other._extensionTags &&
                       _characterSubstitutions == other._characterSubstitutions &&
                       _tagExpansions == other._tagExpansions &&
                       _effectContent == other._effectContent;
            }

            public override bool Equals(object obj) => obj is RegistryRevisions other && Equals(other);

            public override int GetHashCode() => unchecked((int)(_effects * 31 + _actions));
        }

        MarkupAppendState _markupAppend;
        bool _hasNativeFormatting;

        /// <summary>Whether the last process compiled appended markup on top of the plan before it.</summary>
        internal bool LastProcessAppendedMarkup { get; private set; }

        /// <summary>
        /// Whether the last process kept the leading glyphs of the plan before it, through the markup or the plain text append
        /// path: glyphs before <see cref="AppendKeptGlyphCount"/> are those of the plan with <see cref="AppendBaseSourceVersion"/>.
        /// </summary>
        internal bool LastProcessKeptAppendPrefix => LastProcessAppendedMarkup || LastProcessUsedPlainTextAppendFastPath;

        /// <summary>
        /// After an append: glyphs before this index, and after an appended markup compile every effect binding before
        /// <see cref="AppendKeptBindingCount"/>, are those of the plan before it, while later glyphs only appended bindings cover.
        /// </summary>
        internal int AppendKeptGlyphCount { get; private set; }

        internal int AppendKeptBindingCount { get; private set; }

        /// <summary>The source version of the plan an append built on.</summary>
        internal uint AppendBaseSourceVersion { get; private set; }

        // Parity tests compile appended markup in full to compare that with the append path.
        internal bool DisableMarkupAppendForTests { get; set; }

        bool CanProcessAppendedMarkup(
            TextStudioMarkupSyntax markupSyntax,
            int markupSyntaxRevision,
            bool renderMarkupLiterally,
            TextStudioComposedEffectAsset localEffect,
            bool unitsOnly)
        {
            return _markupAppend.IsValid &&
                   !DisableMarkupAppendForTests &&
                   !renderMarkupLiterally &&
                   !unitsOnly &&
                   localEffect == null &&
                   ReferenceEquals(markupSyntax, _markupAppend.MarkupSyntax) &&
                   markupSyntaxRevision == _markupAppend.MarkupSyntaxRevision &&
                   _styleInputRevision == _markupAppend.StyleInputRevision &&
                   _projectionBackend == null &&
                   // A custom word boundary provider may read any context, so its text segments in full (TextSegmenter).
                   _segmenter.WordBoundaryProvider == null &&
                   !ResolvesPlainTextStyles &&
                   _markupAppend.Registries.Equals(RegistryRevisions.Capture());
        }

        // Compiles canonical text that appends to the text of the plan from appendStart on. Returns false before changing the plan
        // when the append cannot keep it; a false return after that leaves state the full compile that follows rebuilds.
        bool TryProcessAppendedMarkup(string canonicalText, uint version, int appendStart, TextStudioMarkupSyntax markupSyntax)
        {
            string previousText = _plan.CanonicalText;
            if (!ReferenceEquals(previousText, _markupAppend.Text) ||
                appendStart != previousText.Length ||
                canonicalText.Length < appendStart ||
                !IsUnchangedByTransforms(canonicalText, appendStart, markupSyntax))
            {
                return false;
            }

            // The tokens the tokenizer scans again must be text, whose resolved characters the builders drop again.
            if (!_tokenizer.TryGetResumePoint(appendStart, out int restartToken))
                return false;
            TextToken[] previousTokens = _plan.Tokens;
            int previousTokenCount = _plan.TokenCount;
            int droppedCharacters = 0;
            for (int i = restartToken; i < previousTokenCount; i++)
            {
                ref TextToken token = ref previousTokens[i];
                if (token.kind != TokenKind.TextSpan &&
                    token.kind != TokenKind.Whitespace &&
                    token.kind != TokenKind.EscapedLiteral)
                {
                    return false;
                }
                droppedCharacters += token.sourceEnd - token.sourceStart;
            }

            string previousResolvedText = _plan.ResolvedText ?? string.Empty;
            int previousBindingCount = _plan.EffectBindingCount;
            uint previousSourceVersion = _plan.SourceVersion;
            int keptResolvedLength = _resolvedSourceIndices.Count - droppedCharacters;
            int keptDisplayLength = _displayBuilder.Length - droppedCharacters;
            if (keptResolvedLength < 0 || keptDisplayLength < 0 || _resolvedBuilder.Length != previousResolvedText.Length ||
                _resolvedSourceIndices.Count != previousResolvedText.Length ||
                _displayResolvedIndices.Count != _displayBuilder.Length)
            {
                return false;
            }

            // From here on a false return falls back to the full compile, which rebuilds every stage below.
            _markupAppend.IsValid = false;
            _tokenizer.TryResume(canonicalText, appendStart);
            _plan.CanonicalText = canonicalText;
            _plan.SourceVersion = version;
            CopyTokensToPlan(_tokenizer, restartToken);
            _plan.TokenCount = _tokenizer.TokenCount;
            string[] names = _tokenizer.Names;
            if (!_rangeLedger.BuildAppended(canonicalText, _plan.Tokens, restartToken, _plan.TokenCount, names))
                return false;

            _resolvedBuilder.Length = keptResolvedLength;
            _displayBuilder.Length = keptDisplayLength;
            _resolvedSourceIndices.RemoveRange(keptResolvedLength, droppedCharacters);
            _displayResolvedIndices.RemoveRange(keptDisplayLength, droppedCharacters);
            AppendTokenText(canonicalText, restartToken, _plan.TokenCount, names);
            string resolvedText = _resolvedBuilder.ToString();
            // Text tokens scanned again must resolve to the characters they resolved to before.
            if (resolvedText.Length < previousResolvedText.Length ||
                string.CompareOrdinal(resolvedText, keptResolvedLength, previousResolvedText, keptResolvedLength,
                    previousResolvedText.Length - keptResolvedLength) != 0)
            {
                return false;
            }

            _plan.ResolvedText = resolvedText;
            _plan.DisplayText = _hasNativeFormatting ? _displayBuilder.ToString() : resolvedText;
            if (!TextStudioTextUnitBuilder.TryBuildAppended(
                    _plan,
                    _resolvedSourceIndices,
                    previousResolvedText.Length,
                    keptResolvedLength,
                    out int rebuiltFrom,
                    out int keptUnitCount))
            {
                return false;
            }

            ApplyNativeTextAtoms(rebuiltFrom);
            BuildDisplayCharacterMap(keptDisplayLength);
            FinishResolvedGlyphs(canonicalText.Length);
            if (!_styleResolver.TryResolveAppended(_plan, _defaultEffects, _rangeLedger, names, restartToken, keptResolvedLength))
                return false;

            _segmenter.NoteKeptUnits(previousResolvedText, version, keptUnitCount);
            _plan.DeferSegmentation(_segmenter);
            LastProcessAppendedMarkup = true;
            AppendKeptGlyphCount = _plan.GetUnitBoundaryForResolvedChar(keptResolvedLength);
            AppendKeptBindingCount = previousBindingCount;
            AppendBaseSourceVersion = previousSourceVersion;
            RecordMarkupAppendState(canonicalText, markupSyntax);
            return true;
        }

        // Normalizing, expanding and translating leave appended text unchanged when they left the text before it unchanged and
        // the appended part changes nothing; the translator scans only from its recorded resume point.
        bool IsUnchangedByTransforms(string canonicalText, int appendStart, TextStudioMarkupSyntax markupSyntax)
        {
            if (markupSyntax != null && !markupSyntax.NormalizesIdentically)
                return false;
            TextStudioAuthoringProfile profile = TextStudioMarkupSyntax.ResolveProfile(markupSyntax);
            if (profile != TextStudioAuthoringProfile.Native && profile != TextStudioAuthoringProfile.Compatibility)
                return false;
            if (TextStudioRegistry.TagExpansions.Count > 0)
                return false;
            return _compatibilityTranslator.TryTranslateAppendedIdentically(canonicalText, appendStart, profile);
        }

        // Records the markup compile of text an append can extend: the text transforms returned it unchanged, the tokenizer has a
        // resume point, the resolver can resume, and no registry changed while the compile read them.
        void RecordMarkupAppendState(string text, TextStudioMarkupSyntax markupSyntax)
        {
            RegistryRevisions registries = RegistryRevisions.Capture();
            _markupAppend = new MarkupAppendState
            {
                IsValid = _styleResolver.CanResolveAppended &&
                          _tokenizer.TryGetResumePoint(text.Length, out _) &&
                          _projectionBackend == null &&
                          registries.Equals(_markupCompileRegistries),
                Text = text,
                MarkupSyntax = markupSyntax,
                MarkupSyntaxRevision = markupSyntax != null ? markupSyntax.Revision : 0,
                StyleInputRevision = _styleInputRevision,
                Registries = registries
            };
        }
    }
}
