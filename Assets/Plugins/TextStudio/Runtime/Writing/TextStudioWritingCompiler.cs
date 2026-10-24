using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEngine;

namespace TextStudio
{
    /// <summary>One allocation-free sample in a prepared stroke-distance stream.</summary>
    public readonly struct TextStudioPreparedWritingPoint
    {
        public Vector2 Position { get; }
        public Vector2 Tangent { get; }
        public Vector2 Normal { get; }
        public float Pressure { get; }
        public float Time { get; }
        public float CumulativeDistance { get; }
        public float NormalizedDistance { get; }

        internal TextStudioPreparedWritingPoint(
            Vector2 position,
            Vector2 tangent,
            float pressure,
            float time,
            float cumulativeDistance,
            float normalizedDistance)
        {
            Position = position;
            Tangent = tangent.sqrMagnitude > 0.00000001f
                ? tangent.normalized
                : Vector2.right;
            Normal = new Vector2(-Tangent.y, Tangent.x);
            Pressure = Mathf.Max(0f, pressure);
            Time = Mathf.Max(0f, time);
            CumulativeDistance = Mathf.Max(0f, cumulativeDistance);
            NormalizedDistance = Mathf.Clamp01(normalizedDistance);
        }
    }

    /// <summary>Prepared pen-down range with true-distance playback metadata.</summary>
    public readonly struct TextStudioPreparedWritingStroke
    {
        public TextStudioStackItemId StrokeId { get; }
        public TextStudioWritingStrokeRole Role { get; }
        public int PointStart { get; }
        public int PointCount { get; }
        public float Length { get; }
        public float SequenceStart { get; }
        public float SequenceDuration { get; }
        public Rect Bounds { get; }
        public bool Closed { get; }

        internal TextStudioPreparedWritingStroke(
            TextStudioStackItemId strokeId,
            TextStudioWritingStrokeRole role,
            int pointStart,
            int pointCount,
            float length,
            float sequenceStart,
            float sequenceDuration,
            Rect bounds,
            bool closed)
        {
            StrokeId = strokeId;
            Role = role;
            PointStart = pointStart;
            PointCount = pointCount;
            Length = Mathf.Max(0f, length);
            SequenceStart = Mathf.Clamp01(sequenceStart);
            SequenceDuration = Mathf.Clamp01(sequenceDuration);
            Bounds = bounds;
            Closed = closed;
        }
    }

    /// <summary>Immutable adaptively flattened path prepared only when authored data changes.</summary>
    public sealed class TextStudioPreparedWritingPath
    {
        readonly TextStudioPreparedWritingPoint[] _points;
        readonly TextStudioPreparedWritingStroke[] _strokes;

        internal TextStudioPreparedWritingPath(
            TextStudioPreparedWritingPoint[] points,
            TextStudioPreparedWritingStroke[] strokes,
            Rect bounds,
            float totalLength,
            ulong fingerprint)
        {
            _points = points ?? Array.Empty<TextStudioPreparedWritingPoint>();
            _strokes = strokes ?? Array.Empty<TextStudioPreparedWritingStroke>();
            Bounds = bounds;
            TotalLength = Mathf.Max(0f, totalLength);
            Fingerprint = fingerprint;
        }

        public int PointCount => _points.Length;
        public int StrokeCount => _strokes.Length;
        public Rect Bounds { get; }
        public float TotalLength { get; }
        public ulong Fingerprint { get; }
        public bool IsEmpty => _strokes.Length == 0 || _points.Length == 0;

