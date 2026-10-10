using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Serializable semantic range selection used to limit an effect to glyphs, words, lines, or authored ranges.</summary>
    [Serializable]
    public struct TextStudioEffectAreaSettings : IEquatable<TextStudioEffectAreaSettings>
    {
        [SerializeField] TextStudioEffectAreaMode _mode;
        [SerializeField, Min(1)] int _groupSize;

        public TextStudioEffectAreaMode Mode => _mode;
        public int GroupSize => Mathf.Max(1, _groupSize);

        public static TextStudioEffectAreaSettings Default => new TextStudioEffectAreaSettings(TextStudioEffectAreaMode.Glyph, 1);
        public static TextStudioEffectAreaSettings Range => new TextStudioEffectAreaSettings(TextStudioEffectAreaMode.Range, 1);

        /// <summary>Creates an area selection, clamping group size to at least one.</summary>
        public TextStudioEffectAreaSettings(TextStudioEffectAreaMode mode, int groupSize)
        {
            _mode = mode;
            _groupSize = Mathf.Max(1, groupSize);
        }

        public bool Equals(TextStudioEffectAreaSettings other)
        {
            return _mode == other._mode && GroupSize == other.GroupSize;
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioEffectAreaSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)_mode * 397) ^ GroupSize;
            }
        }
    }

    /// <summary>Renderer-neutral per-unit source geometry, color, semantic indices, and world-space snapshots for effect evaluation.</summary>
    public readonly struct TextStudioEffectUnitContext
    {
        public readonly TextStudioEffectAreaMode area;
        public readonly int groupSize;
        public readonly int unitIndex;
        public readonly int unitCount;
        public readonly int unitStartGlyph;
        public readonly int unitGlyphCount;
        public readonly int indexInUnit;
        public readonly float normalizedIndexInUnit;
        public readonly float normalizedUnitIndex;
        public readonly float normalizedIndexInBinding;
        public readonly int randomSeedIndex;
        public readonly float playbackOffset;

        /// <summary>Creates semantic indices and normalized positions for one evaluated unit.</summary>
        public TextStudioEffectUnitContext(
            TextStudioEffectAreaMode area,
            int groupSize,
            int unitIndex,
            int unitCount,
            int unitStartGlyph,
            int unitGlyphCount,
            int indexInUnit,
            float normalizedIndexInBinding,
            float playbackOffset)
        {
            this.area = area;
            this.groupSize = Mathf.Max(1, groupSize);
            this.unitIndex = Mathf.Max(0, unitIndex);
            this.unitCount = Mathf.Max(1, unitCount);
            this.unitStartGlyph = Mathf.Max(0, unitStartGlyph);
            this.unitGlyphCount = Mathf.Max(1, unitGlyphCount);
            this.indexInUnit = Mathf.Clamp(indexInUnit, 0, this.unitGlyphCount - 1);
            this.normalizedIndexInUnit = this.unitGlyphCount > 1 ? (float)this.indexInUnit / (this.unitGlyphCount - 1) : 0f;
            this.normalizedUnitIndex = this.unitCount > 1 ? (float)this.unitIndex / (this.unitCount - 1) : 0f;
            this.normalizedIndexInBinding = Mathf.Clamp01(normalizedIndexInBinding);
            this.randomSeedIndex = this.unitIndex;
            this.playbackOffset = Mathf.Max(0f, playbackOffset);
        }

        /// <summary>Creates per-glyph context when word, line, and paragraph segmentation are unavailable.</summary>
        public static TextStudioEffectUnitContext SingleGlyph(int glyphIndex, int totalGlyphs)
        {
            float normalized = totalGlyphs > 1 ? (float)glyphIndex / (totalGlyphs - 1) : 0f;
            return new TextStudioEffectUnitContext(
                TextStudioEffectAreaMode.Glyph,
                1,
                glyphIndex,
                Mathf.Max(1, totalGlyphs),
                glyphIndex,
                1,
                0,
                normalized,
                0f);
        }
    }
}
