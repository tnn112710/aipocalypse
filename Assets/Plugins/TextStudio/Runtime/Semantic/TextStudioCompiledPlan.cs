using System;
using System.Collections.Generic;

namespace TextStudio
{
    /// <summary>
    /// Immutable compiled plan produced by the text pipeline.
    /// Contains all resolved data needed for layout, placement, animation, and rendering.
    /// Rebuilt only when the text or style changes (dirty-checked via version).
    /// </summary>
    internal sealed class TextStudioCompiledPlan
    {
        // Source
        public string CanonicalText { get; internal set; }
        public uint SourceVersion { get; internal set; }

        // Tokens
        public TextToken[] Tokens { get; internal set; }
        public int TokenCount { get; internal set; }

        // Resolved text (stripped of markup). This is the plain character stream that
        // downstream glyph indexing (reveal, effects, layout) operates on.
        public string ResolvedText { get; internal set; }

        // What we hand to the text backend (TMP) for rendering. Identical to
        // ResolvedText except that native rich text formatting tags (u, s, b, i, color, …)
        // are preserved so TMP can render them. When null, adapters fall back to
        // ResolvedText.
        public string DisplayText { get; internal set; }
        internal bool IsLiteralMarkup { get; set; }

        // Glyph data
        public GlyphRun[] GlyphRuns { get; internal set; }
        public int GlyphRunCount { get; internal set; }

        // Segmentation. A pipeline can defer it to the first read of a segmentation member, so text that nothing reads by word, line or paragraph never segments.
        TextSegmenter _deferredSegmenter;
        ITextStudioWordBoundaryProvider _deferredWordBoundaryProvider;
        WordRange[] _wordRanges;
        int _wordCount;
        LineRange[] _lineRanges;
        int _lineCount;
        ParagraphRange[] _paragraphRanges;
        int _paragraphCount;
        int[] _glyphToWordIndex;
        int[] _glyphToLineIndex;
        int[] _glyphToParagraphIndex;

        public WordRange[] WordRanges
        {
            get { EnsureSegmented(); return _wordRanges; }
            internal set => _wordRanges = value;
        }

        public int WordCount
        {
            get { EnsureSegmented(); return _wordCount; }
            internal set => _wordCount = value;
        }

        public LineRange[] LineRanges
        {
            get { EnsureSegmented(); return _lineRanges; }
            internal set => _lineRanges = value;
        }

        public int LineCount
        {
            get { EnsureSegmented(); return _lineCount; }
            internal set => _lineCount = value;
        }

        public ParagraphRange[] ParagraphRanges
        {
            get { EnsureSegmented(); return _paragraphRanges; }
            internal set => _paragraphRanges = value;
        }

        public int ParagraphCount
        {
            get { EnsureSegmented(); return _paragraphCount; }
            internal set => _paragraphCount = value;
        }

        public TextStudioAppearanceSpan[] AppearanceSpans { get; internal set; }
        public int AppearanceSpanCount { get; internal set; }

        // Effects & actions
        public EffectBinding[] EffectBindings { get; internal set; }
        public int EffectBindingCount { get; internal set; }
        internal TextStudioSourceContextRequirements SourceContextRequirements { get; set; }
        public CharacterSubstitutionBinding[] CharacterSubstitutionBindings { get; internal set; }
        public int CharacterSubstitutionBindingCount { get; internal set; }
        public ActionMarker[] ActionMarkers { get; internal set; }
        public int ActionMarkerCount { get; internal set; }
        public TextStudioExtensionTag[] ExtensionTags { get; internal set; }
        public int ExtensionTagCount { get; internal set; }
        public TextStudioInstantRange[] InstantRanges { get; internal set; }
        public int InstantRangeCount { get; internal set; }

        // Region map
        public RegionToGlyphMap[] RegionMaps { get; internal set; }
        public int RegionMapCount { get; internal set; }