        /// <summary>Returns a prepared point with direction, path distance, pressure, and time data at the specified index.</summary>
        public TextStudioPreparedWritingPoint GetPoint(int index)
        {
            if (index < 0 || index >= _points.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _points[index];
        }

        /// <summary>Returns the prepared point range, identity, role, bounds, and playback interval of the stroke at the specified index.</summary>
        public TextStudioPreparedWritingStroke GetStroke(int index)
        {
            if (index < 0 || index >= _strokes.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _strokes[index];
        }

        internal TextStudioPreparedWritingPoint[] Points => _points;
        internal TextStudioPreparedWritingStroke[] Strokes => _strokes;
    }

    /// <summary>Immutable glyph identity, design metrics, and prepared centerline and outline paths.</summary>
    public readonly struct TextStudioCompiledWritingGlyph
    {
        public TextStudioWritingGlyphKey Key { get; }
        public Rect DesignBounds { get; }
        public float Advance { get; }
        public TextStudioPreparedWritingPath Centerline { get; }
        public TextStudioPreparedWritingPath Outline { get; }

        internal TextStudioCompiledWritingGlyph(
            TextStudioWritingGlyphKey key,
            Rect designBounds,
            float advance,
            TextStudioPreparedWritingPath centerline,
            TextStudioPreparedWritingPath outline)
        {
            Key = key;
            DesignBounds = designBounds;
            Advance = Mathf.Max(0f, advance);
            Centerline = centerline;
            Outline = outline;
        }
    }

    /// <summary>Immutable compiled glyph paths indexed by font, glyph, and variation identity.</summary>
    public sealed class CompiledWritingFont
    {
        readonly TextStudioCompiledWritingGlyph[] _glyphs;
        readonly Dictionary<TextStudioWritingGlyphKey, int> _lookup;

        internal CompiledWritingFont(
            float unitsPerEm,
            TextStudioCompiledWritingGlyph[] glyphs)
        {
            UnitsPerEm = Mathf.Max(1f, unitsPerEm);
            _glyphs = glyphs ?? Array.Empty<TextStudioCompiledWritingGlyph>();
            _lookup = new Dictionary<TextStudioWritingGlyphKey, int>(_glyphs.Length);
            for (int i = 0; i < _glyphs.Length; i++)
            {
                if (!_lookup.ContainsKey(_glyphs[i].Key))
                    _lookup.Add(_glyphs[i].Key, i);
            }
        }

        public float UnitsPerEm { get; }
        public int GlyphCount => _glyphs.Length;

        /// <summary>Returns the compiled metrics and writing paths of the glyph at the specified index.</summary>
        public TextStudioCompiledWritingGlyph GetGlyph(int index)
        {
            if (index < 0 || index >= _glyphs.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _glyphs[index];
        }

        /// <summary>Looks up compiled writing paths by font, glyph, and variation identity; returns false and a default glyph when no entry exists.</summary>
        public bool TryGetGlyph(
            TextStudioWritingGlyphKey key,
            out TextStudioCompiledWritingGlyph glyph)
        {
            if (_lookup.TryGetValue(key, out int index))
            {
                glyph = _glyphs[index];
                return true;
            }
            glyph = default;
            return false;
        }
    }

    /// <summary>Prepared centerline and outline paths for an exact resolved phrase and shaping fingerprint.</summary>
    public sealed class CompiledWritingPhrase
    {
        internal CompiledWritingPhrase(
            string resolvedText,
            ulong shapingFingerprint,
            Rect designBounds,
            TextStudioPreparedWritingPath centerline,
            TextStudioPreparedWritingPath outline)
        {
            ResolvedText = resolvedText ?? string.Empty;
            ShapingFingerprint = shapingFingerprint;
            DesignBounds = designBounds;
            Centerline = centerline;
            Outline = outline;
        }

        public string ResolvedText { get; }
        public ulong ShapingFingerprint { get; }
        public Rect DesignBounds { get; }
        public TextStudioPreparedWritingPath Centerline { get; }
        public TextStudioPreparedWritingPath Outline { get; }

        /// <summary>Checks exact resolved text equality and an equal, nonzero shaping fingerprint before reusing prepared phrase paths.</summary>
        public bool Matches(string resolvedText, ulong shapingFingerprint)
        {
            return ShapingFingerprint != 0UL &&
                   ShapingFingerprint == shapingFingerprint &&
                   string.Equals(
                       ResolvedText,
                       resolvedText ?? string.Empty,
                       StringComparison.Ordinal);
        }
    }

    /// <summary>Validated immutable writing asset and playback plan.</summary>
    public sealed class CompiledTextStudioWritingStyle
    {
        readonly TextStudioStackDiagnostic[] _diagnostics;

        internal CompiledTextStudioWritingStyle(
            TextStudioWritingConfiguration configuration,
            CompiledWritingFont font,
            CompiledWritingPhrase phrase,
            TextStudioStackDiagnostic[] diagnostics,
            TextStudioStackFingerprint fingerprint,
            ulong dependencyRevision)
        {
            Configuration = configuration;
            Font = font;
            Phrase = phrase;
            _diagnostics = diagnostics ?? Array.Empty<TextStudioStackDiagnostic>();
            Fingerprint = fingerprint;
            DependencyRevision = dependencyRevision;
            bool valid = true;
            for (int i = 0; i < _diagnostics.Length; i++)
            {
                if (_diagnostics[i].Severity == TextStudioStackDiagnosticSeverity.Error)
                {
                    valid = false;
                    break;
                }
            }
            IsValid = valid;
        }

        public TextStudioWritingConfiguration Configuration { get; }
        public CompiledWritingFont Font { get; }
        public CompiledWritingPhrase Phrase { get; }
        public int DiagnosticCount => _diagnostics.Length;
        public bool IsValid { get; }
        public bool IsPassThrough =>
            Configuration.Mode == TextStudioWritingMode.None;
        public TextStudioStackFingerprint Fingerprint { get; }
        public ulong DependencyRevision { get; }

        /// <summary>Returns the compiled validation diagnostic at the specified index, throwing if the index is out of range.</summary>
        public TextStudioStackDiagnostic GetDiagnostic(int index)
        {
            if (index < 0 || index >= _diagnostics.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _diagnostics[index];
        }
    }

    /// <summary>Identifies whether Writing resolved to a prepared phrase path, glyph paths, glyph coverage, ordinary Typewriter behavior, or no source.</summary>
    public enum TextStudioWritingSourceKind : byte
    {
        None = 0,
        PhrasePath = 1,
        GlyphPath = 2,
        GlyphCoverage = 3,
        Typewriter = 4
    }

    /// <summary>Describes the resolved phrase, shaping identity, glyph variation, and backend data available for writing path selection.</summary>
    public readonly struct TextStudioWritingResolveRequest
    {
        public string ResolvedText { get; }
        public ulong ShapingFingerprint { get; }
        public TextStudioWritingGlyphKey GlyphKey { get; }
        public bool HasGlyph { get; }
        public bool HasSignedDistanceField { get; }
        public TextStudioFontAppearanceData FontData { get; }

        /// <summary>Creates a writing path resolution request, normalizing null resolved text to empty and retaining glyph and font capabilities.</summary>
        public TextStudioWritingResolveRequest(
            string resolvedText,
            ulong shapingFingerprint,
            bool hasSignedDistanceField,
            TextStudioWritingGlyphKey glyphKey = default,
            bool hasGlyph = false,
            TextStudioFontAppearanceData fontData = null)
        {
            ResolvedText = resolvedText ?? string.Empty;
            ShapingFingerprint = shapingFingerprint;
            GlyphKey = glyphKey;
            HasGlyph = hasGlyph;
            FontData = fontData;
            HasSignedDistanceField = hasSignedDistanceField;
        }
    }

    /// <summary>One explicit deterministic result from the honest writing fallback ladder.</summary>
    public readonly struct TextStudioWritingResolution
    {
        public TextStudioWritingDataLevel RequestedLevel { get; }
        public TextStudioWritingDataLevel DataLevel { get; }
        public TextStudioWritingSourceKind SourceKind { get; }
        public TextStudioPreparedWritingPath Path { get; }
        public string DiagnosticCode { get; }

        internal TextStudioWritingResolution(
            TextStudioWritingDataLevel requestedLevel,
            TextStudioWritingDataLevel dataLevel,
            TextStudioWritingSourceKind sourceKind,
            TextStudioPreparedWritingPath path,
            string diagnosticCode)
        {
            RequestedLevel = requestedLevel;
            DataLevel = dataLevel;
            SourceKind = sourceKind;
            Path = path;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public bool IsFallback => DataLevel < RequestedLevel;
        public bool HasPreparedPath => Path != null && !Path.IsEmpty;
        public bool IsAutomatic => DiagnosticCode == "textstudio.writing.automatic";

        /// <summary>Maps automatic glyph atlas coordinates into design space when both atlas scales are positive; otherwise returns prepared path bounds or an empty rectangle.</summary>
        public Rect GetDesignBounds(in TextStudioVisualGlyph glyph)
        {
            if (!IsAutomatic || glyph.AtlasToEm.x <= 0 || glyph.AtlasToEm.y <= 0) return Path?.Bounds ?? default;
            Vector4 uv = glyph.AtlasUvRect, mapping = glyph.AtlasToEm;
            return Rect.MinMaxRect(uv.x * mapping.x + mapping.z, uv.y * mapping.y + mapping.w,
                uv.z * mapping.x + mapping.z, uv.w * mapping.y + mapping.w);
        }
    }

    /// <summary>Selects compatible phrase or glyph paths and reports fallback to ink coverage or ordinary typewriter reveal.</summary>
    public static class TextStudioWritingResolver
    {
        const string FallbackOutline = "textstudio.writing.fallback.outline";
        const string FallbackInk = "textstudio.writing.fallback.ink";
        const string FallbackTypewriter = "textstudio.writing.fallback.typewriter";

        /// <summary>Chooses compatible authored phrase or glyph paths, then automatic font paths, ink coverage, or ordinary typewriter reveal, reporting any required fallback.</summary>
        public static TextStudioWritingResolution Resolve(
            CompiledTextStudioWritingStyle style,
            in TextStudioWritingResolveRequest request)
        {
            TextStudioWritingDataLevel requested = style != null
                ? TextStudioWritingModeUtility.RequestedDataLevel(
                    style.Configuration.Mode)
                : TextStudioWritingDataLevel.None;
            if (style == null || requested == TextStudioWritingDataLevel.None)
            {
                return new TextStudioWritingResolution(
                    requested,
                    TextStudioWritingDataLevel.None,
                    TextStudioWritingSourceKind.None,
                    null,
                    string.Empty);
            }

            if (requested == TextStudioWritingDataLevel.OrdinaryTypewriter)
            {
                return new TextStudioWritingResolution(
                    requested,
                    requested,
                    TextStudioWritingSourceKind.Typewriter,
                    null,
                    string.Empty);
            }

            if (requested == TextStudioWritingDataLevel.AuthoredCenterline)
            {
                if (TryResolvePhrase(
                        style.Phrase,
                        request.ResolvedText,
                        request.ShapingFingerprint,
                        true,
                        out TextStudioPreparedWritingPath phraseCenterline))
                {
                    return ResolvedPath(
                        requested,
                        TextStudioWritingDataLevel.AuthoredCenterline,
                        TextStudioWritingSourceKind.PhrasePath,
                        phraseCenterline,
                        string.Empty);
                }
                if (TryResolveGlyph(
                        style.Font,
                        request.GlyphKey,
                        request.HasGlyph,
                        true,
                        out TextStudioPreparedWritingPath glyphCenterline))
                {
                    return ResolvedPath(
                        requested,
                        TextStudioWritingDataLevel.AuthoredCenterline,
                        TextStudioWritingSourceKind.GlyphPath,
                        glyphCenterline,
                        string.Empty);
                }
            }

            if (requested >= TextStudioWritingDataLevel.PreparedOutline)
            {
                if (TryResolvePhrase(
                        style.Phrase,
                        request.ResolvedText,
                        request.ShapingFingerprint,
                        false,
                        out TextStudioPreparedWritingPath phraseOutline))
                {
                    return ResolvedPath(
                        requested,
                        TextStudioWritingDataLevel.PreparedOutline,
                        TextStudioWritingSourceKind.PhrasePath,
                        phraseOutline,
                        requested > TextStudioWritingDataLevel.PreparedOutline
                            ? FallbackOutline
                            : string.Empty);
                }
                if (TryResolveGlyph(
                        style.Font,
                        request.GlyphKey,
                        request.HasGlyph,
                        false,
                        out TextStudioPreparedWritingPath glyphOutline))
                {
                    return ResolvedPath(
                        requested,
                        TextStudioWritingDataLevel.PreparedOutline,
                        TextStudioWritingSourceKind.GlyphPath,
                        glyphOutline,
                        requested > TextStudioWritingDataLevel.PreparedOutline
                            ? FallbackOutline
                            : string.Empty);
                }
            }

            if (requested >= TextStudioWritingDataLevel.PreparedOutline && request.HasGlyph && request.FontData != null)
            {
                TextStudioPreparedWritingPath automatic = request.FontData.GetAutomaticWritingPath(
                    request.GlyphKey.GlyphIndex, style.Configuration.Mode == TextStudioWritingMode.OutlineTrace);
                if (automatic != null && !automatic.IsEmpty)
                    return new TextStudioWritingResolution(requested, TextStudioWritingDataLevel.PreparedOutline,
                        TextStudioWritingSourceKind.GlyphPath, automatic, "textstudio.writing.automatic");
            }
            if (request.HasSignedDistanceField)
            {
                return new TextStudioWritingResolution(
                    requested,
                    TextStudioWritingDataLevel.InkCoverage,
                    TextStudioWritingSourceKind.GlyphCoverage,
                    null,
                    requested > TextStudioWritingDataLevel.InkCoverage
                        ? FallbackInk
                        : string.Empty);
            }

            return new TextStudioWritingResolution(
                requested,
                TextStudioWritingDataLevel.OrdinaryTypewriter,
                TextStudioWritingSourceKind.Typewriter,
                null,
                FallbackTypewriter);
        }

        static TextStudioWritingResolution ResolvedPath(
            TextStudioWritingDataLevel requested,
            TextStudioWritingDataLevel resolved,
            TextStudioWritingSourceKind source,
            TextStudioPreparedWritingPath path,
            string diagnosticCode)
        {
            return new TextStudioWritingResolution(
                requested,
                resolved,
                source,
                path,
                diagnosticCode);
        }

        static bool TryResolvePhrase(
            CompiledWritingPhrase phrase,
            string resolvedText,
            ulong shapingFingerprint,
            bool centerline,
            out TextStudioPreparedWritingPath path)
        {
            path = centerline ? phrase?.Centerline : phrase?.Outline;
            return phrase != null &&
                   phrase.Matches(resolvedText, shapingFingerprint) &&
                   path != null &&
                   !path.IsEmpty;
        }

        static bool TryResolveGlyph(
            CompiledWritingFont font,
            TextStudioWritingGlyphKey key,
            bool hasGlyph,
            bool centerline,
            out TextStudioPreparedWritingPath path)
        {
            path = null;
            if (!hasGlyph || font == null ||
                !font.TryGetGlyph(key, out TextStudioCompiledWritingGlyph glyph))
            {
                return false;
            }
            path = centerline ? glyph.Centerline : glyph.Outline;
            return path != null && !path.IsEmpty;
        }
    }

    /// <summary>Computes shaped phrase identities from text, glyph variation, clustering, and layout without translation or backend unit scale.</summary>
    public static class TextStudioWritingShapingFingerprint
    {
        const float LayoutQuantization = 4096f;
        const float MinimumLayoutExtent = 0.000001f;

        /// <summary>
        /// Builds a shaped-phrase identity from text, glyph variation, clustering, and relative layout.
        /// Translation and backend unit scale are intentionally removed because prepared phrase paths are
        /// mapped from their design bounds into the current text bounds at render time.
        /// </summary>
        public static ulong Compute(
            string resolvedText,
            in TextStudioVisualSnapshot snapshot)
        {
            TextStudioDeterministicHash64 hash = default;
            hash.Add(resolvedText ?? string.Empty);
            hash.Add(snapshot.RenderGlyphCount);
            const TextStudioVisualCapabilities shapingCapabilities = TextStudioVisualCapabilities.LogicalVisualMapping |
                TextStudioVisualCapabilities.ClusterMapping | TextStudioVisualCapabilities.LineAndWordMapping |
                TextStudioVisualCapabilities.GlyphMetrics | TextStudioVisualCapabilities.VisualBounds | TextStudioVisualCapabilities.Orientation;
            hash.Add((uint)(snapshot.Capabilities & shapingCapabilities));
            Vector4 bounds = snapshot.SourceBounds;
            float width = Mathf.Abs(bounds.z - bounds.x) >=
                          MinimumLayoutExtent
                ? bounds.z - bounds.x
                : 1f;
            float height = Mathf.Abs(bounds.w - bounds.y) >=
                           MinimumLayoutExtent
                ? bounds.w - bounds.y
                : 1f;
            for (int i = 0; i < snapshot.RenderGlyphCount; i++)
            {
                ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(i);
                hash.Add(glyph.ShapingFontIdentity != 0 ? glyph.ShapingFontIdentity : glyph.FontIdentity);
                hash.Add(glyph.GlyphIndex);
                hash.Add(glyph.TextUnitIndex);
                hash.Add(glyph.ClusterStart);
                hash.Add(glyph.ClusterLength);
                hash.Add(glyph.LineIndex);
                hash.Add(glyph.WordIndex);
                AddNormalizedPoint(
                    ref hash,
                    glyph.Origin,
                    bounds.x,
                    bounds.y,
                    width,
                    height);
                hash.Add(Quantize(glyph.Advance / width));
                hash.Add(Quantize((glyph.Baseline - bounds.y) / height));
                AddNormalizedPoint(
                    ref hash,
                    glyph.BoundsMin,
                    bounds.x,
                    bounds.y,
                    width,
                    height);
                AddNormalizedPoint(
                    ref hash,
                    glyph.BoundsMax,
                    bounds.x,
                    bounds.y,
                    width,
                    height);
                hash.Add(Quantize(glyph.Orientation.x));
                hash.Add(Quantize(glyph.Orientation.y));
            }
            return hash.Value;
        }

        static void AddNormalizedPoint(
            ref TextStudioDeterministicHash64 hash,
            Vector2 point,
            float minimumX,
            float minimumY,
            float width,
            float height)
        {
            hash.Add(Quantize((point.x - minimumX) / width));
            hash.Add(Quantize((point.y - minimumY) / height));
        }

        static int Quantize(float value)
        {
            if (!float.IsFinite(value))
                return 0;
            float bounded = Mathf.Clamp(value, -65536f, 65536f);
            return Mathf.RoundToInt(bounded * LayoutQuantization);
        }
    }

    /// <summary>Compiles writing assets into bounded true-distance paths and deterministic fallback data.</summary>
    public static class TextStudioWritingCompiler
    {
        const string DiagnosticPrefix = "textstudio.writing.";
        const int MaximumGlyphs = 65536;
        const int MaximumStrokes = 4096;
        const int MaximumPointsPerStroke = 4096;
        const int MaximumPointsPerAsset = 262144;
        const int MaximumSubdivisionDepth = 12;
        const float PositionEpsilonSquared = 0.0000000001f;

        struct RawPoint
        {
            internal Vector2 Position;
            internal float Pressure;
            internal float Time;
        }

        struct PendingSegment
        {
            internal Vector2 P0;
            internal Vector2 P1;
            internal Vector2 P2;
            internal Vector2 P3;
            internal float Pressure0;
            internal float Pressure1;
            internal float Time0;
            internal float Time1;
            internal int Depth;
        }

        /// <summary>Compiles a writing style and its authored paths into an immutable plan with validation diagnostics; a null style produces a pass-through plan.</summary>
        public static CompiledTextStudioWritingStyle Compile(
            TextStudioWritingStyleAsset style)
        {
            List<TextStudioStackDiagnostic> diagnostics =
                new List<TextStudioStackDiagnostic>();
            TextStudioWritingConfiguration configuration = style != null
                ? style.Configuration
                : TextStudioWritingConfiguration.Default;
            if (style == null)
            {
                configuration = new TextStudioWritingConfiguration(
                    TextStudioWritingMode.None,
                    TextStudioWritingDirection.LeftToRight,
                    Color.white);
            }

            float tolerance = style != null
                ? style.FlatteningTolerance
                : 0.0025f;
            CompiledWritingFont font = CompileFont(
                style != null ? style.FontAsset : null,
                tolerance,
                diagnostics);
            CompiledWritingPhrase phrase = CompilePhrase(
                style != null ? style.PhraseAsset : null,
                tolerance,
                diagnostics);
            ValidateRequestedData(configuration.Mode, font, phrase, diagnostics);

            TextStudioStackFingerprint fingerprint = BuildFingerprint(
                in configuration,
                font,
                phrase,
                tolerance);
            return new CompiledTextStudioWritingStyle(
                configuration,
                font,
                phrase,
                diagnostics.ToArray(),
                fingerprint,
                ComputeDependencyRevision(style));
        }

        /// <summary>Hashes the style, font, and phrase asset identities, revisions, and schemas for compiled plan invalidation.</summary>
        public static ulong ComputeDependencyRevision(
            TextStudioWritingStyleAsset style)
        {
            TextStudioDeterministicHash64 hash = default;
            if (style == null)
                return hash.Value;
            hash.Add(style.GetStableId());
            hash.Add(style.Revision);
            hash.Add(style.SchemaVersion);
            WritingFontAsset font = style.FontAsset;
            if (font != null)
            {
                hash.Add(font.GetStableId());
                hash.Add(font.Revision);
                hash.Add(font.SchemaVersion);
            }
            WritingPhraseAsset phrase = style.PhraseAsset;
            if (phrase != null)
            {
                hash.Add(phrase.GetStableId());
                hash.Add(phrase.Revision);
                hash.Add(phrase.SchemaVersion);
            }
            return hash.Value;
        }

        /// <summary>Classifies changed plan inputs using their fingerprints, treating replacement of a missing plan as a full change.</summary>
        public static TextStudioStackChange ClassifyChanges(
            CompiledTextStudioWritingStyle previous,
            CompiledTextStudioWritingStyle current)
        {
            if (ReferenceEquals(previous, current))
                return TextStudioStackChange.None;
            if (previous == null || current == null)
                return TextStudioStackChange.All;
            return TextStudioStackFingerprintUtility.Classify(
                previous.Fingerprint,
                current.Fingerprint);
        }

        static CompiledWritingFont CompileFont(
            WritingFontAsset asset,
            float tolerance,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            if (asset == null)
                return null;
            int glyphCount = Mathf.Min(asset.GlyphCount, MaximumGlyphs);
            if (asset.GlyphCount > MaximumGlyphs)
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Error,
                    "glyph-capacity",
                    -1,
                    default,
                    "A writing font cannot exceed 65,536 authored glyph variations.");
            }

            List<TextStudioCompiledWritingGlyph> glyphs =
                new List<TextStudioCompiledWritingGlyph>(glyphCount);
            HashSet<TextStudioWritingGlyphKey> keys =
                new HashSet<TextStudioWritingGlyphKey>();
            for (int i = 0; i < glyphCount; i++)
            {
                TextStudioWritingGlyphRecord glyph = asset.GetGlyph(i);
                if (!keys.Add(glyph.Key))
                {
                    AddDiagnostic(
                        diagnostics,
                        TextStudioStackDiagnosticSeverity.Error,
                        "duplicate-glyph-key",
                        i,
                        default,
                        "Writing glyph keys must be unique for each font, glyph, and variation.");
                    continue;
                }

                TextStudioPreparedWritingPath centerline = CompilePath(
                    ReadStrokes(glyph, true),
                    TextStudioWritingStrokeRole.Centerline,
                    tolerance,
                    i,
                    diagnostics);
                TextStudioPreparedWritingPath outline = CompilePath(
                    ReadStrokes(glyph, false),
                    TextStudioWritingStrokeRole.Outline,
                    tolerance,
                    i,
                    diagnostics);
                glyphs.Add(new TextStudioCompiledWritingGlyph(
                    glyph.Key,
                    glyph.DesignBounds,
                    glyph.Advance,
                    centerline,
                    outline));
            }
            return new CompiledWritingFont(asset.UnitsPerEm, glyphs.ToArray());
        }

        static CompiledWritingPhrase CompilePhrase(
            WritingPhraseAsset asset,
            float tolerance,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            if (asset == null)
                return null;
            if (string.IsNullOrEmpty(asset.ResolvedText))
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Warning,
                    "phrase-text-empty",
                    -1,
                    default,
                    "The writing phrase has no resolved text and cannot match runtime shaping.");
            }
            if (asset.ShapingFingerprint == 0UL)
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Warning,
                    "phrase-fingerprint-missing",
                    -1,
                    default,
                    "The writing phrase needs a captured shaping fingerprint before it can match runtime text.");
            }

