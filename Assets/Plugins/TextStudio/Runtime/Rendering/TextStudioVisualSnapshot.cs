using System;
using UnityEngine;

namespace TextStudio
{
    [Flags]
    public enum TextStudioVisualCapabilities : uint
    {
        None = 0,
        LogicalVisualMapping = 1 << 0,
        ClusterMapping = 1 << 1,
        LineAndWordMapping = 1 << 2,
        GlyphMetrics = 1 << 3,
        VisualBounds = 1 << 4,
        Orientation = 1 << 5,
        AtlasCoordinates = 1 << 6,
        AtlasPages = 1 << 7,
        SignedDistanceField = 1 << 8,
        MultiChannelSignedDistanceField = 1 << 9,
        SourceVertexColors = 1 << 10,
        SourceColorBounds = 1 << 11,
        ContourDistance = 1 << 12,
        NativeColorGeometry = 1 << 13
    }

    /// <summary>Independent source revisions used to invalidate only dependent renderer caches.</summary>
    public readonly struct TextStudioVisualVersion : IEquatable<TextStudioVisualVersion>
    {
        public uint Layout { get; }
        public uint Geometry { get; }
        public uint Atlas { get; }
        public uint Materials { get; }

        /// <summary>Creates independent layout, geometry, atlas, and material revisions for visual-source cache invalidation.</summary>
        public TextStudioVisualVersion(uint layout, uint geometry, uint atlas, uint materials)
        {
            Layout = layout;
            Geometry = geometry;
            Atlas = atlas;
            Materials = materials;
        }

        public bool Equals(TextStudioVisualVersion other)
        {
            return Layout == other.Layout &&
                   Geometry == other.Geometry &&
                   Atlas == other.Atlas &&
                   Materials == other.Materials;
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioVisualVersion other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Layout;
                hash = hash * 397 ^ (int)Geometry;
                hash = hash * 397 ^ (int)Atlas;
                return hash * 397 ^ (int)Materials;
            }
        }
    }

    public enum TextStudioVisualSourceKind : byte { FontGlyph, ColorGlyph, InlineSprite }

    /// <summary>Renderer-neutral glyph source and its semantic text-unit owner.</summary>
    public readonly struct TextStudioVisualGlyph
    {
        public ulong FontIdentity { get; }
        /// <summary>Persistent shaping source identity, separate from the runtime resource identity. Zero means no durable identity was supplied.</summary>
        public ulong ShapingFontIdentity { get; }
        public uint GlyphIndex { get; }
        public int TextUnitIndex { get; }
        public int LogicalIndex { get; }
        public int VisualIndex { get; }
        public int ClusterStart { get; }
        public int ClusterLength { get; }
        public int LineIndex { get; }
        public int WordIndex { get; }
        public Vector2 Origin { get; }
        public float Advance { get; }
        public float Baseline { get; }
        public Vector2 BoundsMin { get; }
        public Vector2 BoundsMax { get; }
        public Vector2 Orientation { get; }
        public Vector4 AtlasUvRect { get; }
        public ushort AtlasPage { get; }
        public float DistanceFieldScale { get; }
        public float DistanceFieldBias { get; }
        public TextStudioGlyphFieldFormat FieldFormat { get; }
        public float FieldPixelsPerEm { get; }
        public float FieldSafeDistanceEm { get; }
        /// <summary>Native atlas UV to canonical font em: xy scale, zw offset.</summary>
        public Vector4 AtlasToEm { get; }
        /// <summary>Backend-local size of one em for this actual font glyph, before placement and animation.</summary>
        public float LocalEmSize { get; }
        public bool IsVisible { get; }
        public TextStudioVisualSourceKind SourceKind { get; }
        public int ColorSourceIndex { get; }

        /// <summary>Creates a renderer-neutral glyph record with semantic mappings, geometry, atlas and field metadata, normalized orientation, and optional color-source linkage.</summary>
        public TextStudioVisualGlyph(
            ulong fontIdentity,
            uint glyphIndex,
            int textUnitIndex,
            int logicalIndex,
            int visualIndex,
            int clusterStart,
            int clusterLength,
            int lineIndex,
            int wordIndex,
            Vector2 origin,
            float advance,
            float baseline,
            Vector2 boundsMin,
            Vector2 boundsMax,
            Vector2 orientation,
            Vector4 atlasUvRect,
            ushort atlasPage,
            float distanceFieldScale,
            float distanceFieldBias,
            bool isVisible,
            TextStudioGlyphFieldFormat fieldFormat =
                TextStudioGlyphFieldFormat.SingleDistance,
            float fieldPixelsPerEm = 0f,
            float fieldSafeDistanceEm = 0f,
            TextStudioVisualSourceKind sourceKind = TextStudioVisualSourceKind.FontGlyph,
            Vector4 atlasToEm = default, float localEmSize = 0, ulong shapingFontIdentity = 0, int colorSourceIndex = -1)
        {
            FontIdentity = fontIdentity;
            ShapingFontIdentity = shapingFontIdentity;
            GlyphIndex = glyphIndex;
            TextUnitIndex = textUnitIndex;
            LogicalIndex = logicalIndex;
            VisualIndex = visualIndex;
            ClusterStart = clusterStart;
            ClusterLength = clusterLength;
            LineIndex = lineIndex;
            WordIndex = wordIndex;
            Origin = origin;
            Advance = advance;
            Baseline = baseline;
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
            Orientation = orientation.sqrMagnitude > 0f ? orientation.normalized : Vector2.right;
            AtlasUvRect = atlasUvRect;
            AtlasPage = atlasPage;
            DistanceFieldScale = distanceFieldScale;
            DistanceFieldBias = distanceFieldBias;
            FieldFormat = fieldFormat;
            FieldPixelsPerEm = Mathf.Max(0f, fieldPixelsPerEm);
            FieldSafeDistanceEm = Mathf.Max(0f, fieldSafeDistanceEm);
            IsVisible = isVisible;
            SourceKind = sourceKind;
            ColorSourceIndex = colorSourceIndex;
            AtlasToEm = atlasToEm;
            LocalEmSize = float.IsFinite(localEmSize) ? Mathf.Max(0, localEmSize) : 0;
        }

        TextStudioVisualGlyph(in TextStudioVisualGlyph source, Vector2 minimum, Vector2 maximum, bool isVisible)
        {
            this = source;
            BoundsMin = minimum;
            BoundsMax = maximum;
            IsVisible = isVisible;
        }

        internal TextStudioVisualGlyph WithBounds(Vector2 minimum, Vector2 maximum, bool isVisible) => new TextStudioVisualGlyph(in this, minimum, maximum, isVisible);
    }

    /// <summary>A per-font companion record keeps resource references out of each visual glyph.</summary>
    public readonly struct TextStudioVisualFontSource
    {
        public ulong FontIdentity { get; }
        public TextStudioFontAppearanceData AppearanceData { get; }
        public uint Revision { get; }

        /// <summary>Associates a runtime font identity with prepared appearance data and captures its current revision.</summary>
        public TextStudioVisualFontSource(ulong fontIdentity, TextStudioFontAppearanceData appearanceData)
        {
            FontIdentity = fontIdentity;
            AppearanceData = appearanceData;
            Revision = appearanceData != null ? appearanceData.Revision : 0;
        }
    }

    /// <summary>
    /// Allocation-free visual-source view consumed by style, geometry, compositor, and extension renderers.
    /// Buffers remain engine-owned and are valid only until a later source layout publication.
    /// </summary>
    public readonly struct TextStudioVisualSnapshot
    {
        public TextStudioReadOnlyBuffer<TextStudioVisualGlyph> Glyphs { get; }
        public int GlyphCount { get; }
        /// <summary>Actual backend glyphs in visual draw order, each linked to its semantic text unit.</summary>
        public TextStudioReadOnlyBuffer<TextStudioVisualGlyph> RenderGlyphs { get; }
        public int RenderGlyphCount => RenderGlyphs.Length;
        readonly int[] _unitToRenderGlyph;
        public TextStudioVisualCapabilities Capabilities { get; }
        public TextStudioVisualVersion Version { get; }
        public Vector4 SourceBounds { get; }
        public TextStudioReadOnlyBuffer<TextStudioVisualFontSource> FontSources { get; }
        public int FontSourceCount { get; }
        public TextStudioReadOnlyBuffer<TextStudioColorSource> ColorSources { get; }
        public int ColorSourceCount => ColorSources.Length;

        public bool IsCreated => Glyphs.IsCreated;

        internal TextStudioVisualSnapshot(
            TextStudioVisualGlyph[] glyphs,
            int glyphCount,
            TextStudioVisualCapabilities capabilities,
            TextStudioVisualVersion version,
            Vector4 sourceBounds,
            TextStudioVisualFontSource[] fontSources = null,
            int fontSourceCount = 0,
            TextStudioVisualGlyph[] renderGlyphs = null, int renderGlyphCount = 0, int[] unitToRenderGlyph = null,
            TextStudioColorSource[] colorSources = null, int colorSourceCount = 0)
        {
            Glyphs = new TextStudioReadOnlyBuffer<TextStudioVisualGlyph>(glyphs, glyphCount);
            GlyphCount = glyphCount;
            RenderGlyphs = new TextStudioReadOnlyBuffer<TextStudioVisualGlyph>(renderGlyphs ?? glyphs, renderGlyphs != null ? renderGlyphCount : glyphCount);
            _unitToRenderGlyph = unitToRenderGlyph;
            Capabilities = capabilities;
            Version = version;
            SourceBounds = sourceBounds;
            FontSourceCount = fontSources == null ? 0 : Math.Min(Math.Max(0, fontSourceCount), fontSources.Length);
            FontSources = new TextStudioReadOnlyBuffer<TextStudioVisualFontSource>(fontSources, FontSourceCount);
            int colors = colorSources == null ? 0 : Math.Min(Math.Max(0, colorSourceCount), colorSources.Length);
            ColorSources = new TextStudioReadOnlyBuffer<TextStudioColorSource>(colorSources, colors);
            if (colors > 0) Capabilities |= TextStudioVisualCapabilities.NativeColorGeometry;
        }

        /// <summary>Returns the indexed color source for a non-font visual glyph, or null when no valid color-source link exists.</summary>
        public TextStudioColorSource GetColorSource(in TextStudioVisualGlyph glyph) =>
            glyph.SourceKind != TextStudioVisualSourceKind.FontGlyph && glyph.ColorSourceIndex >= 0 && glyph.ColorSourceIndex < ColorSourceCount
                ? ColorSources[glyph.ColorSourceIndex] : null;

        /// <summary>Attempts to find a visual font source with non-null appearance data by runtime font identity.</summary>
        public bool TryGetFontSource(ulong fontIdentity, out TextStudioVisualFontSource source)
        {
            for (int index = 0; index < FontSourceCount; index++)
                if (FontSources[index].FontIdentity == fontIdentity) { source = FontSources[index]; return source.AppearanceData != null; }
            source = default;
            return false;
        }

        /// <summary>Returns the backend glyph at the supplied index in visual draw order.</summary>
        public TextStudioVisualGlyph GetRenderGlyph(int index) => RenderGlyphs[index];

        // Visual glyphs are 160-byte records. Runtime passes read them in place instead of copying each glyph per pass.
        internal ref readonly TextStudioVisualGlyph RenderGlyphAt(int index)
        {
            if ((uint)index >= (uint)RenderGlyphs.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return ref RenderGlyphs.Items[index];
        }

        /// <summary>Returns the backend render-glyph index for a semantic unit, or -1 when the unit is outside the snapshot.</summary>
        public int GetRenderGlyphIndexForUnit(int unitIndex) => unitIndex >= 0 && unitIndex < GlyphCount
            ? (_unitToRenderGlyph != null ? _unitToRenderGlyph[unitIndex] : unitIndex) : -1;

        /// <summary>Returns the semantic-unit glyph record at the supplied index, throwing for an uncreated snapshot or invalid index.</summary>
        public TextStudioVisualGlyph GetGlyph(int index)
        {
            if (!Glyphs.IsCreated || index < 0 || index >= GlyphCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return Glyphs[index];
        }
    }
}