        // Precomputed glyph→word/line lookups (O(1) per-glyph queries)
        public int[] GlyphToWordIndex
        {
            get { EnsureSegmented(); return _glyphToWordIndex; }
            internal set => _glyphToWordIndex = value;
        }

        public int[] GlyphToLineIndex
        {
            get { EnsureSegmented(); return _glyphToLineIndex; }
            internal set => _glyphToLineIndex = value;
        }

        public int[] GlyphToParagraphIndex
        {
            get { EnsureSegmented(); return _glyphToParagraphIndex; }
            internal set => _glyphToParagraphIndex = value;
        }

        TextStudioTextUnit[] _textUnits;
        int[] _resolvedCharToTextUnitIndex;
        int[] _displayCharToResolvedCharIndex;
        int[] _displayCharToTextUnitIndex;

        public int VisibleGlyphCount { get; internal set; }

        public TextStudioTextUnit[] TextUnits
        {
            get => _textUnits;
            internal set
            {
                if (!ReferenceEquals(_textUnits, value))
                    IdentityUnitCount = 0;
                _textUnits = value;
            }
        }

        public int TextUnitCount { get; internal set; }

        public int[] ResolvedCharToTextUnitIndex
        {
            get => _resolvedCharToTextUnitIndex;
            internal set
            {
                if (!ReferenceEquals(_resolvedCharToTextUnitIndex, value))
                    IdentityUnitCount = 0;
                _resolvedCharToTextUnitIndex = value;
            }
        }

        public int[] DisplayCharToResolvedCharIndex
        {
            get => _displayCharToResolvedCharIndex;
            internal set
            {
                if (!ReferenceEquals(_displayCharToResolvedCharIndex, value))
                    IdentityDisplayCount = 0;
                _displayCharToResolvedCharIndex = value;
            }
        }

        public int[] DisplayCharToTextUnitIndex
        {
            get => _displayCharToTextUnitIndex;
            internal set
            {
                if (!ReferenceEquals(_displayCharToTextUnitIndex, value))
                    IdentityDisplayCount = 0;
                _displayCharToTextUnitIndex = value;
            }
        }

        // Leading slots of TextUnits and ResolvedCharToTextUnitIndex that hold the one-character identity units of plain text. Builders keep those slots instead of rewriting them; any other writer clears the count, as does assigning another array.
        internal int IdentityUnitCount { get; set; }

        // Leading slots of both display maps that map every display character to the same resolved character and unit.
        internal int IdentityDisplayCount { get; set; }

        // Per-character decoration bits collected from Text Studio custom tags
        // (<uu>, <o>, ...) and native rich text underline / strikethrough ranges.
        // Runtime TMP style flags and global TMP fontStyle are still ORed in by
        // the decoration renderer so all sources flow through the same tiles.
        // Length equals <see cref="VisibleGlyphCount"/> when populated; may be
        // null when no source-authored decoration tags appear in the text.
        public DecorationBits[] CharDecorations { get; internal set; }

        /// <summary>
        /// Segments the current units on the first read of a segmentation member, with the word boundary provider the segmenter has now. Null cancels a deferred segmentation.
        /// </summary>
        internal void DeferSegmentation(TextSegmenter segmenter)
        {
            _deferredSegmenter = segmenter;
            _deferredWordBoundaryProvider = segmenter?.WordBoundaryProvider;
        }

        void EnsureSegmented()
        {
            TextSegmenter segmenter = _deferredSegmenter;
            if (segmenter == null)
                return;

            // Cleared first: segmenting reads the arrays it reuses through these members.
            ITextStudioWordBoundaryProvider wordBoundaryProvider = _deferredWordBoundaryProvider;
            _deferredSegmenter = null;
            _deferredWordBoundaryProvider = null;
            segmenter.Segment(this, wordBoundaryProvider);
        }

        public TextStudioTextUnit GetTextUnit(int unitIndex)
        {
            if (TextUnits == null || unitIndex < 0 || unitIndex >= TextUnitCount)
                return default;

            return TextUnits[unitIndex];
        }

