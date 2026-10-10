using System;
using System.Collections;
using System.Collections.Generic;

namespace TextStudio
{
    /// <summary>
    /// Zero-allocation read-only view over a bounded portion of an engine-owned array.
    /// The view is valid only for the duration documented by the API that supplied it.
    /// </summary>
    public readonly struct TextStudioReadOnlyBuffer<T> : IReadOnlyList<T>
    {
        readonly T[] _items;
        readonly int _count;

        internal TextStudioReadOnlyBuffer(T[] items, int count)
        {
            _items = items;
            _count = items != null ? Math.Max(0, Math.Min(count, items.Length)) : 0;
        }

        public bool IsCreated => _items != null;
        internal T[] Items => _items;
        public int Count => _count;
        public int Length => _count;

        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _items[index];
            }
        }

        /// <summary>Creates a zero-allocation enumerator bounded to the published count.</summary>
        public Enumerator GetEnumerator() => new Enumerator(_items, _count);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Value-type enumerator over the bounded engine-owned buffer.</summary>
        public struct Enumerator : IEnumerator<T>
        {
            readonly T[] _items;
            readonly int _count;
            int _index;

            internal Enumerator(T[] items, int count)
            {
                _items = items;
                _count = count;
                _index = -1;
            }

            public T Current => _items[_index];
            object IEnumerator.Current => Current;

            /// <summary>Advances to the next item within the published count.</summary>
            public bool MoveNext()
            {
                int next = _index + 1;
                if (next >= _count)
                    return false;
                _index = next;
                return true;
            }

            /// <summary>Returns the enumerator to its initial position.</summary>
            public void Reset() => _index = -1;
            /// <summary>Completes enumeration. The engine-owned buffer is not released.</summary>
            public void Dispose() { }
        }
    }

    /// <summary>Read-only semantic plan consumed by renderers and integrations.</summary>
    public readonly struct TextStudioPlanView
    {
        readonly TextStudioCompiledPlan _plan;

        internal TextStudioPlanView(TextStudioCompiledPlan plan)
        {
            _plan = plan;
        }

        internal TextStudioCompiledPlan CompiledPlan => _plan;

        public bool IsCreated => _plan != null;
        public string CanonicalText => _plan?.CanonicalText ?? string.Empty;
        public uint SourceVersion => _plan?.SourceVersion ?? 0u;
        public string ResolvedText => _plan?.ResolvedText ?? string.Empty;
        public string DisplayText => _plan?.DisplayText ?? string.Empty;
        public int TokenCount => _plan?.TokenCount ?? 0;
        public int GlyphRunCount => _plan?.GlyphRunCount ?? 0;
        public int WordCount => _plan?.WordCount ?? 0;
        public int LineCount => _plan?.LineCount ?? 0;
        public int ParagraphCount => _plan?.ParagraphCount ?? 0;
        public int AppearanceSpanCount => _plan?.AppearanceSpanCount ?? 0;
        public TextStudioReadOnlyBuffer<TextStudioAppearanceSpan> AppearanceSpans =>
            new TextStudioReadOnlyBuffer<TextStudioAppearanceSpan>(_plan?.AppearanceSpans, AppearanceSpanCount);
        public int EffectBindingCount => _plan?.EffectBindingCount ?? 0;
        public int CharacterSubstitutionBindingCount => _plan?.CharacterSubstitutionBindingCount ?? 0;
        public int ActionMarkerCount => _plan?.ActionMarkerCount ?? 0;
        public int ExtensionTagCount => _plan?.ExtensionTagCount ?? 0;
        public int InstantRangeCount => _plan?.InstantRangeCount ?? 0;
        public int RegionMapCount => _plan?.RegionMapCount ?? 0;
        public int VisibleGlyphCount => _plan?.VisibleGlyphCount ?? 0;
        public int TextUnitCount => _plan?.TextUnitCount ?? 0;

        public TextStudioReadOnlyBuffer<TextToken> Tokens =>
            new TextStudioReadOnlyBuffer<TextToken>(_plan?.Tokens, TokenCount);
        public TextStudioReadOnlyBuffer<GlyphRun> GlyphRuns =>
            new TextStudioReadOnlyBuffer<GlyphRun>(_plan?.GlyphRuns, GlyphRunCount);
        public TextStudioReadOnlyBuffer<WordRange> WordRanges =>
            new TextStudioReadOnlyBuffer<WordRange>(_plan?.WordRanges, WordCount);
        public TextStudioReadOnlyBuffer<LineRange> LineRanges =>
            new TextStudioReadOnlyBuffer<LineRange>(_plan?.LineRanges, LineCount);
        public TextStudioReadOnlyBuffer<ParagraphRange> ParagraphRanges =>
            new TextStudioReadOnlyBuffer<ParagraphRange>(_plan?.ParagraphRanges, ParagraphCount);
        public TextStudioReadOnlyBuffer<EffectBinding> EffectBindings =>
            new TextStudioReadOnlyBuffer<EffectBinding>(_plan?.EffectBindings, EffectBindingCount);
        public TextStudioReadOnlyBuffer<CharacterSubstitutionBinding> CharacterSubstitutionBindings =>
            new TextStudioReadOnlyBuffer<CharacterSubstitutionBinding>(
                _plan?.CharacterSubstitutionBindings,
                CharacterSubstitutionBindingCount);
        public TextStudioReadOnlyBuffer<ActionMarker> ActionMarkers =>
            new TextStudioReadOnlyBuffer<ActionMarker>(_plan?.ActionMarkers, ActionMarkerCount);
        public TextStudioReadOnlyBuffer<TextStudioExtensionTag> ExtensionTags =>
            new TextStudioReadOnlyBuffer<TextStudioExtensionTag>(_plan?.ExtensionTags, ExtensionTagCount);
        public TextStudioReadOnlyBuffer<TextStudioInstantRange> InstantRanges =>
            new TextStudioReadOnlyBuffer<TextStudioInstantRange>(_plan?.InstantRanges, InstantRangeCount);
        public TextStudioReadOnlyBuffer<RegionToGlyphMap> RegionMaps =>
            new TextStudioReadOnlyBuffer<RegionToGlyphMap>(_plan?.RegionMaps, RegionMapCount);
        public TextStudioReadOnlyBuffer<int> GlyphToWordIndex =>
            new TextStudioReadOnlyBuffer<int>(_plan?.GlyphToWordIndex, VisibleGlyphCount);
        public TextStudioReadOnlyBuffer<int> GlyphToLineIndex =>
            new TextStudioReadOnlyBuffer<int>(_plan?.GlyphToLineIndex, VisibleGlyphCount);
        public TextStudioReadOnlyBuffer<int> GlyphToParagraphIndex =>
            new TextStudioReadOnlyBuffer<int>(_plan?.GlyphToParagraphIndex, VisibleGlyphCount);
        public TextStudioReadOnlyBuffer<TextStudioTextUnit> TextUnits =>
            new TextStudioReadOnlyBuffer<TextStudioTextUnit>(_plan?.TextUnits, TextUnitCount);
        public TextStudioReadOnlyBuffer<int> ResolvedCharToTextUnitIndex =>
            new TextStudioReadOnlyBuffer<int>(
                _plan?.ResolvedCharToTextUnitIndex,
                _plan?.ResolvedText?.Length ?? 0);
        public TextStudioReadOnlyBuffer<int> DisplayCharToTextUnitIndex =>
            new TextStudioReadOnlyBuffer<int>(
                _plan?.DisplayCharToTextUnitIndex,
                _plan?.DisplayText?.Length ?? 0);
        public TextStudioReadOnlyBuffer<int> DisplayCharToResolvedCharIndex =>
            new TextStudioReadOnlyBuffer<int>(
                _plan?.DisplayCharToResolvedCharIndex,
                _plan?.DisplayText?.Length ?? 0);
        public TextStudioReadOnlyBuffer<DecorationBits> CharDecorations =>
            new TextStudioReadOnlyBuffer<DecorationBits>(_plan?.CharDecorations, VisibleGlyphCount);

        /// <summary>Gets semantic-unit metadata, or a default value for an invalid index.</summary>
        public TextStudioTextUnit GetTextUnit(int unitIndex) =>
            _plan != null ? _plan.GetTextUnit(unitIndex) : default;
        /// <summary>Gets the unit's start index in resolved plain text.</summary>
        public int GetResolvedStart(int unitIndex) =>
            _plan != null ? _plan.GetResolvedStart(unitIndex) : unitIndex;
        /// <summary>Gets the unit's UTF-16 length in resolved plain text.</summary>
        public int GetResolvedLength(int unitIndex) =>
            _plan != null ? _plan.GetResolvedLength(unitIndex) : 0;
        /// <summary>Maps a resolved UTF-16 character index to its semantic text unit.</summary>
        public int GetUnitIndexForResolvedChar(int resolvedCharIndex) =>
            _plan != null ? _plan.GetUnitIndexForResolvedChar(resolvedCharIndex) : resolvedCharIndex;
        /// <summary>Maps a display-text UTF-16 index to its semantic text unit, ignoring retained native markup.</summary>
        public int GetUnitIndexForDisplayChar(int displayCharIndex) =>
            _plan != null ? _plan.GetUnitIndexForDisplayChar(displayCharIndex) : displayCharIndex;
        /// <summary>Maps a display-text UTF-16 index to resolved plain text.</summary>
        public int GetResolvedIndexForDisplayChar(int displayCharIndex) =>
            _plan != null ? _plan.GetResolvedIndexForDisplayChar(displayCharIndex) : displayCharIndex;
        /// <summary>Maps a resolved character boundary to the corresponding semantic-unit boundary.</summary>
        public int GetUnitBoundaryForResolvedChar(int resolvedCharIndex) =>
            _plan != null ? _plan.GetUnitBoundaryForResolvedChar(resolvedCharIndex) : 0;
        /// <summary>Gets the first UTF-16 code unit of a semantic unit, or a null character when unavailable.</summary>
        public char GetPrimaryChar(int unitIndex) =>
            _plan != null ? _plan.GetPrimaryChar(unitIndex) : '\0';
        /// <summary>Gets the first Unicode code point of a semantic unit, or zero when unavailable.</summary>
        public int GetPrimaryCodePoint(int unitIndex) =>
            _plan != null ? _plan.GetPrimaryCodePoint(unitIndex) : 0;
        /// <summary>Allocates and returns the resolved substring represented by a semantic unit.</summary>
        public string GetUnitText(int unitIndex) =>
            _plan != null ? _plan.GetUnitText(unitIndex) : string.Empty;
        /// <summary>Reports whether every resolved character in a semantic unit is whitespace.</summary>
        public bool IsWhiteSpaceUnit(int unitIndex) =>
            _plan != null && _plan.IsWhiteSpaceUnit(unitIndex);
        /// <summary>Reports whether a semantic unit begins with a line-break character.</summary>
        public bool IsLineBreakUnit(int unitIndex) =>
            _plan != null && _plan.IsLineBreakUnit(unitIndex);
        /// <summary>Reports whether a semantic unit begins with Unicode punctuation.</summary>
        public bool IsPunctuationUnit(int unitIndex) =>
            _plan != null && _plan.IsPunctuationUnit(unitIndex);
    }

    /// <summary>Read-only view of the glyphs changed for the current frame.</summary>
    public readonly struct TextStudioDirtyGlyphView
    {
        readonly GlyphDirtyBits _dirty;

        internal TextStudioDirtyGlyphView(GlyphDirtyBits dirty)
        {
            _dirty = dirty;
        }

        public bool IsCreated => _dirty != null;
        /// <summary>Reports whether one visible glyph must be refreshed.</summary>
        public bool IsDirty(int glyphIndex) => _dirty != null && _dirty.IsDirty(glyphIndex);
        /// <summary>Reports whether every glyph in the requested prefix must be refreshed.</summary>
        public bool AreAllDirty(int glyphCount) => _dirty != null && _dirty.AreAllDirty(glyphCount);
        /// <summary>Reports whether any glyph in a bounded range must be refreshed.</summary>
        public bool AnyDirtyInRange(int start, int count) =>
            _dirty != null && _dirty.AnyDirtyInRange(start, count);
    }

    /// <summary>
    /// Read-only per-binding ownership selected by the hub after every relevant renderer accepted
    /// exact execution. A false result means the shared CPU evaluator remains authoritative.
    /// </summary>
    public readonly struct TextStudioRendererEffectExecutionView
    {
        readonly bool[] _rendererOwnedBindings;
        readonly int _bindingCount;

        internal TextStudioRendererEffectExecutionView(
            bool[] rendererOwnedBindings,
            int bindingCount)
        {
            _rendererOwnedBindings = rendererOwnedBindings;
            _bindingCount = rendererOwnedBindings != null
                ? Math.Max(0, Math.Min(bindingCount, rendererOwnedBindings.Length))
                : 0;
        }

        /// <summary>Whether the view contains a current hub decision.</summary>
        public bool IsCreated => _rendererOwnedBindings != null;

        /// <summary>Number of compiled effect bindings covered by this decision.</summary>
        public int BindingCount => _bindingCount;

        /// <summary>
        /// Reports whether the renderer set owns the complete binding. Invalid indexes return false
        /// so renderers preserve the CPU path when a plan or frame is stale.
        /// </summary>
        public bool IsRendererOwned(int bindingIndex)
        {
            return (uint)bindingIndex < (uint)_bindingCount &&
                   _rendererOwnedBindings[bindingIndex];
        }
    }

    /// <summary>Controlled writer for presentation-glyph substitutions.</summary>
    public readonly struct TextStudioPresentationGlyphWriter
    {
        readonly PresentationGlyph[] _glyphs;
        readonly int _count;

        internal TextStudioPresentationGlyphWriter(PresentationGlyph[] glyphs, int count)
        {
            _glyphs = glyphs;
            _count = glyphs != null ? Math.Max(0, Math.Min(count, glyphs.Length)) : 0;
        }

        internal PresentationGlyph[] Items => _glyphs;
        public bool IsCreated => _glyphs != null;
        public int Count => _count;

        public PresentationGlyph this[int glyphIndex]
        {
            get => Get(glyphIndex);
            set => Set(glyphIndex, in value);
        }

        /// <summary>Gets one mutable presentation-glyph value, throwing for an invalid index.</summary>
        public PresentationGlyph Get(int glyphIndex)
        {
            ValidateIndex(glyphIndex);
            return _glyphs[glyphIndex];
        }

        /// <summary>Replaces one presentation glyph, throwing for an invalid index.</summary>
        public void Set(int glyphIndex, in PresentationGlyph glyph)
        {
            ValidateIndex(glyphIndex);
            _glyphs[glyphIndex] = glyph;
        }

        /// <summary>Changes the renderer-specific glyph identifier for one presentation glyph.</summary>
        public void SetGlyphId(int glyphIndex, int glyphId)
        {
            ValidateIndex(glyphIndex);
            _glyphs[glyphIndex].glyphId = glyphId;
        }

        /// <summary>Sets the color tint override for one presentation glyph.</summary>
        public void SetTint(int glyphIndex, in UnityEngine.Color32 tint)
        {
            ValidateIndex(glyphIndex);
            _glyphs[glyphIndex].tintOverride = tint;
        }

        /// <summary>Sets the alpha multiplier for one presentation glyph.</summary>
        public void SetAlpha(int glyphIndex, float alphaMultiplier)
        {
            ValidateIndex(glyphIndex);
            _glyphs[glyphIndex].alphaMultiplier = alphaMultiplier;
        }

        /// <summary>Changes the explicit visibility flag for one presentation glyph.</summary>
        public void SetVisible(int glyphIndex, bool visible)
        {
            ValidateIndex(glyphIndex);
            if (visible)
                _glyphs[glyphIndex].flags |= PresentationFlags.Visible;
            else
                _glyphs[glyphIndex].flags &= ~PresentationFlags.Visible;
        }

        void ValidateIndex(int glyphIndex)
        {
            if ((uint)glyphIndex >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(glyphIndex));
        }
    }
}
