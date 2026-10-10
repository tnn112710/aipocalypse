using System;
using TextStudio.Geometry;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace TextStudio
{
    /// <summary>One shaped glyph's field placement. Bounds and canonical font curves use em coordinates.</summary>
    [Serializable]
    public struct TextStudioPreparedGlyphField
    {
        [SerializeField] uint _glyphIndex;
        [SerializeField] int _page;
        [SerializeField] Rect _emBounds;
        [SerializeField] Rect _atlasUv;

        public uint GlyphIndex => _glyphIndex;
        public int Page => _page;
        public Rect EmBounds => _emBounds;
        public Rect AtlasUv => _atlasUv;

        /// <summary>Associates a shaped glyph identifier with its atlas page, em-space bounds, and atlas UV rectangle.</summary>
        public TextStudioPreparedGlyphField(uint glyphIndex, int page, Rect emBounds, Rect atlasUv)
        {
            _glyphIndex = glyphIndex;
            _page = page;
            _emBounds = emBounds;
            _atlasUv = atlasUv;
        }
    }

    /// <summary>Density and the full signed-distance decode interval are independent, explicit quantities.</summary>
    [Serializable]
    public sealed class TextStudioPreparedFieldTier
    {
        [SerializeField] float _pixelsPerEm;
        [SerializeField] float _distanceRangeEm;
        [SerializeField] Texture2D[] _pages = Array.Empty<Texture2D>();
        [SerializeField] TextStudioPreparedGlyphField[] _glyphs = Array.Empty<TextStudioPreparedGlyphField>();

        public float PixelsPerEm => _pixelsPerEm;
        public float DistanceRangeEm => _distanceRangeEm;
        public float SafeDistanceEm => _distanceRangeEm * .5f;
        public int PageCount => _pages?.Length ?? 0;
        public int GlyphCount => _glyphs?.Length ?? 0;
        public long TextureBytes
        {
            get
            {
                long bytes = 0;
                for (int index = 0; index < PageCount; index++)
                    if (_pages[index] != null) bytes += (long)_pages[index].width * _pages[index].height * 4;
                return bytes;
            }
        }

        /// <summary>Returns the field atlas texture at the supplied page index.</summary>
        public Texture2D GetPage(int index) => _pages[index];
        /// <summary>Returns the prepared glyph placement at the supplied index in the glyph-ID-sorted array.</summary>
        public TextStudioPreparedGlyphField GetGlyph(int index) => _glyphs[index];

        /// <summary>Creates a field tier with the supplied density and distance interval, copied page and glyph arrays, and glyphs sorted by identifier.</summary>
        public TextStudioPreparedFieldTier(float pixelsPerEm, float distanceRangeEm, Texture2D[] pages, TextStudioPreparedGlyphField[] glyphs)
        {
            _pixelsPerEm = pixelsPerEm;
            _distanceRangeEm = distanceRangeEm;
            _pages = pages == null ? Array.Empty<Texture2D>() : (Texture2D[])pages.Clone();
            _glyphs = glyphs == null ? Array.Empty<TextStudioPreparedGlyphField>() : (TextStudioPreparedGlyphField[])glyphs.Clone();
            Array.Sort(_glyphs, (first, second) => first.GlyphIndex.CompareTo(second.GlyphIndex));
        }

        /// <summary>Attempts to find a prepared field placement by shaped glyph identifier using the sorted glyph table.</summary>
        public bool TryGetGlyph(uint glyphIndex, out TextStudioPreparedGlyphField field)
        {
            int left = 0, right = GlyphCount - 1;
            while (left <= right)
            {
                int middle = left + (right - left) / 2;
                uint candidate = _glyphs[middle].GlyphIndex;
                if (candidate == glyphIndex) { field = _glyphs[middle]; return true; }
                if (candidate < glyphIndex) left = middle + 1;
                else right = middle - 1;
            }
            field = default;
            return false;
        }

        internal bool Validate(out string error)
        {
            error = string.Empty;
            if (!float.IsFinite(_pixelsPerEm) || _pixelsPerEm < 8f || _pixelsPerEm > 1024f ||
                !float.IsFinite(_distanceRangeEm) || _distanceRangeEm <= 0f || _distanceRangeEm > 8f)
                error = "The prepared field has an invalid density or distance interval.";
            for (int index = 0; index < PageCount && error.Length == 0; index++)
            {
                Texture2D page = _pages[index];
                if (page == null || page.mipmapCount != 1 || GraphicsFormatUtility.IsSRGBFormat(page.graphicsFormat) ||
                    page.format != TextureFormat.RGBA32 || page.wrapMode != TextureWrapMode.Clamp ||
                    page.width > TextStudioPreparedFieldPages.PageSize || page.height > TextStudioPreparedFieldPages.PageSize)
                    error = "Prepared fields require uncompressed linear RGBA32 pages without mipmaps and with clamp sampling.";
            }
            for (int index = 0; index < GlyphCount && error.Length == 0; index++)
            {
                TextStudioPreparedGlyphField glyph = _glyphs[index];
                if (glyph.Page < 0 || glyph.Page >= PageCount || index > 0 && _glyphs[index - 1].GlyphIndex >= glyph.GlyphIndex ||
                    !ValidRect(glyph.EmBounds) || !ValidRect(glyph.AtlasUv) || glyph.AtlasUv.xMin < 0f || glyph.AtlasUv.yMin < 0f ||
                    glyph.AtlasUv.xMax > 1f || glyph.AtlasUv.yMax > 1f)
                    error = "Prepared glyph IDs, bounds, or atlas placements are invalid.";
            }
            return error.Length == 0;
        }

        static bool ValidRect(Rect rect) => float.IsFinite(rect.x) && float.IsFinite(rect.y) &&
            float.IsFinite(rect.width) && float.IsFinite(rect.height) && rect.width > 0f && rect.height > 0f;
    }

    /// <summary>Build-safe font curves and MTSDF tiers. Players consume prepared fields and never execute a desktop baker.</summary>
    public sealed class TextStudioFontAppearanceData : ScriptableObject
    {
        public const int CurrentSchema = 1;
        public const string GeneratorVersion = "msdfgen-1.13-1874bcf7/ts-1";
        [SerializeField] int _schema = CurrentSchema;
        [SerializeField] Object _font;
        [SerializeField] string _sourceName;
        [SerializeField] string _sourceHash;
        [SerializeField] string _generator;
        [SerializeField] int _faceIndex;
        [SerializeField] byte[] _fontBytes = Array.Empty<byte>();
        [SerializeField] TextStudioPreparedFieldTier[] _tiers = Array.Empty<TextStudioPreparedFieldTier>();
        [SerializeField] uint _revision = 1;
        [NonSerialized] ICurveFontFace _face;
        [NonSerialized] TextStudioFontContourCache _contours;
        [NonSerialized] TextStudioAutomaticWritingPaths _writingPaths;
        [NonSerialized] bool _hasValidation;
        [NonSerialized] bool _validated;

        public Object Font => _font;
        public string SourceName => !string.IsNullOrEmpty(_sourceName) ? _sourceName : _font != null ? _font.name : "Font";
        public string SourceHash => _sourceHash ?? string.Empty;
        public string Generator => _generator ?? string.Empty;
        public int FaceIndex => _faceIndex;
        public uint Revision => _revision;
        public int TierCount => _tiers?.Length ?? 0;
        public int SourceBytes => _fontBytes?.Length ?? 0;
        /// <summary>Returns the prepared font-field tier at the supplied array index.</summary>
        public TextStudioPreparedFieldTier GetTier(int index) => _tiers[index];

        internal byte[] CopySourceBytes() => (byte[])_fontBytes.Clone();

        internal ICurveFontFace ExistingCurveSource => _face;
        internal long ContourPayloadBytes => _contours?.Bytes ?? 0;

        /// <summary>Returns the cached curve font face from embedded source bytes, or null for missing bytes or an outdated schema.</summary>
        public ICurveFontFace GetCurveSource()
        {
            if (_schema != CurrentSchema || SourceBytes == 0) return null;
            return _face ??= FontCurveSourceCache.GetOrCreate(_fontBytes, _faceIndex);
        }

        internal TextStudioPreparedWritingPath GetAutomaticWritingPath(uint glyph, bool outline) =>
            (_writingPaths ??= new TextStudioAutomaticWritingPaths()).Get(this, glyph, outline);

        internal TextStudio2DSurfaceGeometry GetStroke(uint glyphId, in TextStudio2DStrokeSpec stroke, float unitScale, float tolerance,
            TextStudioPixelContourProjection? projection = null, float nativeFaceOffsetEm = 0)
        {
            ICurveFontFace face = GetCurveSource();
            return face == null ? null : (_contours ??= new TextStudioFontContourCache()).GetStroke(face, glyphId, in stroke, unitScale, tolerance, projection, nativeFaceOffsetEm);
        }

        internal MeasuredContourSet GetContours(uint glyphId, float tolerance, float nativeFaceOffsetEm = 0)
        {
            ICurveFontFace face = GetCurveSource();
            return face == null ? null : (_contours ??= new TextStudioFontContourCache()).GetContours(face, glyphId, tolerance, nativeFaceOffsetEm);
        }

#if UNITY_EDITOR
#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        internal static Func<TextStudioFontAppearanceData, bool> EditorSourceValidator;
#endif
        internal bool IsSourceCurrent
        {
            get
            {
#if UNITY_EDITOR
                if (EditorSourceValidator != null && !EditorSourceValidator(this)) return false;
#endif
                return IsPreparedDataValid;
            }
        }

        internal bool IsPreparedDataValid
        {
            get
            {
                if (!_hasValidation) { _validated = Validate(out _); _hasValidation = true; }
                return _validated;
            }
        }

        /// <summary>Returns the smallest qualifying tier by texture payload for the required pixel density and safe distance, or -1 when none qualifies.</summary>
        public int SelectTier(float minimumPixelsPerEm, float minimumSafeDistanceEm)
        {
            int best = -1;
            long bestBytes = long.MaxValue;
            for (int index = 0; index < TierCount; index++)
            {
                TextStudioPreparedFieldTier tier = _tiers[index];
                if (tier == null || tier.GlyphCount == 0 || tier.PageCount == 0 || tier.PixelsPerEm + .001f < minimumPixelsPerEm ||
                    tier.SafeDistanceEm + .000001f < minimumSafeDistanceEm) continue;
                long bytes = tier.TextureBytes;
                if (best < 0 || bytes < bestBytes || bytes == bestBytes && (tier.PixelsPerEm < _tiers[best].PixelsPerEm ||
                    tier.PixelsPerEm == _tiers[best].PixelsPerEm && tier.DistanceRangeEm < _tiers[best].DistanceRangeEm))
                { best = index; bestBytes = bytes; }
            }
            return best;
        }

        /// <summary>Checks prepared font schema, embedded-source metadata, field tiers, and resource limits, returning a diagnostic on failure.</summary>
        public bool Validate(out string error)
        {
            error = string.Empty;
            if (_schema != CurrentSchema) error = "Prepare this font again with the current appearance data schema.";
            else if (SourceBytes == 0 || SourceHash.Length != 64 || _faceIndex < 0 || _generator != GeneratorVersion)
                error = "The prepared font source or generator metadata is incomplete.";
            else if (SourceBytes > 32 * 1024 * 1024) error = "The prepared font exceeds the 32 MiB source budget.";
            else
            {
                long textureBytes = 0;
                for (int index = 0; index < TierCount; index++)
                {
                    if (_tiers[index] == null) { error = "A prepared field tier is missing."; return false; }
                    if (!_tiers[index].Validate(out error)) return false;
                    textureBytes += _tiers[index].TextureBytes;
                }
                if (textureBytes > 64 * 1024 * 1024) error = "The prepared font exceeds the 64 MiB field budget.";
            }
            return error.Length == 0;
        }

        /// <summary>Checks embedded source identity and glyph coverage. Use during authoring or builds, outside frame evaluation.</summary>
        public bool ValidateSource(out string error)
        {
            if (!Validate(out error)) return false;
            try
            {
                ICurveFontFace face = GetCurveSource();
                string identity = face?.ContentHash;
                // The shared parser appends the sfnt offset for collection faces; this asset stores face identity separately.
                if (identity == null || identity.Length < 64 || !string.Equals(identity.Substring(0, 64), SourceHash, StringComparison.Ordinal))
                    error = "The prepared font's source hash does not match its embedded font bytes. Prepare it again.";
                else
                {
                    for (int tier = 0; tier < TierCount; tier++)
                    for (int glyph = 0; glyph < _tiers[tier].GlyphCount; glyph++)
                    {
                        if (_tiers[tier].GetGlyph(glyph).GlyphIndex < face.GlyphCount) continue;
                        error = "The prepared glyph inventory does not belong to its embedded font face. Prepare it again.";
                        return false;
                    }
                }
            }
            catch (Exception exception)
            {
                error = "The prepared font source cannot be read: " + exception.Message;
            }
            return error.Length == 0;
        }

        internal void Configure(Object font, byte[] bytes, int faceIndex, string sourceHash, TextStudioPreparedFieldTier[] tiers, string sourceName = null)
        {
            _schema = CurrentSchema;
            _font = font;
            _sourceName = !string.IsNullOrEmpty(sourceName) ? sourceName : font != null ? font.name : "Font";
            _fontBytes = bytes == null ? Array.Empty<byte>() : (byte[])bytes.Clone();
            _sourceHash = sourceHash;
            _faceIndex = faceIndex;
            _generator = GeneratorVersion;
            _tiers = tiers == null ? Array.Empty<TextStudioPreparedFieldTier>() : (TextStudioPreparedFieldTier[])tiers.Clone();
            ClearRuntimeCaches();
            _revision++;
        }

        internal void ClearRuntimeCaches() { _face = null; _contours = null; _hasValidation = false; _validated = false; }
        void OnDisable() => ClearRuntimeCaches();
    }
}