        public int GetResolvedStart(int unitIndex)
        {
            if (TextUnits == null || unitIndex < 0 || unitIndex >= TextUnitCount)
                return unitIndex;

            return TextUnits[unitIndex].resolvedStart;
        }

        public int GetResolvedLength(int unitIndex)
        {
            if (TextUnits == null || unitIndex < 0 || unitIndex >= TextUnitCount)
                return 0;

            return TextUnits[unitIndex].resolvedLength;
        }

        public int GetUnitIndexForResolvedChar(int resolvedCharIndex)
        {
            if (ResolvedCharToTextUnitIndex == null ||
                resolvedCharIndex < 0 ||
                resolvedCharIndex >= (ResolvedText != null ? ResolvedText.Length : 0) ||
                resolvedCharIndex >= ResolvedCharToTextUnitIndex.Length)
            {
                return resolvedCharIndex;
            }

            return ResolvedCharToTextUnitIndex[resolvedCharIndex];
        }

        public int GetUnitIndexForDisplayChar(int displayCharIndex)
        {
            if (DisplayCharToTextUnitIndex == null ||
                displayCharIndex < 0 ||
                displayCharIndex >= (DisplayText != null ? DisplayText.Length : 0) ||
                displayCharIndex >= DisplayCharToTextUnitIndex.Length)
            {
                return -1;
            }

            return DisplayCharToTextUnitIndex[displayCharIndex];
        }

        public int GetResolvedIndexForDisplayChar(int displayCharIndex)
        {
            if (DisplayCharToResolvedCharIndex == null ||
                displayCharIndex < 0 ||
                displayCharIndex >= (DisplayText != null ? DisplayText.Length : 0) ||
                displayCharIndex >= DisplayCharToResolvedCharIndex.Length)
            {
                return -1;
            }

            return DisplayCharToResolvedCharIndex[displayCharIndex];
        }

        public int GetUnitBoundaryForResolvedChar(int resolvedCharIndex)
        {
            if (resolvedCharIndex <= 0)
                return 0;

            if (string.IsNullOrEmpty(ResolvedText))
                return 0;

            if (resolvedCharIndex >= ResolvedText.Length)
                return TextUnitCount > 0 ? TextUnitCount : VisibleGlyphCount;

            int unitIndex = GetUnitIndexForResolvedChar(resolvedCharIndex);
            if (TextUnits == null || unitIndex < 0 || unitIndex >= TextUnitCount)
                return resolvedCharIndex;

            TextStudioTextUnit unit = TextUnits[unitIndex];
            return resolvedCharIndex >= unit.ResolvedEnd ? unitIndex + 1 : unitIndex;
        }

        public char GetPrimaryChar(int unitIndex)
        {
            int resolvedStart = GetResolvedStart(unitIndex);
            return !string.IsNullOrEmpty(ResolvedText) &&
                   resolvedStart >= 0 &&
                   resolvedStart < ResolvedText.Length
                ? ResolvedText[resolvedStart]
                : '\0';
        }

        public int GetPrimaryCodePoint(int unitIndex)
        {
            int start = GetResolvedStart(unitIndex);
            return !string.IsNullOrEmpty(ResolvedText) && start >= 0 && start < ResolvedText.Length
                ? TextStudioTextUnitBoundary.ReadCodePoint(ResolvedText, start, out _)
                : 0;
        }

        public string GetUnitText(int unitIndex)
        {
            if (TextUnits == null || unitIndex < 0 || unitIndex >= TextUnitCount)
                return string.Empty;

            TextStudioTextUnit unit = TextUnits[unitIndex];
            return ResolvedText != null &&
                   unit.resolvedStart >= 0 &&
                   unit.resolvedLength >= 0 &&
                   unit.ResolvedEnd <= ResolvedText.Length
                ? ResolvedText.Substring(unit.resolvedStart, unit.resolvedLength)
                : string.Empty;
        }