            TextStudioPreparedWritingPath centerline = CompilePath(
                ReadStrokes(asset, true),
                TextStudioWritingStrokeRole.Centerline,
                tolerance,
                -1,
                diagnostics);
            TextStudioPreparedWritingPath outline = CompilePath(
                ReadStrokes(asset, false),
                TextStudioWritingStrokeRole.Outline,
                tolerance,
                -1,
                diagnostics);
            return new CompiledWritingPhrase(
                asset.ResolvedText,
                asset.ShapingFingerprint,
                asset.DesignBounds,
                centerline,
                outline);
        }

        static TextStudioWritingStroke[] ReadStrokes(
            TextStudioWritingGlyphRecord glyph,
            bool centerline)
        {
            int count = centerline
                ? glyph.CenterlineStrokeCount
                : glyph.OutlineStrokeCount;
            TextStudioWritingStroke[] strokes =
                new TextStudioWritingStroke[count];
            for (int i = 0; i < count; i++)
            {
                strokes[i] = centerline
                    ? glyph.GetCenterlineStroke(i)
                    : glyph.GetOutlineStroke(i);
            }
            return strokes;
        }

        static TextStudioWritingStroke[] ReadStrokes(
            WritingPhraseAsset phrase,
            bool centerline)
        {
            int count = centerline
                ? phrase.CenterlineStrokeCount
                : phrase.OutlineStrokeCount;
            TextStudioWritingStroke[] strokes =
                new TextStudioWritingStroke[count];
            for (int i = 0; i < count; i++)
            {
                strokes[i] = centerline
                    ? phrase.GetCenterlineStroke(i)
                    : phrase.GetOutlineStroke(i);
            }
            return strokes;
        }