        public bool IsWhiteSpaceUnit(int unitIndex)
        {
            if (TextUnits == null ||
                unitIndex < 0 ||
                unitIndex >= TextUnitCount ||
                string.IsNullOrEmpty(ResolvedText))
                return false;

            TextStudioTextUnit unit = TextUnits[unitIndex];
            int end = unit.ResolvedEnd;
            if (unit.resolvedStart < 0 || unit.resolvedLength <= 0 || end > ResolvedText.Length)
                return false;

            for (int i = unit.resolvedStart; i < end; i++)
            {
                if (!char.IsWhiteSpace(ResolvedText[i]))
                    return false;
            }

            return true;
        }

        public bool IsLineBreakUnit(int unitIndex)
        {
            char c = GetPrimaryChar(unitIndex);
            return c == '\n' || c == '\r';
        }

        public bool IsPunctuationUnit(int unitIndex)
        {
            char c = GetPrimaryChar(unitIndex);
            return c != '\0' && char.IsPunctuation(c);
        }
    }

    /// <summary>
    /// A run of visible glyphs with shared style properties.
    /// </summary>
    public struct GlyphRun
    {
        public int startIndex;
        public int count;
        public int sourceStart;
        public int sourceEnd;
        public int styleIndex;

        public int EndIndex => startIndex + count;
    }

    /// <summary>Inclusive visible-glyph range whose reveal timing is collapsed to one instant boundary.</summary>
    public struct TextStudioInstantRange
    {
        public int glyphStart;
        public int glyphCount;

        public int GlyphEnd => glyphStart + glyphCount;

        /// <summary>Reports whether a visible-glyph index is inside the inclusive range.</summary>
        public bool Contains(int glyphIndex)
        {
            return glyphIndex >= glyphStart && glyphIndex < glyphStart + glyphCount;
        }
    }

    /// <summary>
    /// Word segmentation range.
    /// </summary>
    public struct WordRange
    {
        public int glyphStart;
        public int glyphCount;
        public int wordIndex;

        public int GlyphEnd => glyphStart + glyphCount;
    }

    /// <summary>
    /// Line segmentation range.
    /// </summary>
    public struct LineRange
    {
        public int glyphStart;
        public int glyphCount;
        public int lineIndex;
        public float width;
        public float baseline;

        public int GlyphEnd => glyphStart + glyphCount;
    }

    /// <summary>
    /// Paragraph segmentation range.
    /// </summary>
    public struct ParagraphRange
    {
        public int glyphStart;
        public int glyphCount;
        public int paragraphIndex;

        public int GlyphEnd => glyphStart + glyphCount;
    }

    /// <summary>
    /// Binding of an effect to a glyph range within the text.
    /// </summary>
    public struct EffectBinding
    {
        public string effectId;
        public ITextStudioEffect effect;
        public int glyphStart;
        public int glyphCount;
        public AnimationPhase phase;
        public TextStudioPhaseMask phaseMask;
        public EffectBlendMode blendMode;
        public PhaseParams phaseParams;
        public float intensity;
        public float timeOffset;
        public TextStudioTagParameters parameters;
        public TextStudioEffectAreaSettings area;
        internal TextStudioSequenceSettings sequence;
        internal int phaseBlendGroupId;
        internal float revealActiveBlendDuration;
        internal TextStudioPhaseMask revealActiveBlendPhaseMask;
        internal float revealActiveBlendTimeOffset;
        internal TextStudioEffectScopeChain scope;
        internal TextStudioEffectSelectorChain selectorScope;
        internal TextStudioComposedEnvelopeStack envelope;
    }

    /// <summary>
    /// Binding of a presentation-layer character substitution to a glyph range.
    /// </summary>
    public struct CharacterSubstitutionBinding
    {
        public string effectId;
        public int glyphStart;
        public int glyphCount;
        public TextStudioTagParameters parameters;

        public string SubstitutionId => effectId;
    }

    /// <summary>
    /// Maps a named region to its glyph range.
    /// </summary>
    public struct RegionToGlyphMap
    {
        public string regionName;
        public int glyphStart;
        public int glyphCount;
    }
}