        static TextStudioPreparedWritingPath CompilePath(
            TextStudioWritingStroke[] source,
            TextStudioWritingStrokeRole expectedRole,
            float tolerance,
            int itemIndex,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            if (source == null || source.Length == 0)
                return EmptyPath();
            if (source.Length > MaximumStrokes)
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Error,
                    "stroke-capacity",
                    itemIndex,
                    default,
                    "One writing source cannot exceed 4,096 pen-down strokes.");
                return EmptyPath();
            }

            List<TextStudioPreparedWritingPoint> points =
                new List<TextStudioPreparedWritingPoint>();
            List<TextStudioPreparedWritingStroke> prepared =
                new List<TextStudioPreparedWritingStroke>(source.Length);
            List<float> strokeLengths = new List<float>(source.Length);
            Rect totalBounds = default;
            bool hasBounds = false;
            float totalLength = 0f;

            for (int strokeIndex = 0; strokeIndex < source.Length; strokeIndex++)
            {
                TextStudioWritingStroke stroke = source[strokeIndex];
                if (stroke.Role != expectedRole)
                {
                    AddDiagnostic(
                        diagnostics,
                        TextStudioStackDiagnosticSeverity.Error,
                        "stroke-role",
                        itemIndex,
                        stroke.StrokeId,
                        "A writing stroke is stored in a path collection with a different semantic role.");
                    continue;
                }
                if (stroke.KnotCount < 2)
                {
                    AddDiagnostic(
                        diagnostics,
                        TextStudioStackDiagnosticSeverity.Warning,
                        "stroke-too-short",
                        itemIndex,
                        stroke.StrokeId,
                        "A pen-down stroke needs at least two knots and was ignored.");
                    continue;
                }

                List<RawPoint> raw = FlattenStroke(
                    in stroke,
                    tolerance,
                    itemIndex,
                    diagnostics);
                if (raw.Count < 2)
                    continue;
                int pointStart = points.Count;
                float length = 0f;
                float[] distances = new float[raw.Count];
                for (int i = 1; i < raw.Count; i++)
                {
                    length += Vector2.Distance(
                        raw[i - 1].Position,
                        raw[i].Position);
                    distances[i] = length;
                }
                if (length <= 0.000001f)
                {
                    AddDiagnostic(
                        diagnostics,
                        TextStudioStackDiagnosticSeverity.Warning,
                        "stroke-degenerate",
                        itemIndex,
                        stroke.StrokeId,
                        "A zero-length writing stroke was ignored.");
                    continue;
                }

                Rect strokeBounds = new Rect(
                    raw[0].Position.x,
                    raw[0].Position.y,
                    0f,
                    0f);
                for (int i = 0; i < raw.Count; i++)
                {
                    Vector2 tangent = ResolveTangent(raw, i);
                    RawPoint rawPoint = raw[i];
                    points.Add(new TextStudioPreparedWritingPoint(
                        rawPoint.Position,
                        tangent,
                        rawPoint.Pressure,
                        rawPoint.Time,
                        distances[i],
                        distances[i] / length));
                    strokeBounds = Include(strokeBounds, rawPoint.Position);
                }
                prepared.Add(new TextStudioPreparedWritingStroke(
                    stroke.StrokeId,
                    stroke.Role,
                    pointStart,
                    raw.Count,
                    length,
                    0f,
                    0f,
                    strokeBounds,
                    stroke.Closed));
                strokeLengths.Add(length);
                totalLength += length;
                totalBounds = hasBounds ? Union(totalBounds, strokeBounds) : strokeBounds;
                hasBounds = true;

                if (points.Count > MaximumPointsPerAsset)
                {
                    AddDiagnostic(
                        diagnostics,
                        TextStudioStackDiagnosticSeverity.Error,
                        "point-capacity",
                        itemIndex,
                        stroke.StrokeId,
                        "One writing source cannot exceed 262,144 prepared path points.");
                    return EmptyPath();
                }
            }

            if (prepared.Count == 0)
                return EmptyPath();
            float sequenceCursor = 0f;
            for (int i = 0; i < prepared.Count; i++)
            {
                TextStudioPreparedWritingStroke stroke = prepared[i];
                float duration = totalLength > 0f
                    ? strokeLengths[i] / totalLength
                    : 1f / prepared.Count;
                prepared[i] = new TextStudioPreparedWritingStroke(
                    stroke.StrokeId,
                    stroke.Role,
                    stroke.PointStart,
                    stroke.PointCount,
                    stroke.Length,
                    sequenceCursor,
                    duration,
                    stroke.Bounds,
                    stroke.Closed);
                sequenceCursor += duration;
            }

            TextStudioPreparedWritingPoint[] pointArray = points.ToArray();
            TextStudioPreparedWritingStroke[] strokeArray = prepared.ToArray();
            ulong fingerprint = HashPreparedPath(pointArray, strokeArray);
            return new TextStudioPreparedWritingPath(
                pointArray,
                strokeArray,
                hasBounds ? totalBounds : default,
                totalLength,
                fingerprint);
        }

        static List<RawPoint> FlattenStroke(
            in TextStudioWritingStroke stroke,
            float tolerance,
            int itemIndex,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            List<RawPoint> output = new List<RawPoint>(32);
            TextStudioWritingKnot first = stroke.GetKnot(0);
            output.Add(new RawPoint
            {
                Position = first.Position,
                Pressure = first.Pressure,
                Time = first.Time
            });
            int segmentCount = stroke.Closed
                ? stroke.KnotCount
                : stroke.KnotCount - 1;
            Stack<PendingSegment> pending = new Stack<PendingSegment>(32);
            float toleranceSquared = tolerance * tolerance;
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                TextStudioWritingKnot start = stroke.GetKnot(segmentIndex);
                TextStudioWritingKnot end = stroke.GetKnot(
                    (segmentIndex + 1) % stroke.KnotCount);
                pending.Push(new PendingSegment
                {
                    P0 = start.Position,
                    P1 = start.OutgoingControl,
                    P2 = end.IncomingControl,
                    P3 = end.Position,
                    Pressure0 = start.Pressure,
                    Pressure1 = end.Pressure,
                    Time0 = start.Time,
                    Time1 = end.Time,
                    Depth = 0
                });
                while (pending.Count > 0)
                {
                    PendingSegment segment = pending.Pop();
                    if (segment.Depth >= MaximumSubdivisionDepth ||
                        IsFlatEnough(in segment, toleranceSquared))
                    {
                        AppendRawPoint(output, new RawPoint
                        {
                            Position = segment.P3,
                            Pressure = segment.Pressure1,
                            Time = segment.Time1
                        });
                    }
                    else
                    {
                        Subdivide(
                            in segment,
                            out PendingSegment left,
                            out PendingSegment right);
                        pending.Push(right);
                        pending.Push(left);
                    }

                    if (output.Count <= MaximumPointsPerStroke)
                        continue;
                    AddDiagnostic(
                        diagnostics,
                        TextStudioStackDiagnosticSeverity.Error,
                        "stroke-point-capacity",
                        itemIndex,
                        stroke.StrokeId,
                        "A writing stroke exceeded 4,096 adaptive samples. Simplify the path or increase its flattening tolerance.");
                    output.Clear();
                    return output;
                }
            }
            return output;
        }

        static bool IsFlatEnough(
            in PendingSegment segment,
            float toleranceSquared)
        {
            Vector2 chord = segment.P3 - segment.P0;
            float chordLengthSquared = chord.sqrMagnitude;
            if (chordLengthSquared <= PositionEpsilonSquared)
            {
                return Mathf.Max(
                    (segment.P1 - segment.P0).sqrMagnitude,
                    (segment.P2 - segment.P0).sqrMagnitude) <= toleranceSquared;
            }
            float firstCross = Cross(segment.P1 - segment.P0, chord);
            float secondCross = Cross(segment.P2 - segment.P0, chord);
            float maximumCrossSquared = Mathf.Max(
                firstCross * firstCross,
                secondCross * secondCross);
            return maximumCrossSquared <= toleranceSquared * chordLengthSquared;
        }

        static void Subdivide(
            in PendingSegment source,
            out PendingSegment left,
            out PendingSegment right)
        {
            Vector2 p01 = (source.P0 + source.P1) * 0.5f;
            Vector2 p12 = (source.P1 + source.P2) * 0.5f;
            Vector2 p23 = (source.P2 + source.P3) * 0.5f;
            Vector2 p012 = (p01 + p12) * 0.5f;
            Vector2 p123 = (p12 + p23) * 0.5f;
            Vector2 midpoint = (p012 + p123) * 0.5f;
            float pressure = (source.Pressure0 + source.Pressure1) * 0.5f;
            float time = (source.Time0 + source.Time1) * 0.5f;
            int depth = source.Depth + 1;
            left = new PendingSegment
            {
                P0 = source.P0,
                P1 = p01,
                P2 = p012,
                P3 = midpoint,
                Pressure0 = source.Pressure0,
                Pressure1 = pressure,
                Time0 = source.Time0,
                Time1 = time,
                Depth = depth
            };
            right = new PendingSegment
            {
                P0 = midpoint,
                P1 = p123,
                P2 = p23,
                P3 = source.P3,
                Pressure0 = pressure,
                Pressure1 = source.Pressure1,
                Time0 = time,
                Time1 = source.Time1,
                Depth = depth
            };
        }

        static void AppendRawPoint(List<RawPoint> points, RawPoint point)
        {
            if (points.Count > 0 &&
                (points[points.Count - 1].Position - point.Position).sqrMagnitude <=
                PositionEpsilonSquared)
            {
                RawPoint existing = points[points.Count - 1];
                existing.Pressure = point.Pressure;
                existing.Time = point.Time;
                points[points.Count - 1] = existing;
                return;
            }
            points.Add(point);
        }

        static Vector2 ResolveTangent(List<RawPoint> points, int index)
        {
            Vector2 tangent = index == 0
                ? points[1].Position - points[0].Position
                : index == points.Count - 1
                    ? points[index].Position - points[index - 1].Position
                    : points[index + 1].Position - points[index - 1].Position;
            if (tangent.sqrMagnitude > PositionEpsilonSquared)
                return tangent.normalized;
            for (int offset = 1; offset < points.Count; offset++)
            {
                int previous = index - offset;
                int next = index + offset;
                if (previous >= 0)
                {
                    tangent = points[index].Position - points[previous].Position;
                    if (tangent.sqrMagnitude > PositionEpsilonSquared)
                        return tangent.normalized;
                }
                if (next < points.Count)
                {
                    tangent = points[next].Position - points[index].Position;
                    if (tangent.sqrMagnitude > PositionEpsilonSquared)
                        return tangent.normalized;
                }
            }
            return Vector2.right;
        }

        static void ValidateRequestedData(
            TextStudioWritingMode mode,
            CompiledWritingFont font,
            CompiledWritingPhrase phrase,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            TextStudioWritingDataLevel requested =
                TextStudioWritingModeUtility.RequestedDataLevel(mode);
            if (requested < TextStudioWritingDataLevel.PreparedOutline)
                return;
            bool hasCenterline = HasPath(phrase?.Centerline);
            bool hasOutline = HasPath(phrase?.Outline);
            if (font != null)
            {
                for (int i = 0; i < font.GlyphCount; i++)
                {
                    TextStudioCompiledWritingGlyph glyph = font.GetGlyph(i);
                    hasCenterline |= HasPath(glyph.Centerline);
                    hasOutline |= HasPath(glyph.Outline);
                }
            }
            if (requested == TextStudioWritingDataLevel.AuthoredCenterline &&
                !hasCenterline)
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Warning,
                    "authored-centerline-missing",
                    -1,
                    default,
                    "This style requests true handwriting but has no authored centerline paths. Runtime will use the documented fallback ladder.");
            }
            if (!hasOutline && !hasCenterline)
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Information,
                    "prepared-path-missing",
                    -1,
                    default,
                    "No prepared path is available. Signed-distance coverage or ordinary typewriter fallback will be selected per backend.");
            }
        }

        static bool HasPath(TextStudioPreparedWritingPath path)
        {
            return path != null && !path.IsEmpty;
        }

        static TextStudioStackFingerprint BuildFingerprint(
            in TextStudioWritingConfiguration configuration,
            CompiledWritingFont font,
            CompiledWritingPhrase phrase,
            float tolerance)
        {
            TextStudioDeterministicHash64 topology = default;
            TextStudioDeterministicHash64 parameters = default;
            TextStudioDeterministicHash64 renderState = default;
            TextStudioDeterministicHash64 bounds = default;
            topology.Add((byte)configuration.Mode);
            topology.Add((byte)configuration.Cap);
            topology.Add((byte)configuration.Join);
            topology.Add((byte)configuration.ThreeDMode);
            topology.Add(configuration.ThreeDRadialSegments);
            topology.Add(tolerance);
            if (phrase != null)
            {
                topology.Add(phrase.ResolvedText);
                topology.Add(phrase.ShapingFingerprint);
                topology.Add(phrase.Centerline?.Fingerprint ?? 0UL);
                topology.Add(phrase.Outline?.Fingerprint ?? 0UL);
            }
            if (font != null)
            {
                topology.Add(font.UnitsPerEm);
                topology.Add(font.GlyphCount);
                for (int i = 0; i < font.GlyphCount; i++)
                {
                    TextStudioCompiledWritingGlyph glyph = font.GetGlyph(i);
                    topology.Add(glyph.Key.FontIdentity);
                    topology.Add(glyph.Key.GlyphIndex);
                    topology.Add(glyph.Key.VariationFingerprint);
                    topology.Add(glyph.Centerline?.Fingerprint ?? 0UL);
                    topology.Add(glyph.Outline?.Fingerprint ?? 0UL);
                }
            }

            parameters.Add((byte)configuration.Direction);
            parameters.Add((byte)configuration.Brush);
            parameters.Add(configuration.Color);
            parameters.Add(configuration.Width);
            parameters.Add(configuration.EdgeSoftness);
            parameters.Add(configuration.StrokeGap);
            parameters.Add(configuration.FillStart);
            parameters.Add(configuration.FillDuration);
            parameters.Add(configuration.Reverse);
            parameters.Add(configuration.ThreeDSurfaceOffset);
            parameters.Add(configuration.ThreeDReliefDepth);
            renderState.Add((byte)configuration.ReducedMotionBehavior);
            renderState.Add((byte)configuration.Mode);
            renderState.Add((byte)configuration.ThreeDMode);
            bounds.Add(configuration.Width);
            bounds.Add(configuration.MiterLimit);
            bounds.Add((byte)configuration.Cap);
            bounds.Add((byte)configuration.Join);
            bounds.Add((byte)configuration.ThreeDMode);
            bounds.Add(configuration.ThreeDSurfaceOffset);
            bounds.Add(configuration.ThreeDReliefDepth);

            TextStudioDeterministicHash64 complete = default;
            complete.Add(topology.Value);
            complete.Add(parameters.Value);
            complete.Add(renderState.Value);
            complete.Add(bounds.Value);
            return new TextStudioStackFingerprint(
                topology.Value,
                parameters.Value,
                renderState.Value,
                bounds.Value,
                complete.Value);
        }

        static ulong HashPreparedPath(
            TextStudioPreparedWritingPoint[] points,
            TextStudioPreparedWritingStroke[] strokes)
        {
            TextStudioDeterministicHash64 hash = default;
            hash.Add(points.Length);
            hash.Add(strokes.Length);
            for (int i = 0; i < points.Length; i++)
            {
                TextStudioPreparedWritingPoint point = points[i];
                hash.Add(point.Position);
                hash.Add(point.Tangent);
                hash.Add(point.Pressure);
                hash.Add(point.Time);
                hash.Add(point.CumulativeDistance);
            }
            for (int i = 0; i < strokes.Length; i++)
            {
                TextStudioPreparedWritingStroke stroke = strokes[i];
                hash.Add(stroke.StrokeId.Value);
                hash.Add((byte)stroke.Role);
                hash.Add(stroke.PointStart);
                hash.Add(stroke.PointCount);
                hash.Add(stroke.Length);
                hash.Add(stroke.Closed);
            }
            return hash.Value;
        }

        static TextStudioPreparedWritingPath EmptyPath()
        {
            return new TextStudioPreparedWritingPath(
                Array.Empty<TextStudioPreparedWritingPoint>(),
                Array.Empty<TextStudioPreparedWritingStroke>(),
                default,
                0f,
                0UL);
        }

        static float Cross(Vector2 first, Vector2 second)
        {
            return first.x * second.y - first.y * second.x;
        }

        static Rect Include(Rect bounds, Vector2 point)
        {
            float xMin = Mathf.Min(bounds.xMin, point.x);
            float yMin = Mathf.Min(bounds.yMin, point.y);
            float xMax = Mathf.Max(bounds.xMax, point.x);
            float yMax = Mathf.Max(bounds.yMax, point.y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static Rect Union(Rect first, Rect second)
        {
            return Rect.MinMaxRect(
                Mathf.Min(first.xMin, second.xMin),
                Mathf.Min(first.yMin, second.yMin),
                Mathf.Max(first.xMax, second.xMax),
                Mathf.Max(first.yMax, second.yMax));
        }

        static void AddDiagnostic(
            List<TextStudioStackDiagnostic> diagnostics,
            TextStudioStackDiagnosticSeverity severity,
            string code,
            int itemIndex,
            TextStudioStackItemId itemId,
            string message)
        {
            diagnostics.Add(new TextStudioStackDiagnostic(
                severity,
                DiagnosticPrefix + code,
                itemIndex,
                itemId,
                message));
        }
    }
}
