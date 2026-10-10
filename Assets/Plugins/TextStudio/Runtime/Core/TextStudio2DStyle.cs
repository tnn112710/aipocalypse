using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    public enum TextStudio2DLayerKind : byte
    {
        Fill = 0,
        Stroke = 1,
        InnerShadow = 2,
        OuterShadow = 3,
        InnerGlow = 4,
        OuterGlow = 5,
        Bevel = 6,
        FauxDepth = 7,
        Decoration = 8,
        Mask = 9,
        Group = 10,
        MaterialResponse = 11,
        GraphicEffect = 12,
        AnimatedEffect = 13,
        ColorAdjustment = 14,
        CompositeAdjustment = 15
    }

    public enum TextStudio2DExecutionPath : byte
    {
        Automatic = 0,
        FusedField = 1,
        RetainedGeometry = 2,
        HeroCompositor = 3
    }

    public enum TextStudio2DSourceApplicability : byte { Automatic, TextGlyphsOnly, TextAndColor, ColorOnly }
    public enum TextStudio2DColorFaceMode : byte { PreserveOriginal, Tint, SilhouetteFill }

    public enum TextStudio2DQualityTier : byte
    {
        Responsive = 0,
        Balanced = 1,
        Hero = 2
    }

    /// <summary>Requests source glyph-field density without coupling a style to one text backend.</summary>
    public enum TextStudio2DSourceFieldQuality : byte
    {
        Automatic = 0,
        Native = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>Controls whether overlapping glyphs complete one layer or their whole stack first.</summary>
    public enum TextStudio2DPainterOrder : byte
    {
        [InspectorName("By Layer")] LayerMajor = 0,
        [InspectorName("By Character")] GlyphMajor = 1
    }

    public enum TextStudio2DGroupOrder : byte
    {
        Inherit = 0,
        [InspectorName("By Layer")] ByLayer = 1,
        [InspectorName("By Character")] ByCharacter = 2
    }

    public enum TextStudio2DSharedSurfaceOrder : byte
    {
        [InspectorName("Before Characters")] BeforeCharacters = 0,
        [InspectorName("After Characters")] AfterCharacters = 1
    }

    /// <summary>Controls alpha-clipped shadow casting for world-space 2D output.</summary>
    public enum TextStudio2DWorldShadowMode : byte
    {
        Off = 0,
        AlphaClipped = 1
    }

    public enum TextStudio2DBlendMode : byte
    {
        Normal = 0,
        Add = 1,
        Multiply = 2,
        Screen = 3,
        Overlay = 4,
        SoftLight = 5,
        HardLight = 6,
        ColorDodge = 7,
        ColorBurn = 8,
        Difference = 9,
        Subtract = 10,
        Min = 11,
        Max = 12
    }

    public enum TextStudio2DPaintKind : byte
    {
        Solid = 0,
        LinearGradient = 1,
        RadialGradient = 2,
        ConicGradient = 3,
        Texture = 4,
        Pattern = 5,
        ProceduralNoise = 6,
        FourCornerGradient = 7,
        DistanceGradient = 8
    }

    public enum TextStudio2DMappingSpace : byte
    {
        Glyph = 0,
        Word = 1,
        Line = 2,
        Text = 3,
        Object = 4,
        Screen = 5,
        World = 6,
        Range = 7
    }

    /// <summary>Controls whether a field measurement is authored relative to the font em or in screen pixels.</summary>
    public enum TextStudio2DUnit : byte
    {
        Em = 0,
        Pixels = 1
    }

    /// <summary>Controls how an image is fitted into its selected paint frame.</summary>
    public enum TextStudio2DTextureFit : byte
    {
        Stretch = 0,
        Contain = 1,
        Cover = 2,
        Tile = 3
    }

    /// <summary>Chooses the signed-distance measurement used by a distance-gradient paint.</summary>
    public enum TextStudio2DDistanceDomain : byte
    {
        Automatic = 0,
        FillInterior = 1,
        StrokeBand = 2,
        EffectEdge = 3,
        ShapeInterior = 4,
        ShapeEdge = 5,
        DepthProgress = 6
    }

    /// <summary>Controls how scalar gradients continue outside their normalized zero-to-one domain.</summary>
    public enum TextStudio2DGradientSpread : byte
    {
        Clamp = 0,
        Repeat = 1,
        Mirror = 2
    }

    public enum TextStudio2DStrokeJoin : byte
    {
        Round = 0,
        Miter = 1,
        Bevel = 2
    }

    /// <summary>Controls whether a soft field is evaluated from each glyph SDF or from the composited text silhouette.</summary>
    public enum TextStudio2DSoftEffectRendering : byte
    {
        DistanceField = 0,
        TextWideBlur = 1
    }

    /// <summary>Controls which coordinates drive paint across a faux-depth sweep.</summary>
    public enum TextStudio2DFauxDepthPaintMapping : byte
    {
        Surface = 0,
        DepthProgress = 1
    }

    public enum TextStudio2DDecorationKind : byte
    {
        Underline = 0,
        Strikethrough = 1,
        Overline = 2,
        Background = 3,
        Border = 4
    }

    /// <summary>Controls the vertical reference used by an explicitly sized plate decoration.</summary>
    public enum TextStudio2DDecorationVerticalAnchor : byte
    {
        GlyphBounds = 0,
        Baseline = 1,
        LineBox = 2
    }

    /// <summary>Analytic end geometry available to background and border decorations.</summary>
    public enum TextStudio2DDecorationEndStyle : byte
    {
        RoundedCorners = 0,
        Square = 1,
        Round = 2,
        Chamfer = 3,
        Point = 4,
        Notch = 5
    }

    public enum TextStudio2DDecorationShape : byte
    {
        Plate = 0,
        RoundedRectangle = 1,
        Squircle = 2,
        Capsule = 3,
        Ellipse = 4,
        Triangle = 5,
        Hexagon = 6,
        Octagon = 7,
        Star = 8,
        CustomPolygon = 9,
        GlyphSilhouette = 10
    }

    public enum TextStudio2DDecorationTopology : byte
    {
        PerLine = 0,
        ConnectedRange = 1,
        PerFragment = 2
    }

    /// <summary>Common color transforms that can be further edited as an explicit matrix.</summary>
    public enum TextStudio2DColorAdjustmentPreset : byte
    {
        Custom = 0,
        Grayscale = 1,
        Sepia = 2,
        Invert = 3,
        HighContrast = 4,
        Desaturate = 5
    }

    /// <summary>Calibrated 2D surface responses evaluated from the shared signed-distance normal.</summary>
    public enum TextStudio2DMaterialResponseKind : byte
    {
        Emboss = 0,
        Deboss = 1,
        Satin = 2,
        Chrome = 3,
        GoldFoil = 4,
        Enamel = 5,
        BubbleGel = 6,
        Neon = 7,
        Ice = 8,
        Glass = 9,
        HolographicFoil = 10
    }

    public enum TextStudio2DStyleInheritanceMode : byte
    {
        Append = 0,
        OverrideMatchingIds = 1,
        Replace = 2
    }

    /// <summary>A color stop at a normalized position within a 2D paint gradient.</summary>
    [Serializable]
    public struct TextStudio2DGradientStop
    {
        [SerializeField, Range(0f, 1f)] float _position;
        [SerializeField] Color _color;

        public float Position => _position;
        public Color Color => _color;

        /// <summary>Creates a gradient stop with the supplied position and color.</summary>
        public TextStudio2DGradientStop(float position, Color color)
        {
            _position = position;
            _color = color;
        }
    }

    /// <summary>Four colors bilinearly interpolated across a normalized rectangular paint domain.</summary>
    [Serializable]
    public struct TextStudio2DFourCornerGradient
    {
        [SerializeField] Color _topLeft;
        [SerializeField] Color _topRight;
        [SerializeField] Color _bottomLeft;
        [SerializeField] Color _bottomRight;

        public Color TopLeft => _topLeft;
        public Color TopRight => _topRight;
        public Color BottomLeft => _bottomLeft;
        public Color BottomRight => _bottomRight;

        /// <summary>Creates a rectangular gradient from its top-left, top-right, bottom-left, and bottom-right colors.</summary>
        public TextStudio2DFourCornerGradient(
            Color topLeft,
            Color topRight,
            Color bottomLeft,
            Color bottomRight)
        {
            _topLeft = topLeft;
            _topRight = topRight;
            _bottomLeft = bottomLeft;
            _bottomRight = bottomRight;
        }

        /// <summary>Evaluates normalized coordinates where (0, 0) is bottom-left and (1, 1) is top-right.</summary>
        public Color Evaluate(float u, float v)
        {
            Color bottom = Color.Lerp(_bottomLeft, _bottomRight, u);
            Color top = Color.Lerp(_topLeft, _topRight, u);
            return Color.Lerp(bottom, top, v);
        }
    }

    /// <summary>Typed paint shared by fills, strokes, glows, shadows, and decorations.</summary>
    [Serializable]
    public partial struct TextStudio2DPaint
    {
        [SerializeField] TextStudio2DPaintKind _kind;
        [SerializeField] TextStudio2DMappingSpace _mapping;
        [SerializeField] TextStudio2DGradientSpread _spread;
        [SerializeField] Color _color;
        [SerializeField] TextStudio2DGradientStop[] _gradientStops;
        [SerializeField] TextStudio2DFourCornerGradient _fourCornerGradient;
        [SerializeField] float _angle;
        [SerializeField] Vector2 _center;
        [SerializeField, TextStudio2DParameter(TextStudio2DParameterKind.PaintScale)] Vector2 _scale;
        [SerializeField] Vector2 _offset;
        [SerializeField] Texture2D _texture;
        [SerializeField] string _resourceId;
        [SerializeField] TextStudio2DTextureFit _textureFit;
        [SerializeField] TextStudio2DDistanceDomain _distanceDomain;
        [SerializeField, TextStudio2DParameter(TextStudio2DParameterKind.DistanceRange)] Vector2 _distanceRange;
        [SerializeField, TextStudio2DParameter(TextStudio2DParameterKind.PatternScale), Tooltip("Noise frequency in mapping-space units, from 0.01 to 4096. The slider is logarithmic; the numeric input uses the exact frequency.")] float _noiseScale;
        [SerializeField, Range(0f, 1f), Tooltip("Blend fine noise detail into the base noise, from none (0) to full detail (1).")] float _noiseDetail;
        [SerializeField] int _noiseSeed;

        public TextStudio2DPaintKind Kind => _kind;
        public TextStudio2DMappingSpace Mapping => _mapping;
        public TextStudio2DGradientSpread Spread => _spread;
        public Color Color => _color;
        internal long CaptureManagedPayload(TextStudioAppearanceManagedMemory memory) => memory.Array(_gradientStops);

        public int GradientStopCount => _gradientStops?.Length ?? 0;
        public TextStudio2DFourCornerGradient FourCornerGradient => _fourCornerGradient;
        public float Angle => _angle;
        public Vector2 Center => _center;
        public Vector2 Scale => _scale;
        public Vector2 Offset => _offset;
        public Texture2D Texture => _texture;
        public string ResourceId => _resourceId ?? string.Empty;
        public TextStudio2DTextureFit TextureFit => _textureFit;
        public TextStudio2DDistanceDomain DistanceDomain => _distanceDomain;
        public Vector2 DistanceRange => _distanceRange;
        public float NoiseScale => _noiseScale;
        public float NoiseDetail => _noiseDetail;
        public int NoiseSeed => _noiseSeed;

        /// <summary>Creates solid-color paint mapped to each glyph.</summary>
        public static TextStudio2DPaint Solid(Color color)
        {
            return Defaults(TextStudio2DPaintKind.Solid, TextStudio2DMappingSpace.Glyph, color);
        }

        /// <summary>Creates linear, radial, or conic gradient paint with copied stops, the specified mapping and transform, and clamped spread.</summary>
        public static TextStudio2DPaint Gradient(
            TextStudio2DPaintKind kind,
            TextStudio2DMappingSpace mapping,
            TextStudio2DGradientStop[] stops,
            float angle = 0f,
            Vector2? center = null,
            Vector2? scale = null,
            Vector2 offset = default)
        {
            return Gradient(
                kind,
                mapping,
                stops,
                angle,
                center,
                scale,
                offset,
                TextStudio2DGradientSpread.Clamp);
        }

        /// <summary>Creates linear, radial, or conic gradient paint with copied stops and the specified mapping, transform, and spread mode.</summary>
        public static TextStudio2DPaint Gradient(
            TextStudio2DPaintKind kind,
            TextStudio2DMappingSpace mapping,
            TextStudio2DGradientStop[] stops,
            float angle,
            Vector2? center,
            Vector2? scale,
            Vector2 offset,
            TextStudio2DGradientSpread spread)
        {
            if (kind != TextStudio2DPaintKind.LinearGradient &&
                kind != TextStudio2DPaintKind.RadialGradient &&
                kind != TextStudio2DPaintKind.ConicGradient)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            TextStudio2DPaint paint = Defaults(kind, mapping, Color.white);
            paint._gradientStops = stops != null
                ? (TextStudio2DGradientStop[])stops.Clone()
                : Array.Empty<TextStudio2DGradientStop>();
            paint._angle = angle;
            paint._center = center ?? paint._center;
            paint._scale = scale ?? paint._scale;
            paint._offset = offset;
            paint._spread = spread;
            return paint;
        }

        /// <summary>Creates paint that interpolates four corner colors within the specified mapping space and transform.</summary>
        public static TextStudio2DPaint FourCorner(
            TextStudio2DMappingSpace mapping,
            TextStudio2DFourCornerGradient gradient,
            Vector2? center = null,
            Vector2? scale = null,
            Vector2 offset = default)
        {
            TextStudio2DPaint paint = Defaults(
                TextStudio2DPaintKind.FourCornerGradient,
                mapping,
                Color.white);
            paint._fourCornerGradient = gradient;
            paint._center = center ?? paint._center;
            paint._scale = scale ?? paint._scale;
            paint._offset = offset;
            return paint;
        }

        /// <summary>Creates a gradient driven by a layer-specific signed-distance measurement.</summary>
        public static TextStudio2DPaint DistanceGradient(
            TextStudio2DDistanceDomain domain,
            TextStudio2DGradientStop[] stops,
            Vector2 distanceRange,
            TextStudio2DGradientSpread spread = TextStudio2DGradientSpread.Clamp)
        {
            TextStudio2DPaint paint = Defaults(
                TextStudio2DPaintKind.DistanceGradient,
                TextStudio2DMappingSpace.Glyph,
                Color.white);
            paint._gradientStops = stops != null
                ? (TextStudio2DGradientStop[])stops.Clone()
                : Array.Empty<TextStudio2DGradientStop>();
            paint._distanceDomain = domain;
            paint._distanceRange = distanceRange;
            paint._spread = spread;
            return paint;
        }

        /// <summary>Creates texture paint with a stable resource identifier, mapping space, scale, offset, and optional tint.</summary>
        public static TextStudio2DPaint TexturePaint(
            Texture2D texture,
            string stableResourceId,
            TextStudio2DMappingSpace mapping = TextStudio2DMappingSpace.Glyph,
            Vector2? scale = null,
            Vector2 offset = default,
            Color? tint = null)
        {
            return TextureLikePaint(
                TextStudio2DPaintKind.Texture,
                texture,
                stableResourceId,
                mapping,
                scale,
                offset,
                tint);
        }

        /// <summary>Creates pattern paint with a stable texture resource identifier, mapping space, scale, offset, and optional tint.</summary>
        public static TextStudio2DPaint PatternPaint(
            Texture2D texture,
            string stableResourceId,
            TextStudio2DMappingSpace mapping = TextStudio2DMappingSpace.Glyph,
            Vector2? scale = null,
            Vector2 offset = default,
            Color? tint = null)
        {
            return TextureLikePaint(
                TextStudio2DPaintKind.Pattern,
                texture,
                stableResourceId,
                mapping,
                scale,
                offset,
                tint);
        }

        static TextStudio2DPaint TextureLikePaint(
            TextStudio2DPaintKind kind,
            Texture2D texture,
            string stableResourceId,
            TextStudio2DMappingSpace mapping,
            Vector2? scale,
            Vector2 offset,
            Color? tint)
        {
            TextStudio2DPaint paint = Defaults(kind, mapping, tint ?? Color.white);
            paint._texture = texture;
            paint._resourceId = stableResourceId ?? string.Empty;
            paint._scale = scale ?? paint._scale;
            paint._offset = offset;
            return paint;
        }

        /// <summary>Creates deterministic procedural noise paint with the specified color, frequency, detail, seed, and mapping space.</summary>
        public static TextStudio2DPaint Noise(
            Color color,
            float scale,
            float detail,
            int seed,
            TextStudio2DMappingSpace mapping = TextStudio2DMappingSpace.Text)
        {
            TextStudio2DPaint paint = Defaults(
                TextStudio2DPaintKind.ProceduralNoise,
                mapping,
                color);
            paint._noiseScale = scale;
            paint._noiseDetail = detail;
            paint._noiseSeed = seed;
            return paint;
        }

        /// <summary>Returns the gradient stop at the specified index, throwing if the index is outside the stop array.</summary>
        public TextStudio2DGradientStop GetGradientStop(int index)
        {
            if (_gradientStops == null || index < 0 || index >= _gradientStops.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _gradientStops[index];
        }

        internal TextStudio2DGradientStop[] CloneGradientStops()
        {
            return _gradientStops != null
                ? (TextStudio2DGradientStop[])_gradientStops.Clone()
                : Array.Empty<TextStudio2DGradientStop>();
        }

        internal TextStudio2DPaint Clone()
        {
            TextStudio2DPaint clone = this;
            clone._gradientStops = CloneGradientStops();
            return clone;
        }

        /// <summary>Returns a paint copy with the specified gradient spread mode marked as a local override.</summary>
        public TextStudio2DPaint WithSpread(TextStudio2DGradientSpread spread)
        {
            TextStudio2DPaint copy = Clone();
            copy._spread = spread;
            copy._overrides |= TextStudio2DPaintOverrides.Spread;
            return copy;
        }

        /// <summary>Returns a paint copy with the specified texture fitting mode marked as a local override.</summary>
        public TextStudio2DPaint WithTextureFit(TextStudio2DTextureFit fit)
        {
            TextStudio2DPaint copy = Clone();
            copy._textureFit = fit;
            copy._overrides |= TextStudio2DPaintOverrides.TextureFit;
            return copy;
        }

        static TextStudio2DPaint Defaults(
            TextStudio2DPaintKind kind,
            TextStudio2DMappingSpace mapping,
            Color color)
        {
            return new TextStudio2DPaint
            {
                _kind = kind,
                _mapping = mapping,
                _spread = TextStudio2DGradientSpread.Clamp,
                _color = color,
                _gradientStops = Array.Empty<TextStudio2DGradientStop>(),
                _fourCornerGradient = new TextStudio2DFourCornerGradient(
                    Color.white,
                    Color.white,
                    Color.black,
                    Color.black),
                _angle = 0f,
                _center = new Vector2(0.5f, 0.5f),
                _scale = Vector2.one,
                _offset = Vector2.zero,
                _texture = null,
                _resourceId = string.Empty,
                _textureFit = kind == TextStudio2DPaintKind.Pattern
                    ? TextStudio2DTextureFit.Tile
                    : TextStudio2DTextureFit.Stretch,
                _distanceDomain = TextStudio2DDistanceDomain.Automatic,
                _distanceRange = new Vector2(0f, 0.25f),
                _noiseScale = 8f,
                _noiseDetail = 0.5f,
                _noiseSeed = 0
            };
        }
    }

    [Serializable]
    public partial struct TextStudio2DFillSpec
    {
        [SerializeField, Tooltip("Expand the face with positive values or contract it with negative values, using the selected Units. Inline images use this only with Silhouette Fill; Preserve Original and Tint keep the original image edge.")] float _dilation;
        [SerializeField, TextStudio2DParameter(TextStudio2DParameterKind.Softness), Tooltip("Soften the face edge using the selected Units. Zero keeps a crisp antialiased edge. Inline images use this only with Silhouette Fill; Preserve Original and Tint keep the original image edge.")] float _softness;
        [SerializeField] TextStudio2DUnit _units;
        [SerializeField, Tooltip("Preserve Original keeps the native image. Tint multiplies it by this fill's paint. Silhouette Fill replaces it with paint. The topmost matching image fill owns the native face once.")]
        TextStudio2DColorFaceMode _colorFace;

        public float Dilation => _dilation;
        public TextStudio2DColorFaceMode ColorFace => _colorFace;
        public float Softness => _softness;
        public TextStudio2DUnit Units => _units;

        /// <summary>Creates a fill specification with dilation, edge softness, measurement units, and inline color-image handling.</summary>
        public TextStudio2DFillSpec(
            float dilation,
            float softness = 0f,
            TextStudio2DUnit units = TextStudio2DUnit.Em,
            TextStudio2DColorFaceMode colorFace = TextStudio2DColorFaceMode.PreserveOriginal)
        {
            _dilation = dilation;
            _softness = softness;
            _units = units;
            _colorFace = colorFace;
            _preservesNativeAppearance = false;
        }

        /// <summary>Returns a fill specification copy using the supplied measurement units.</summary>
        public TextStudio2DFillSpec WithUnits(TextStudio2DUnit units)
        {
            TextStudio2DFillSpec copy = this;
            copy._units = units;
            return copy;
        }

        /// <summary>Returns a fill specification copy with the supplied inline color-image handling mode.</summary>
        public TextStudio2DFillSpec WithColorFace(TextStudio2DColorFaceMode mode)
        {
            TextStudio2DFillSpec copy = this;
            copy._colorFace = mode;
            return copy;
        }
    }

    /// <summary>Settings for stroke width, alignment, joins, dashes, edge softness, and measurement units.</summary>
    [Serializable]
    public partial struct TextStudio2DStrokeSpec
    {
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Stroke width in the selected Units. Zero removes the stroke.")] float _width;
        [SerializeField, Range(-1f, 1f)] float _alignment;
        [SerializeField] TextStudio2DStrokeJoin _join;
        [SerializeField, TextStudio2DParameter(1f), Tooltip("Maximum corner-tip distance divided by the offset on that side of the outline. Corners exceeding this ratio become bevels.")] float _miterLimit;
        [SerializeField, TextStudio2DParameter(0.0001f), Tooltip("Alternating positive dash and gap lengths in the selected Units. An empty pattern draws a continuous stroke.")] float[] _dashPattern;
        [SerializeField] float _dashOffset;
        [SerializeField] TextStudio2DLineCap _dashCap;
        [SerializeField, Tooltip("Continue the pattern distance across all contours, including counters. When disabled, each contour starts at the same phase.")] bool _continueAcrossContours;
        [SerializeField, TextStudio2DParameter(TextStudio2DParameterKind.Softness), Tooltip("Soften the stroke edge using the selected Units. Zero keeps a crisp antialiased edge.")] float _softness;
        [SerializeField, Tooltip("Em follows font size. Pixels measures the output image. Exact pixel outlines support one output view, or views with matching scale and perspective.")] TextStudio2DUnit _units;

        public float Width => _width;
        /// <summary>Alignment from -1 inside through 0 centered to +1 outside.</summary>
        public float Alignment => _alignment;
        public TextStudio2DStrokeJoin Join => _join;
        public float MiterLimit => _miterLimit;
        public int DashCount => _dashPattern?.Length ?? 0;
        public float DashOffset => _dashOffset;
        public TextStudio2DLineCap DashCap => _dashCap;
        public bool ContinueAcrossContours => _continueAcrossContours;
        public float Softness => _softness;
        public TextStudio2DUnit Units => _units;

        /// <summary>Creates a stroke specification with copied dash lengths and the supplied outline, softness, unit, and contour settings.</summary>
        public TextStudio2DStrokeSpec(
            float width,
            float alignment = 0f,
            TextStudio2DStrokeJoin join = TextStudio2DStrokeJoin.Round,
            float miterLimit = 4f,
            float[] dashPattern = null,
            float dashOffset = 0f,
            float softness = 0f,
            TextStudio2DUnit units = TextStudio2DUnit.Em,
            TextStudio2DLineCap dashCap = TextStudio2DLineCap.Butt,
            bool continueAcrossContours = false)
        {
            _width = width;
            _alignment = alignment;
            _join = join;
            _miterLimit = miterLimit;
            _dashPattern = dashPattern != null ? (float[])dashPattern.Clone() : Array.Empty<float>();
            _dashOffset = dashOffset;
            _softness = softness;
            _units = units;
            _dashCap = dashCap;
            _continueAcrossContours = continueAcrossContours;
        }

        /// <summary>Returns a stroke copy with the supplied alignment from inside through centered to outside.</summary>
        public TextStudio2DStrokeSpec WithAlignment(float alignment)
        {
            TextStudio2DStrokeSpec copy = Clone();
            copy._alignment = alignment;
            return copy;
        }

        /// <summary>Returns a stroke copy with the supplied edge softness.</summary>
        public TextStudio2DStrokeSpec WithSoftness(float softness)
        {
            TextStudio2DStrokeSpec copy = Clone();
            copy._softness = softness;
            return copy;
        }

        /// <summary>Returns a stroke copy using the supplied measurement units.</summary>
        public TextStudio2DStrokeSpec WithUnits(TextStudio2DUnit units)
        {
            TextStudio2DStrokeSpec copy = Clone();
            copy._units = units;
            return copy;
        }

        internal long CaptureManagedPayload(TextStudioAppearanceManagedMemory memory) => memory.Array(_dashPattern);

        /// <summary>Returns the dash or gap length at the specified index, throwing if the index is outside the pattern array.</summary>
        public float GetDash(int index)
        {
            if (_dashPattern == null || index < 0 || index >= _dashPattern.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _dashPattern[index];
        }

        internal TextStudio2DStrokeSpec Clone()
        {
            TextStudio2DStrokeSpec clone = this;
            clone._dashPattern = _dashPattern != null
                ? (float[])_dashPattern.Clone()
                : Array.Empty<float>();
            return clone;
        }
    }

    /// <summary>Settings for shadow offset, blur, spread, rendering method, and measurement units.</summary>
    [Serializable]
    public struct TextStudio2DShadowSpec
    {
        [SerializeField] Vector2 _offset;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Blur distance in the selected Units. Zero produces a hard shadow.")] float _blur;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Expand the shadow silhouette before blurring, using the selected Units.")] float _spread;
        [SerializeField] TextStudio2DSoftEffectRendering _rendering;
        [SerializeField] TextStudio2DUnit _units;

        public Vector2 Offset => _offset;
        public float Blur => _blur;
        public float Spread => _spread;
        public TextStudio2DSoftEffectRendering Rendering => _rendering;
        public TextStudio2DUnit Units => _units;

        /// <summary>Creates a shadow specification with the supplied offset, blur, spread, and units using distance-field rendering.</summary>
        public TextStudio2DShadowSpec(
            Vector2 offset,
            float blur,
            float spread = 0f,
            TextStudio2DUnit units = TextStudio2DUnit.Em)
        {
            _offset = offset;
            _blur = blur;
            _spread = spread;
            _rendering = TextStudio2DSoftEffectRendering.DistanceField;
            _units = units;
        }

        /// <summary>Returns a shadow specification copy with the supplied rendering method.</summary>
        public TextStudio2DShadowSpec WithRendering(
            TextStudio2DSoftEffectRendering rendering)
        {
            TextStudio2DShadowSpec copy = this;
            copy._rendering = rendering;
            return copy;
        }

        /// <summary>Returns a shadow specification copy configured to blur the complete text silhouette.</summary>
        public TextStudio2DShadowSpec WithTextWideBlur()
        {
            return WithRendering(TextStudio2DSoftEffectRendering.TextWideBlur);
        }

        /// <summary>Returns a shadow specification copy using the supplied measurement units.</summary>
        public TextStudio2DShadowSpec WithUnits(TextStudio2DUnit units)
        {
            TextStudio2DShadowSpec copy = this;
            copy._units = units;
            return copy;
        }
    }

    /// <summary>Settings for glow radius, core spread, rendering method, and measurement units.</summary>
    [Serializable]
    public struct TextStudio2DGlowSpec
    {
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Glow radius in the selected Units. Larger values extend the glow farther from the silhouette.")] float _radius;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Expand the glow's solid core before its falloff, using the selected Units.")] float _spread;
        [SerializeField] TextStudio2DSoftEffectRendering _rendering;
        [SerializeField] TextStudio2DUnit _units;

        public float Radius => _radius;
        public float Spread => _spread;
        public TextStudio2DSoftEffectRendering Rendering => _rendering;
        public TextStudio2DUnit Units => _units;

        /// <summary>Creates a glow specification with the supplied radius, spread, and units using distance-field rendering.</summary>
        public TextStudio2DGlowSpec(
            float radius,
            float spread = 0f,
            TextStudio2DUnit units = TextStudio2DUnit.Em)
        {
            _radius = radius;
            _spread = spread;
            _rendering = TextStudio2DSoftEffectRendering.DistanceField;
            _units = units;
        }

        /// <summary>Returns a glow specification copy with the supplied rendering method.</summary>
        public TextStudio2DGlowSpec WithRendering(
            TextStudio2DSoftEffectRendering rendering)
        {
            TextStudio2DGlowSpec copy = this;
            copy._rendering = rendering;
            return copy;
        }

        /// <summary>Returns a glow specification copy configured to blur the complete text silhouette.</summary>
        public TextStudio2DGlowSpec WithTextWideBlur()
        {
            return WithRendering(TextStudio2DSoftEffectRendering.TextWideBlur);
        }

        /// <summary>Returns a glow specification copy using the supplied measurement units.</summary>
        public TextStudio2DGlowSpec WithUnits(TextStudio2DUnit units)
        {
            TextStudio2DGlowSpec copy = this;
            copy._units = units;
            return copy;
        }
    }

    /// <summary>Settings for beveled edge relief, lighting, highlight and shadow colors, and measurement units.</summary>
    [Serializable]
    public struct TextStudio2DBevelSpec
    {
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Width of the beveled edge in the selected Units.")] float _width;
        [SerializeField, TextStudio2DParameter(TextStudio2DParameterKind.Softness), Tooltip("Soften the bevel transition in the selected Units.")] float _softness;
        [SerializeField, Tooltip("Relief strength. Negative values reverse the bevel response.")] float _depth;
        [SerializeField, Tooltip("Light direction in degrees around the text plane. Values may wrap through multiple turns.")] float _lightAngle;
        [SerializeField, Tooltip("Light elevation in degrees above the text plane. Negative values light from below the plane.")] float _lightElevation;
        [SerializeField] Color _highlight;
        [SerializeField] Color _shadow;
        [SerializeField] TextStudio2DUnit _units;

        public float Width => _width;
        public float Softness => _softness;
        public float Depth => _depth;
        public float LightAngle => _lightAngle;
        public float LightElevation => _lightElevation;
        public Color Highlight => _highlight;
        public Color Shadow => _shadow;
        public TextStudio2DUnit Units => _units;

        /// <summary>Creates a bevel specification with the supplied edge relief, lighting, colors, and measurement units.</summary>
        public TextStudio2DBevelSpec(
            float width,
            float softness,
            float depth,
            float lightAngle,
            float lightElevation,
            Color highlight,
            Color shadow,
            TextStudio2DUnit units = TextStudio2DUnit.Em)
        {
            _width = width;
            _softness = softness;
            _depth = depth;
            _lightAngle = lightAngle;
            _lightElevation = lightElevation;
            _highlight = highlight;
            _shadow = shadow;
            _units = units;
        }

        /// <summary>Returns a bevel specification copy using the supplied measurement units.</summary>
        public TextStudio2DBevelSpec WithUnits(TextStudio2DUnit units)
        {
            TextStudio2DBevelSpec copy = this;
            copy._units = units;
            return copy;
        }
    }

    /// <summary>
    /// Compact physically inspired surface response. Presets provide finished defaults while the
    /// fluent modifiers expose the professional lighting, material, and optical controls.
    /// </summary>
    [Serializable]
    public struct TextStudio2DMaterialResponseSpec
    {
        [SerializeField] TextStudio2DMaterialResponseKind _kind;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Width of the surface response around the glyph contour.")] float _width;
        [SerializeField, TextStudio2DParameter(TextStudio2DParameterKind.Softness), Tooltip("Soften the transition into the surface response.")] float _softness;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Surface relief strength. Choose Deboss for an inward response.")] float _depth;
        [SerializeField] float _lightAngle;
        [SerializeField] float _lightElevation;
        [SerializeField, Range(0f, 1f), Tooltip("Broaden reflections from polished (0) to rough (1).")] float _roughness;
        [SerializeField, Range(0f, 1f), Tooltip("Blend from nonmetallic (0) to metallic (1) surface response.")] float _metallic;
        [SerializeField, Range(0f, 1f), Tooltip("Strength of the clearcoat highlight, from none (0) to full (1).")] float _clearcoat;
        [SerializeField, Range(-1f, 1f), Tooltip("Directional reflection response from -1 to 1. Zero is isotropic.")] float _anisotropy;
        [SerializeField, Range(0f, 1f), Tooltip("Strength of the iridescent color response, from none (0) to full (1).")] float _iridescence;
        [SerializeField, Range(0f, 1f), Tooltip("Strength of the procedural sparkle response, from none (0) to full (1).")] float _sparkle;
        [SerializeField, Range(0f, 8f), Tooltip("Emissive contribution from 0 to 8. Values above 1 preserve HDR intensity.")] float _emission;
        [SerializeField] int _seed;
        [SerializeField] Color _highlight;
        [SerializeField] Color _shadow;
        [SerializeField] Color _environment;

        public TextStudio2DMaterialResponseKind Kind => _kind;
        public float Width => _width;
        public float Softness => _softness;
        public float Depth => _depth;
        public float LightAngle => _lightAngle;
        public float LightElevation => _lightElevation;
        public float Roughness => _roughness;
        public float Metallic => _metallic;
        public float Clearcoat => _clearcoat;
        public float Anisotropy => _anisotropy;
        public float Iridescence => _iridescence;
        public float Sparkle => _sparkle;
        public float Emission => _emission;
        public int Seed => _seed;
        public Color Highlight => _highlight;
        public Color Shadow => _shadow;
        public Color Environment => _environment;

        /// <summary>Creates a material response with the supplied relief, lighting, surface, optical, color, and deterministic seed settings.</summary>
        public TextStudio2DMaterialResponseSpec(
            TextStudio2DMaterialResponseKind kind,
            float width,
            float softness,
            float depth,
            float lightAngle,
            float lightElevation,
            float roughness,
            float metallic,
            float clearcoat,
            float anisotropy,
            float iridescence,
            float sparkle,
            float emission,
            int seed,
            Color highlight,
            Color shadow,
            Color environment)
        {
            _kind = kind;
            _width = width;
            _softness = softness;
            _depth = depth;
            _lightAngle = lightAngle;
            _lightElevation = lightElevation;
            _roughness = roughness;
            _metallic = metallic;
            _clearcoat = clearcoat;
            _anisotropy = anisotropy;
            _iridescence = iridescence;
            _sparkle = sparkle;
            _emission = emission;
            _seed = seed;
            _highlight = highlight;
            _shadow = shadow;
            _environment = environment;
        }

        /// <summary>Returns polished, deterministic defaults for a professional material family.</summary>
        public static TextStudio2DMaterialResponseSpec Preset(
            TextStudio2DMaterialResponseKind kind)
        {
            switch (kind)
            {
                case TextStudio2DMaterialResponseKind.Deboss:
                    return Create(kind, 0.16f, 0.035f, 0.72f, 135f, 42f, 0.58f, 0f, 0.1f, 0f, 0f, 0f, 0f, 11,
                        new Color(0.9f, 0.93f, 1f, 0.55f), new Color(0.03f, 0.04f, 0.06f, 0.7f), new Color(0.32f, 0.38f, 0.48f, 0.18f));
                case TextStudio2DMaterialResponseKind.Satin:
                    return Create(kind, 0.38f, 0.08f, 0.55f, 120f, 36f, 0.46f, 0.15f, 0.25f, 0.82f, 0.04f, 0f, 0f, 23,
                        new Color(1f, 0.92f, 0.94f, 0.72f), new Color(0.11f, 0.025f, 0.05f, 0.48f), new Color(0.58f, 0.18f, 0.3f, 0.28f));
                case TextStudio2DMaterialResponseKind.Chrome:
                    return Create(kind, 0.5f, 0.045f, 0.8f, 128f, 48f, 0.12f, 1f, 0.72f, 0.34f, 0.08f, 0.04f, 0f, 31,
                        new Color(1f, 1f, 1f, 0.95f), new Color(0.015f, 0.025f, 0.045f, 0.9f), new Color(0.32f, 0.52f, 0.78f, 0.72f));
                case TextStudio2DMaterialResponseKind.GoldFoil:
                    return Create(kind, 0.48f, 0.055f, 0.78f, 132f, 44f, 0.2f, 1f, 0.64f, 0.42f, 0.14f, 0.18f, 0f, 47,
                        new Color(1f, 0.94f, 0.58f, 0.96f), new Color(0.15f, 0.035f, 0.006f, 0.8f), new Color(0.95f, 0.46f, 0.05f, 0.58f));
                case TextStudio2DMaterialResponseKind.Enamel:
                    return Create(kind, 0.55f, 0.08f, 0.65f, 135f, 52f, 0.2f, 0.05f, 0.92f, 0f, 0.04f, 0f, 0f, 59,
                        new Color(1f, 1f, 1f, 0.88f), new Color(0.02f, 0.025f, 0.035f, 0.42f), new Color(0.24f, 0.42f, 0.7f, 0.24f));
                case TextStudio2DMaterialResponseKind.BubbleGel:
                    return Create(kind, 0.72f, 0.12f, 0.9f, 125f, 58f, 0.1f, 0f, 0.95f, 0f, 0.18f, 0.02f, 0f, 67,
                        new Color(1f, 1f, 1f, 0.94f), new Color(0.04f, 0.08f, 0.16f, 0.34f), new Color(0.25f, 0.7f, 1f, 0.34f));
                case TextStudio2DMaterialResponseKind.Neon:
                    return Create(kind, 0.18f, 0.08f, 0.48f, 135f, 38f, 0.34f, 0f, 0.12f, 0f, 0f, 0f, 2.2f, 73,
                        new Color(1f, 1f, 1f, 0.95f), new Color(0.01f, 0.015f, 0.025f, 0.28f), new Color(0.1f, 0.8f, 1f, 0.85f));
                case TextStudio2DMaterialResponseKind.Ice:
                    return Create(kind, 0.62f, 0.07f, 0.82f, 128f, 50f, 0.16f, 0.08f, 0.78f, 0.08f, 0.22f, 0.12f, 0.04f, 83,
                        new Color(1f, 1f, 1f, 0.96f), new Color(0.025f, 0.12f, 0.22f, 0.48f), new Color(0.3f, 0.82f, 1f, 0.52f));
                case TextStudio2DMaterialResponseKind.Glass:
                    return Create(kind, 0.7f, 0.08f, 0.86f, 132f, 55f, 0.08f, 0f, 1f, 0f, 0.12f, 0f, 0f, 97,
                        new Color(1f, 1f, 1f, 0.92f), new Color(0.02f, 0.06f, 0.1f, 0.28f), new Color(0.25f, 0.72f, 0.95f, 0.26f));
                case TextStudio2DMaterialResponseKind.HolographicFoil:
                    return Create(kind, 0.54f, 0.06f, 0.76f, 125f, 46f, 0.18f, 0.82f, 0.62f, 0.3f, 1f, 0.42f, 0.03f, 101,
                        new Color(1f, 1f, 1f, 0.9f), new Color(0.04f, 0.025f, 0.12f, 0.5f), new Color(0.45f, 0.8f, 1f, 0.72f));
                default:
                    return Create(kind, 0.16f, 0.035f, 0.72f, 135f, 42f, 0.5f, 0f, 0.15f, 0f, 0f, 0f, 0f, 7,
                        new Color(1f, 1f, 1f, 0.72f), new Color(0.025f, 0.03f, 0.045f, 0.64f), new Color(0.4f, 0.48f, 0.62f, 0.2f));
            }
        }

        /// <summary>Returns a material response copy with the supplied relief width, softness, and depth.</summary>
        public TextStudio2DMaterialResponseSpec WithRelief(
            float width,
            float softness,
            float depth)
        {
            TextStudio2DMaterialResponseSpec value = this;
            value._width = width;
            value._softness = softness;
            value._depth = depth;
            return value;
        }

        /// <summary>Returns a material response copy with the supplied light angle and elevation.</summary>
        public TextStudio2DMaterialResponseSpec WithLighting(float angle, float elevation)
        {
            TextStudio2DMaterialResponseSpec value = this;
            value._lightAngle = angle;
            value._lightElevation = elevation;
            return value;
        }

        /// <summary>Returns a material response copy with the supplied roughness, metallic, clearcoat, and anisotropy settings.</summary>
        public TextStudio2DMaterialResponseSpec WithSurface(
            float roughness,
            float metallic,
            float clearcoat,
            float anisotropy = 0f)
        {
            TextStudio2DMaterialResponseSpec value = this;
            value._roughness = roughness;
            value._metallic = metallic;
            value._clearcoat = clearcoat;
            value._anisotropy = anisotropy;
            return value;
        }

        /// <summary>Returns a material response copy with the supplied iridescence, sparkle, emission, and deterministic seed settings.</summary>
        public TextStudio2DMaterialResponseSpec WithOptics(
            float iridescence,
            float sparkle,
            float emission,
            int seed)
        {
            TextStudio2DMaterialResponseSpec value = this;
            value._iridescence = iridescence;
            value._sparkle = sparkle;
            value._emission = emission;
            value._seed = seed;
            return value;
        }

        /// <summary>Returns a material response copy with the supplied highlight, shadow, and environment colors.</summary>
        public TextStudio2DMaterialResponseSpec WithColors(
            Color highlight,
            Color shadow,
            Color environment)
        {
            TextStudio2DMaterialResponseSpec value = this;
            value._highlight = highlight;
            value._shadow = shadow;
            value._environment = environment;
            return value;
        }

        static TextStudio2DMaterialResponseSpec Create(
            TextStudio2DMaterialResponseKind kind,
            float width,
            float softness,
            float depth,
            float lightAngle,
            float lightElevation,
            float roughness,
            float metallic,
            float clearcoat,
            float anisotropy,
            float iridescence,
            float sparkle,
            float emission,
            int seed,
            Color highlight,
            Color shadow,
            Color environment)
        {
            return new TextStudio2DMaterialResponseSpec(
                kind,
                width,
                softness,
                depth,
                lightAngle,
                lightElevation,
                roughness,
                metallic,
                clearcoat,
                anisotropy,
                iridescence,
                sparkle,
                emission,
                seed,
                highlight,
                shadow,
                environment);
        }
    }

    /// <summary>Settings for silhouette extrusion direction, taper, falloff, edge softness, paint mapping, and contour lighting.</summary>
    [Serializable]
    public struct TextStudio2DFauxDepthSpec
    {
        [SerializeField] Vector2 _offset;
        [SerializeField, Range(-0.95f, 0.95f), Tooltip("Change silhouette scale per unit of depth. Negative values widen it; positive values narrow it. The change shrinks with Depth Length and vanishes at zero.")] float _taper;
        [SerializeField, Range(0f, 1f), Tooltip("Coverage loss from the face to the depth endpoint: 0 keeps solid depth; 1 fully fades the far end.")] float _falloff;
        [SerializeField, TextStudio2DParameter(TextStudio2DParameterKind.Softness), Tooltip("Soften the extrusion silhouette using the selected Units without lowering its solid interior opacity.")] float _softness;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Expand the source contour in the selected Units so depth can include a face stroke's silhouette.")] float _silhouetteOutset;
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of the depth reserved for terminal fading. Zero disables the fade; 1 fades along the entire depth.")] float _endFade;
        [SerializeField] TextStudio2DFauxDepthPaintMapping _paintMapping;
        [SerializeField] float _sideLightAngle;
        [SerializeField, Range(0f, 1f), Tooltip("Strength of directional side-wall lighting, from none (0) to full (1).")] float _sideLightStrength;
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of the depth occupied by the near-face highlight, from 0 to 1.")] float _frontBevelWidth;
        [SerializeField, Range(0f, 1f), Tooltip("Strength of the near-face highlight, from none (0) to full (1).")] float _frontBevelStrength;
        [SerializeField, Range(0f, 1f), Tooltip("Darken the terminal depth from unchanged (0) to fully shaded (1), before End Fade.")] float _farCapShade;
        [SerializeField] TextStudio2DUnit _units;

        public Vector2 Offset => _offset;
        public float Taper => _taper;
        public float Falloff => _falloff;
        public float Softness => _softness;
        public float SilhouetteOutset => _silhouetteOutset;
        public float EndFade => _endFade;
        public TextStudio2DFauxDepthPaintMapping PaintMapping => _paintMapping;
        public float SideLightAngle => _sideLightAngle;
        public float SideLightStrength => _sideLightStrength;
        public float FrontBevelWidth => _frontBevelWidth;
        public float FrontBevelStrength => _frontBevelStrength;
        public float FarCapShade => _farCapShade;
        public TextStudio2DUnit Units => _units;

        internal bool HasDepth => _offset.x != 0f || _offset.y != 0f;

        // Pixel depths resolve their length into effect units before calling this.
        internal float ResolveTaper(float length) => Mathf.Min(0.95f, _taper * length);

        /// <summary>Creates a faux-depth specification with the supplied extrusion offset, taper, falloff, softness, and measurement units.</summary>
        public TextStudio2DFauxDepthSpec(
            Vector2 offset,
            float taper = 0f,
            float falloff = 0f,
            float softness = 0f,
            TextStudio2DUnit units = TextStudio2DUnit.Em)
        {
            _offset = offset;
            _taper = taper;
            _falloff = falloff;
            _softness = softness;
            _silhouetteOutset = 0f;
            _endFade = 0f;
            _paintMapping = TextStudio2DFauxDepthPaintMapping.Surface;
            _sideLightAngle = 135f;
            _sideLightStrength = 0f;
            _frontBevelWidth = 0f;
            _frontBevelStrength = 0f;
            _farCapShade = 0f;
            _units = units;
        }

        /// <summary>Expands the source contour before tracing depth so an extrusion can carry an outer stroke silhouette.</summary>
        public TextStudio2DFauxDepthSpec WithSilhouetteOutset(float silhouetteOutset)
        {
            TextStudio2DFauxDepthSpec value = this;
            value._silhouetteOutset = silhouetteOutset;
            return value;
        }

        /// <summary>Fades the terminal portion of the extrusion while preserving its direction.</summary>
        public TextStudio2DFauxDepthSpec WithEndFade(float endFade)
        {
            TextStudio2DFauxDepthSpec value = this;
            value._endFade = endFade;
            return value;
        }

        /// <summary>Maps the layer paint either through its normal surface space or from the face toward the depth endpoint.</summary>
        public TextStudio2DFauxDepthSpec WithPaintMapping(
            TextStudio2DFauxDepthPaintMapping paintMapping)
        {
            TextStudio2DFauxDepthSpec value = this;
            value._paintMapping = paintMapping;
            return value;
        }

        /// <summary>Adds contour lighting plus restrained near-face and far-cap depth responses.</summary>
        public TextStudio2DFauxDepthSpec WithLighting(
            float sideLightAngle,
            float sideLightStrength,
            float frontBevelWidth = 0f,
            float frontBevelStrength = 0f,
            float farCapShade = 0f)
        {
            TextStudio2DFauxDepthSpec value = this;
            value._sideLightAngle = sideLightAngle;
            value._sideLightStrength = sideLightStrength;
            value._frontBevelWidth = frontBevelWidth;
            value._frontBevelStrength = frontBevelStrength;
            value._farCapShade = farCapShade;
            return value;
        }

        /// <summary>Returns a faux-depth specification copy using the supplied measurement units.</summary>
        public TextStudio2DFauxDepthSpec WithUnits(TextStudio2DUnit units)
        {
            TextStudio2DFauxDepthSpec value = this;
            value._units = units;
            return value;
        }
    }

    public enum TextStudio2DBackplateVisibility
    {
        FollowTypewriter = 0,
        AlwaysVisible = 1
    }

    /// <summary>Settings for decoration geometry, topology, reveal visibility, line patterns, and ordered surface treatments.</summary>
    [Serializable]
    public struct TextStudio2DDecorationSpec
    {
        [SerializeField] TextStudio2DDecorationKind _kind;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Thickness of the line or border decoration. Zero removes its thickness.")] float _thickness;
        [SerializeField] float _offset;
        [SerializeField] Vector4 _padding;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Default corner radius. Zero keeps sharp corners; the shape limits rounding to its available size.")] float _cornerRadius;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Explicit plate height. Zero sizes the plate automatically from the text.")] float _plateHeight;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Explicit plate width in em units before padding. Zero sizes the plate automatically from the text.")] float _plateWidth;
        [SerializeField] TextStudio2DDecorationVerticalAnchor _verticalAnchor;
        [SerializeField] TextStudio2DDecorationEndStyle _leftEnd;
        [SerializeField] TextStudio2DDecorationEndStyle _rightEnd;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("End-cap size. Zero uses the shape's automatic cap size.")] float _capSize;
        [SerializeField] TextStudio2DDecorationShape _shape;
        [SerializeField, TextStudio2DParameter(TextStudio2DParameterKind.CornerRadii)] Vector4 _cornerRadii;
        [SerializeField, Range(2f, 16f), Tooltip("Squircle exponent from an ellipse (2) toward a rounded rectangle (16).")] float _squirclePower;
        [SerializeField, Range(3, 8), Tooltip("Number of sides for a regular polygon, from 3 to 8.")] int _polygonSides;
        [SerializeField, Range(0.05f, 0.95f), Tooltip("Inner star radius relative to its outer radius, from 0.05 to 0.95.")] float _starInset;
        [SerializeField] Vector2[] _customPoints;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Expand the union of selected letter shapes, in em units.")] float _glyphPadding;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Close gaps narrower than twice this radius before applying padding. Zero preserves separated shapes.")] float _glyphBridgeRadius;
        [SerializeField, Tooltip("Fill enclosed counters and gaps in the backing without changing the lettering.")] bool _glyphFillHoles;
        [SerializeField] TextStudio2DDecorationTopology _topology;
        [SerializeField, Tooltip("Connect wrapped fragments across explicitly authored line breaks. Separate logical matches and non-overlapping columns remain separate.")] bool _connectAcrossLineBreaks;
        [SerializeField] TextStudio2DSurfaceTreatment[] _surfaceTreatments;
        [SerializeField] TextStudio2DDecorationLineSpec _line;

        [SerializeField, Tooltip("Follow Typewriter builds the backing from currently visible letters, including grouped sources. Always Visible keeps the complete backing while letters reveal or hide.")] TextStudio2DBackplateVisibility _backplateVisibility;

        public TextStudio2DBackplateVisibility BackplateVisibility => _backplateVisibility;
        public float GlyphPadding => _glyphPadding;
        public float GlyphBridgeRadius => _glyphBridgeRadius;
        public bool GlyphFillHoles => _glyphFillHoles;
        public TextStudio2DDecorationKind Kind => _kind;
        public float Thickness => _thickness;
        public float Offset => _offset;
        public Vector4 Padding => _padding;
        public float CornerRadius => _cornerRadius;
        public float PlateHeight => _plateHeight;
        public float PlateWidth => _plateWidth;
        public TextStudio2DDecorationVerticalAnchor VerticalAnchor => _verticalAnchor;
        public TextStudio2DDecorationEndStyle LeftEnd => _leftEnd;
        public TextStudio2DDecorationEndStyle RightEnd => _rightEnd;
        public float CapSize => _capSize;
        public TextStudio2DDecorationShape Shape => _shape;
        public Vector4 CornerRadii => _cornerRadii;
        public float SquirclePower => _squirclePower;
        public int PolygonSides => _polygonSides;
        public float StarInset => _starInset;
        internal long CaptureManagedPayload(TextStudioAppearanceManagedMemory memory)
        {
            long bytes = memory.Array(_customPoints) + memory.Array(_surfaceTreatments);
            if (_surfaceTreatments != null)
                foreach (TextStudio2DSurfaceTreatment treatment in _surfaceTreatments)
                    bytes += treatment.Stroke.CaptureManagedPayload(memory) + treatment.Paint.CaptureManagedPayload(memory);
            return bytes;
        }

        public int CustomPointCount => _customPoints?.Length ?? 0;
        public TextStudio2DDecorationTopology Topology => _topology;
        public bool ConnectAcrossLineBreaks => _connectAcrossLineBreaks;
        public int TreatmentCount => _surfaceTreatments?.Length ?? 0;
        public TextStudio2DDecorationLineSpec Line => _line;

        /// <summary>Creates a decoration specification with the supplied kind, thickness, offset, padding, and corner radius.</summary>
        public TextStudio2DDecorationSpec(
            TextStudio2DDecorationKind kind,
            float thickness,
            float offset = 0f,
            Vector4 padding = default,
            float cornerRadius = 0f)
        {
            _backplateVisibility = TextStudio2DBackplateVisibility.FollowTypewriter;
            _glyphPadding = 0f;
            _glyphBridgeRadius = 0f;
            _glyphFillHoles = false;
            _kind = kind;
            _thickness = thickness;
            _offset = offset;
            _padding = padding;
            _cornerRadius = cornerRadius;
            _plateHeight = 0f;
            _plateWidth = 0f;
            _verticalAnchor = TextStudio2DDecorationVerticalAnchor.GlyphBounds;
            _leftEnd = TextStudio2DDecorationEndStyle.RoundedCorners;
            _rightEnd = TextStudio2DDecorationEndStyle.RoundedCorners;
            _capSize = 0f;
            _shape = TextStudio2DDecorationShape.Plate;
            _cornerRadii = Vector4.zero;
            _squirclePower = 4f;
            _polygonSides = 6;
            _starInset = .5f;
            _customPoints = Array.Empty<Vector2>();
            _topology = TextStudio2DDecorationTopology.PerLine;
            _connectAcrossLineBreaks = false;
            _surfaceTreatments = Array.Empty<TextStudio2DSurfaceTreatment>();
            _line = TextStudio2DDecorationLineSpec.Default;
        }

        /// <summary>Configures explicit plate height, anchor, end geometry, and cap size.</summary>
        public TextStudio2DDecorationSpec WithPlateShape(
            float plateHeight,
            TextStudio2DDecorationVerticalAnchor verticalAnchor,
            TextStudio2DDecorationEndStyle leftEnd,
            TextStudio2DDecorationEndStyle rightEnd,
            float capSize = 0f)
        {
            TextStudio2DDecorationSpec value = this;
            value._plateHeight = plateHeight;
            value._verticalAnchor = verticalAnchor;
            value._leftEnd = leftEnd;
            value._rightEnd = rightEnd;
            value._capSize = capSize;
            return value;
        }

        /// <summary>Returns a decoration specification copy with the supplied explicit plate width.</summary>
        public TextStudio2DDecorationSpec WithPlateWidth(float plateWidth)
        {
            TextStudio2DDecorationSpec value = this;
            value._plateWidth = plateWidth;
            return value;
        }

        /// <summary>Returns a decoration copy with the supplied shape, corner radii, squircle power, polygon sides, and star inset.</summary>
        public TextStudio2DDecorationSpec WithShape(
            TextStudio2DDecorationShape shape,
            Vector4 cornerRadii = default,
            float squirclePower = 4f,
            int polygonSides = 6,
            float starInset = .5f)
        {
            TextStudio2DDecorationSpec value = Clone();
            value._shape = shape;
            value._cornerRadii = cornerRadii;
            value._squirclePower = squirclePower;
            value._polygonSides = polygonSides;
            value._starInset = starInset;
            return value;
        }

        /// <summary>Returns a decoration copy configured as a custom polygon with independently copied points.</summary>
        public TextStudio2DDecorationSpec WithCustomPolygon(Vector2[] points)
        {
            TextStudio2DDecorationSpec value = Clone();
            value._shape = TextStudio2DDecorationShape.CustomPolygon;
            value._customPoints = points != null
                ? (Vector2[])points.Clone()
                : Array.Empty<Vector2>();
            return value;
        }

        /// <summary>Derives one retained backing from selected, posed font contours. Treatments apply to its union boundary.</summary>
        public TextStudio2DDecorationSpec WithGlyphSilhouette(float padding, float bridgeRadius = 0f, bool fillHoles = true)
        {
            TextStudio2DDecorationSpec value = Clone();
            value._shape = TextStudio2DDecorationShape.GlyphSilhouette;
            value._glyphPadding = padding;
            value._glyphBridgeRadius = bridgeRadius;
            value._glyphFillHoles = fillHoles;
            value._topology = TextStudio2DDecorationTopology.ConnectedRange;
            return value;
        }

        /// <summary>Returns a decoration specification copy with the supplied visibility behavior during text reveal and hiding.</summary>
        public TextStudio2DDecorationSpec WithBackplateVisibility(TextStudio2DBackplateVisibility visibility)
        {
            TextStudio2DDecorationSpec value = this;
            value._backplateVisibility = visibility;
            return value;
        }

        /// <summary>Returns a decoration copy with the supplied topology and connection behavior across line breaks.</summary>
        public TextStudio2DDecorationSpec WithTopology(
            TextStudio2DDecorationTopology topology, bool acrossLineBreaks = false)
        {
            TextStudio2DDecorationSpec value = Clone();
            value._topology = topology;
            value._connectAcrossLineBreaks = acrossLineBreaks;
            return value;
        }

        /// <summary>Replaces the ordered surface treatments. Index zero is visually topmost.</summary>
        public TextStudio2DDecorationSpec WithTreatments(params TextStudio2DSurfaceTreatment[] treatments)
        {
            TextStudio2DDecorationSpec value = Clone();
            value._surfaceTreatments = CloneTreatments(treatments);
            return value;
        }

        /// <summary>Returns an independent copy of the surface treatment at the specified index, throwing if the index is invalid.</summary>
        public TextStudio2DSurfaceTreatment GetTreatment(int index)
        {
            if (index < 0 || index >= TreatmentCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _surfaceTreatments[index].Clone();
        }

        internal TextStudio2DSurfaceTreatment GetTreatmentUnsafe(int index) => _surfaceTreatments[index];

        internal Vector4 GetTreatmentExpansion(TextStudio2DUnit units)
        {
            Vector4 expansion = Vector4.zero;
            for (int index = 0; index < TreatmentCount; index++)
                expansion = Vector4.Max(expansion, _surfaceTreatments[index].GetExpansion(units));
            return expansion;
        }

        /// <summary>Returns a decoration copy with the supplied line-pattern and font-metric settings.</summary>
        public TextStudio2DDecorationSpec WithLine(TextStudio2DDecorationLineSpec line)
        {
            TextStudio2DDecorationSpec value = Clone();
            value._line = line;
            return value;
        }

        /// <summary>Returns the custom polygon point at the specified index, throwing if the index is outside the point array.</summary>
        public Vector2 GetCustomPoint(int index)
        {
            if (_customPoints == null || index < 0 || index >= _customPoints.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _customPoints[index];
        }

        internal TextStudio2DDecorationSpec Clone()
        {
            TextStudio2DDecorationSpec copy = this;
            copy._customPoints = _customPoints != null
                ? (Vector2[])_customPoints.Clone()
                : Array.Empty<Vector2>();
            copy._surfaceTreatments = CloneTreatments(_surfaceTreatments);
            return copy;
        }

        internal void RepairTreatmentIdentities(HashSet<TextStudioStackItemId> identities)
        {
            for (int index = 0; index < TreatmentCount; index++)
            {
                TextStudio2DSurfaceTreatment treatment = _surfaceTreatments[index];
                TextStudioStackItemId identity = treatment.ItemId;
                if (identity.IsValid && identities.Add(identity)) continue;
                do { identity = TextStudioStackItemId.CreateUnique("surface"); }
                while (!identities.Add(identity));
                _surfaceTreatments[index] = treatment.WithIdentity(identity);
            }
        }

        static TextStudio2DSurfaceTreatment[] CloneTreatments(TextStudio2DSurfaceTreatment[] treatments)
        {
            if (treatments == null || treatments.Length == 0) return Array.Empty<TextStudio2DSurfaceTreatment>();
            TextStudio2DSurfaceTreatment[] result = new TextStudio2DSurfaceTreatment[treatments.Length];
            for (int index = 0; index < result.Length; index++) result[index] = treatments[index].Clone();
            return result;
        }
    }

    /// <summary>A linear-light straight RGBA transform, separate from geometric coverage.</summary>
    [Serializable]
    public partial struct TextStudio2DColorAdjustmentSpec
    {
        [SerializeField] Vector4 _red;
        [SerializeField] Vector4 _green;
        [SerializeField] Vector4 _blue;
        [SerializeField] Vector4 _alpha;
        [SerializeField] Vector4 _offset;
        [SerializeField, Range(0f, 1f)] float _strength;
        [SerializeField] bool _clampOutput;

        public Vector4 Red => _mode == TextStudio2DColorAdjustmentMode.Matrix ? _red : Resolve()._red;
        public Vector4 Green => _mode == TextStudio2DColorAdjustmentMode.Matrix ? _green : Resolve()._green;
        public Vector4 Blue => _mode == TextStudio2DColorAdjustmentMode.Matrix ? _blue : Resolve()._blue;
        public Vector4 Alpha => _mode == TextStudio2DColorAdjustmentMode.Matrix ? _alpha : Resolve()._alpha;
        public Vector4 Offset => _mode == TextStudio2DColorAdjustmentMode.Matrix ? _offset : Resolve()._offset;
        public float Strength => _strength;
        public bool ClampOutput => _clampOutput;

        /// <summary>Creates a straight RGBA matrix transform with the supplied channel rows, offset, strength, and output clamping behavior.</summary>
        public TextStudio2DColorAdjustmentSpec(
            Vector4 red,
            Vector4 green,
            Vector4 blue,
            Vector4 alpha,
            Vector4 offset = default,
            float strength = 1f,
            bool clampOutput = false) : this()
        {
            _red = red;
            _green = green;
            _blue = blue;
            _alpha = alpha;
            _offset = offset;
            _strength = strength;
            _clampOutput = clampOutput;
        }

        public static TextStudio2DColorAdjustmentSpec Identity =>
            new TextStudio2DColorAdjustmentSpec(
                new Vector4(1f, 0f, 0f, 0f),
                new Vector4(0f, 1f, 0f, 0f),
                new Vector4(0f, 0f, 1f, 0f),
                new Vector4(0f, 0f, 0f, 1f));

        /// <summary>Creates the selected color-adjustment preset with the supplied strength.</summary>
        public static TextStudio2DColorAdjustmentSpec Preset(
            TextStudio2DColorAdjustmentPreset preset,
            float strength = 1f)
        {
            TextStudio2DColorAdjustmentSpec value = preset switch
            {
                TextStudio2DColorAdjustmentPreset.Grayscale =>
                    Luminance(new Vector3(.2126f, .7152f, .0722f)),
                TextStudio2DColorAdjustmentPreset.Sepia =>
                    new TextStudio2DColorAdjustmentSpec(
                        new Vector4(.393f, .769f, .189f, 0f),
                        new Vector4(.349f, .686f, .168f, 0f),
                        new Vector4(.272f, .534f, .131f, 0f),
                        new Vector4(0f, 0f, 0f, 1f)),
                TextStudio2DColorAdjustmentPreset.Invert =>
                    new TextStudio2DColorAdjustmentSpec(
                        new Vector4(-1f, 0f, 0f, 0f),
                        new Vector4(0f, -1f, 0f, 0f),
                        new Vector4(0f, 0f, -1f, 0f),
                        new Vector4(0f, 0f, 0f, 1f),
                        new Vector4(1f, 1f, 1f, 0f)),
                TextStudio2DColorAdjustmentPreset.HighContrast =>
                    new TextStudio2DColorAdjustmentSpec(
                        new Vector4(1.35f, 0f, 0f, 0f),
                        new Vector4(0f, 1.35f, 0f, 0f),
                        new Vector4(0f, 0f, 1.35f, 0f),
                        new Vector4(0f, 0f, 0f, 1f),
                        new Vector4(-.175f, -.175f, -.175f, 0f)),
                TextStudio2DColorAdjustmentPreset.Desaturate =>
                    Luminance(new Vector3(.299f, .587f, .114f)),
                _ => Identity
            };
            value._strength = strength;
            return value;
        }

        /// <summary>Returns a color-adjustment specification copy with the supplied transform strength.</summary>
        public TextStudio2DColorAdjustmentSpec WithStrength(float strength)
        {
            TextStudio2DColorAdjustmentSpec value = this;
            value._strength = strength;
            return value;
        }

        static TextStudio2DColorAdjustmentSpec Luminance(Vector3 weights)
        {
            Vector4 row = new Vector4(weights.x, weights.y, weights.z, 0f);
            return new TextStudio2DColorAdjustmentSpec(
                row,
                row,
                row,
                new Vector4(0f, 0f, 0f, 1f));
        }
    }

    /// <summary>Settings that select a source stack item as a mask with optional inversion and edge feathering.</summary>
    [Serializable]
    public struct TextStudio2DMaskSpec
    {
        [SerializeField] TextStudioStackItemId _sourceItemId;
        [SerializeField] bool _invert;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Soften the mask boundary. Zero keeps the original edge.")] float _feather;

        public TextStudioStackItemId SourceItemId => _sourceItemId;
        public bool Invert => _invert;
        public float Feather => _feather;

        /// <summary>Creates a mask specification referencing the supplied source item with optional inversion and feathering.</summary>
        public TextStudio2DMaskSpec(
            TextStudioStackItemId sourceItemId,
            bool invert = false,
            float feather = 0f)
        {
            _sourceItemId = sourceItemId;
            _invert = invert;
            _feather = feather;
        }
    }

    /// <summary>One persistent, independently addressable row in a 2D appearance stack.</summary>
    [Serializable]
    public partial struct TextStudio2DLayer : ITextStudioStackItem
    {
        [SerializeField] TextStudioStackItemId _itemId;
        [SerializeField] string _displayName;
        [SerializeField] TextStudio2DLayerKind _kind;
        [SerializeField] bool _enabled;
        [SerializeField] bool _solo;
        [SerializeField] bool _participatesInAnimatedTint;
        [SerializeField, Range(0f, 1f), Tooltip("Overall layer opacity from transparent (0) to fully opaque (1), before blending.")] float _opacity;
        [SerializeField] TextStudio2DBlendMode _blendMode;
        [SerializeField] TextStudio2DExecutionPath _executionPath;
        [SerializeField] TextStudio2DQualityTier _qualityTier;
        [SerializeField] TextStudio2DGroupOrder _groupOrder;
        [SerializeField] TextStudio2DSharedSurfaceOrder _sharedSurfaceOrder;
        [SerializeField] TextStudio2DSourceApplicability _sourceApplicability;
        [SerializeField] TextStudioStackItemId _parentId;
        [SerializeField] TextStudioStackItemId _maskId;
        [SerializeField] TextStudioApplicationSelector _selector;
        [SerializeField] TextStudio2DPaint _paint;
        [SerializeField] TextStudio2DFillSpec _fill;
        [SerializeField] TextStudio2DStrokeSpec _stroke;
        [SerializeField] TextStudio2DShadowSpec _shadow;
        [SerializeField] TextStudio2DGlowSpec _glow;
        [SerializeField] TextStudio2DBevelSpec _bevel;
        [SerializeField] TextStudio2DFauxDepthSpec _fauxDepth;
        [SerializeField] TextStudio2DDecorationSpec _decoration;
        [SerializeField] TextStudio2DMaskSpec _mask;
        [SerializeField] TextStudio2DMaterialResponseSpec _materialResponse;
        [SerializeField] TextStudio2DGraphicEffectSpec _graphicEffect;
        [SerializeField] TextStudio2DAnimatedEffectSpec _animatedEffect;
        [SerializeField] TextStudio2DColorAdjustmentSpec _colorAdjustment;
        [SerializeField] TextStudioAppearanceActivation _appearanceActivation;
        [NonSerialized] TextStudioStackItemId _surfaceOwnerId;

        public TextStudioStackItemId ItemId => _itemId;
        public string DisplayName => _displayName ?? string.Empty;
        public TextStudio2DLayerKind Kind => _kind;
        public bool IsEnabled => _enabled;
        public bool IsSolo => _solo;
        public bool ParticipatesInAnimatedTint => _participatesInAnimatedTint;
        public float Opacity => _opacity;
        public TextStudio2DBlendMode BlendMode => _blendMode;
        public TextStudio2DExecutionPath ExecutionPath => _executionPath;
        public TextStudio2DQualityTier QualityTier => _qualityTier;
        public TextStudio2DGroupOrder GroupOrder => _groupOrder;
        public TextStudio2DSharedSurfaceOrder SharedSurfaceOrder => _sharedSurfaceOrder;
        public TextStudio2DSourceApplicability SourceApplicability => _sourceApplicability;
        public TextStudioStackItemId ParentId => _parentId;
        public TextStudioStackItemId MaskId => _maskId;
        public TextStudioApplicationSelector Selector => _selector;
        public TextStudio2DPaint Paint => _paint;
        public TextStudio2DFillSpec Fill => _fill;
        public TextStudio2DStrokeSpec Stroke => _stroke;
        public TextStudio2DShadowSpec Shadow => _shadow;
        public TextStudio2DGlowSpec Glow => _glow;
        public TextStudio2DBevelSpec Bevel => _bevel;
        public TextStudio2DFauxDepthSpec FauxDepth => _fauxDepth;
        public TextStudio2DDecorationSpec Decoration => _decoration;
        public TextStudio2DMaskSpec Mask => _mask;
        public TextStudio2DMaterialResponseSpec MaterialResponse => _materialResponse;
        public TextStudio2DGraphicEffectSpec GraphicEffect => _graphicEffect;
        public TextStudio2DAnimatedEffectSpec AnimatedEffect => _animatedEffect;
        public TextStudio2DColorAdjustmentSpec ColorAdjustment => _colorAdjustment;
        public TextStudioAppearanceActivation AppearanceActivation => _appearanceActivation.Clone();
        internal bool IsTagDefinition => _appearanceActivation.Mode == TextStudioAppearanceActivationMode.Tagged;
        internal TextStudioStackItemId SurfaceOwnerId => _surfaceOwnerId;

        /// <summary>Creates a named fill layer with an independent paint copy and default fill settings.</summary>
        public static TextStudio2DLayer CreateFill(string displayName, TextStudio2DPaint paint)
        {
            return CreateFill(displayName, paint, default);
        }

        /// <summary>Creates a named fill layer with an independent paint copy and the supplied fill specification.</summary>
        public static TextStudio2DLayer CreateFill(
            string displayName,
            TextStudio2DPaint paint,
            TextStudio2DFillSpec fill)
        {
            TextStudio2DLayer layer = Defaults(TextStudio2DLayerKind.Fill, displayName);
            layer._paint = paint.Clone();
            layer._fill = fill;
            return layer;
        }

        /// <summary>Creates a named stroke layer with independent paint and stroke-pattern copies.</summary>
        public static TextStudio2DLayer CreateStroke(
            string displayName,
            TextStudio2DPaint paint,
            TextStudio2DStrokeSpec stroke)
        {
            TextStudio2DLayer layer = Defaults(TextStudio2DLayerKind.Stroke, displayName);
            layer._paint = paint.Clone();
            layer._stroke = stroke.Clone();
            return layer;
        }

        /// <summary>Creates a named inner or outer shadow layer with an independent paint copy and the supplied shadow settings.</summary>
        public static TextStudio2DLayer CreateShadow(
            string displayName,
            bool inner,
            TextStudio2DPaint paint,
            TextStudio2DShadowSpec shadow)
        {
            TextStudio2DLayer layer = Defaults(
                inner ? TextStudio2DLayerKind.InnerShadow : TextStudio2DLayerKind.OuterShadow,
                displayName);
            layer._paint = paint.Clone();
            layer._shadow = shadow;
            return layer;
        }

        /// <summary>Creates a named inner or outer glow layer with an independent paint copy and the supplied glow settings.</summary>
        public static TextStudio2DLayer CreateGlow(
            string displayName,
            bool inner,
            TextStudio2DPaint paint,
            TextStudio2DGlowSpec glow)
        {
            TextStudio2DLayer layer = Defaults(
                inner ? TextStudio2DLayerKind.InnerGlow : TextStudio2DLayerKind.OuterGlow,
                displayName);
            layer._paint = paint.Clone();
            layer._glow = glow;
            return layer;
        }

        /// <summary>Creates a named bevel layer with the supplied relief and lighting settings.</summary>
        public static TextStudio2DLayer CreateBevel(string displayName, TextStudio2DBevelSpec bevel)
        {
            TextStudio2DLayer layer = Defaults(TextStudio2DLayerKind.Bevel, displayName);
            layer._bevel = bevel;
            return layer;
        }

        /// <summary>Creates a named faux-depth layer with an independent paint copy and the supplied extrusion settings.</summary>
        public static TextStudio2DLayer CreateFauxDepth(
            string displayName,
            TextStudio2DPaint paint,
            TextStudio2DFauxDepthSpec fauxDepth)
        {
            TextStudio2DLayer layer = Defaults(TextStudio2DLayerKind.FauxDepth, displayName);
            layer._paint = paint.Clone();
            layer._fauxDepth = fauxDepth;
            return layer;
        }

        /// <summary>Creates a named decoration layer whose surface treatments are replaced by a fill using the supplied paint.</summary>
        public static TextStudio2DLayer CreateDecoration(
            string displayName,
            TextStudio2DPaint paint,
            TextStudio2DDecorationSpec decoration)
        {
            return CreateDecoration(displayName, decoration.WithTreatments(TextStudio2DSurfaceTreatment.CreateFill("Fill", paint)));
        }

        /// <summary>Creates a named decoration layer with an independently copied decoration specification.</summary>
        public static TextStudio2DLayer CreateDecoration(string displayName, TextStudio2DDecorationSpec decoration)
        {
            TextStudio2DLayer layer = Defaults(TextStudio2DLayerKind.Decoration, displayName);
            layer._decoration = decoration.Clone();
            return layer;
        }

        /// <summary>Creates a named mask layer with the supplied source, inversion, and feathering settings.</summary>
        public static TextStudio2DLayer CreateMask(string displayName, TextStudio2DMaskSpec mask)
        {
            TextStudio2DLayer layer = Defaults(TextStudio2DLayerKind.Mask, displayName);
            layer._mask = mask;
            return layer;
        }

        /// <summary>Creates a named group layer for organizing child appearance layers.</summary>
        public static TextStudio2DLayer CreateGroup(string displayName)
        {
            return Defaults(TextStudio2DLayerKind.Group, displayName);
        }

        /// <summary>Creates a named material-response layer with the supplied surface and lighting settings.</summary>
        public static TextStudio2DLayer CreateMaterialResponse(
            string displayName,
            TextStudio2DMaterialResponseSpec materialResponse)
        {
            TextStudio2DLayer layer = Defaults(
                TextStudio2DLayerKind.MaterialResponse,
                displayName);
            layer._materialResponse = materialResponse;
            return layer;
        }

        /// <summary>Creates a named graphic-effect layer with the supplied pattern and color settings.</summary>
        public static TextStudio2DLayer CreateGraphicEffect(
            string displayName,
            TextStudio2DGraphicEffectSpec graphicEffect)
        {
            TextStudio2DLayer layer = Defaults(
                TextStudio2DLayerKind.GraphicEffect,
                displayName);
            layer._graphicEffect = graphicEffect;
            return layer;
        }

        /// <summary>Creates a named animated-effect layer with the supplied timing, shape, color, and motion settings.</summary>
        public static TextStudio2DLayer CreateAnimatedEffect(
            string displayName,
            TextStudio2DAnimatedEffectSpec animatedEffect)
        {
            TextStudio2DLayer layer = Defaults(
                TextStudio2DLayerKind.AnimatedEffect,
                displayName);
            layer._animatedEffect = animatedEffect;
            return layer;
        }

        /// <summary>Creates a named color-adjustment layer with the supplied transform.</summary>
        public static TextStudio2DLayer CreateColorAdjustment(
            string displayName,
            TextStudio2DColorAdjustmentSpec colorAdjustment)
        {
            TextStudio2DLayer layer = Defaults(
                TextStudio2DLayerKind.ColorAdjustment,
                displayName);
            layer._colorAdjustment = colorAdjustment;
            return layer;
        }

        /// <summary>Creates a named composite-adjustment layer with the supplied color transform.</summary>
        public static TextStudio2DLayer CreateCompositeAdjustment(
            string displayName,
            TextStudio2DColorAdjustmentSpec adjustment)
        {
            TextStudio2DLayer layer = Defaults(TextStudio2DLayerKind.CompositeAdjustment, displayName);
            layer._colorAdjustment = adjustment;
            return layer;
        }

        /// <summary>Returns a layer copy with the supplied persistent stack-item identity.</summary>
        public TextStudio2DLayer WithIdentity(TextStudioStackItemId itemId)
        {
            TextStudio2DLayer layer = this;
            layer._itemId = itemId;
            return layer;
        }

        /// <summary>Returns a layer copy with the supplied enabled state, allowing solo mode only while enabled.</summary>
        public TextStudio2DLayer WithState(bool enabled, bool solo = false)
        {
            TextStudio2DLayer layer = this;
            layer._enabled = enabled;
            layer._solo = enabled && solo;
            return layer;
        }

        /// <summary>Returns a layer copy with the supplied opacity and blend mode.</summary>
        public TextStudio2DLayer WithCompositing(
            float opacity,
            TextStudio2DBlendMode blendMode = TextStudio2DBlendMode.Normal)
        {
            TextStudio2DLayer layer = this;
            layer._opacity = opacity;
            layer._blendMode = blendMode;
            return layer;
        }

        /// <summary>Returns a layer copy with the supplied execution path and quality tier.</summary>
        public TextStudio2DLayer WithExecution(
            TextStudio2DExecutionPath executionPath,
            TextStudio2DQualityTier qualityTier = TextStudio2DQualityTier.Balanced)
        {
            TextStudio2DLayer layer = this;
            layer._executionPath = executionPath;
            layer._qualityTier = qualityTier;
            return layer;
        }

        /// <summary>Returns a layer copy with the supplied application selector.</summary>
        public TextStudio2DLayer WithSelector(TextStudioApplicationSelector selector)
        {
            TextStudio2DLayer layer = this;
            layer._selector = selector;
            return layer;
        }

        /// <summary>Sets the ordering of this group's child layers; nested groups remain isolated owners.</summary>
        public TextStudio2DLayer WithGroupOrder(TextStudio2DGroupOrder order)
        {
            TextStudio2DLayer layer = this;
            layer._groupOrder = order;
            return layer;
        }

        /// <summary>Draws this decoration once before or after its scope's characters.</summary>
        public TextStudio2DLayer WithSharedSurfaceOrder(TextStudio2DSharedSurfaceOrder order)
        {
            TextStudio2DLayer layer = this;
            layer._sharedSurfaceOrder = order;
            return layer;
        }

        /// <summary>Returns a layer copy with the supplied parent-group and mask references.</summary>
        public TextStudio2DLayer WithRelationships(
            TextStudioStackItemId parentId,
            TextStudioStackItemId maskId = default)
        {
            TextStudio2DLayer layer = this;
            layer._parentId = parentId;
            layer._maskId = maskId;
            return layer;
        }

        /// <summary>Returns a layer copy with the supplied participation in animated tinting.</summary>
        public TextStudio2DLayer WithAnimatedTint(bool participates)
        {
            TextStudio2DLayer layer = this;
            layer._participatesInAnimatedTint = participates;
            return layer;
        }

        /// <summary>Returns a layer copy targeting the supplied source-content categories.</summary>
        public TextStudio2DLayer WithSourceApplicability(TextStudio2DSourceApplicability sources)
        {
            TextStudio2DLayer layer = this;
            layer._sourceApplicability = sources;
            return layer;
        }

        /// <summary>Replaces this layer's paint with an independently owned binding.</summary>
        public TextStudio2DLayer WithPaint(TextStudio2DPaint paint)
        {
            TextStudio2DLayer layer = this;
            layer._paint = paint.Clone();
            return layer;
        }

        internal TextStudio2DLayer Clone()
        {
            TextStudio2DLayer clone = this;
            clone._paint = _paint.Clone();
            clone._stroke = _stroke.Clone();
            clone._decoration = _decoration.Clone();
            clone._appearanceActivation = _appearanceActivation.Clone();
            TextStudioApplicationSelectorNode[] nodes = _selector.GetNodesUnsafe();
            clone._selector = TextStudioApplicationSelector.Create(nodes, _selector.Root);
            return clone;
        }

        /// <summary>Returns a layer copy with an independently copied appearance-activation specification.</summary>
        public TextStudio2DLayer WithAppearanceActivation(TextStudioAppearanceActivation activation)
        {
            TextStudio2DLayer copy = this;
            copy._appearanceActivation = activation.Clone();
            return copy;
        }

        internal TextStudio2DLayer WithRepairedIdentity(TextStudioStackItemId itemId)
        {
            TextStudio2DLayer layer = this;
            layer._itemId = itemId;
            return layer;
        }

        internal TextStudio2DLayer WithSurfaceOwner(TextStudioStackItemId ownerId)
        {
            TextStudio2DLayer layer = this;
            layer._surfaceOwnerId = ownerId;
            return layer;
        }

        internal TextStudio2DLayer WithRepairedTreatmentIdentities(HashSet<TextStudioStackItemId> identities)
        {
            TextStudio2DLayer layer = this;
            layer._decoration = _decoration.Clone();
            layer._decoration.RepairTreatmentIdentities(identities);
            return layer;
        }

        static TextStudio2DLayer Defaults(TextStudio2DLayerKind kind, string displayName)
        {
            return new TextStudio2DLayer
            {
                _itemId = TextStudioStackItemId.CreateUnique("layer"),
                _displayName = string.IsNullOrWhiteSpace(displayName) ? kind.ToString() : displayName,
                _kind = kind,
                _enabled = true,
                _solo = false,
                _participatesInAnimatedTint = true,
                _opacity = 1f,
                _blendMode = TextStudio2DBlendMode.Normal,
                _executionPath = TextStudio2DExecutionPath.Automatic,
                _qualityTier = TextStudio2DQualityTier.Balanced,
                _parentId = default,
                _maskId = default,
                _selector = TextStudioApplicationSelector.All,
                _paint = TextStudio2DPaint.Solid(Color.white),
                _fill = default,
                _stroke = new TextStudio2DStrokeSpec(0.05f),
                _shadow = new TextStudio2DShadowSpec(new Vector2(0.05f, -0.05f), 0.05f),
                _glow = new TextStudio2DGlowSpec(0.1f),
                _bevel = new TextStudio2DBevelSpec(
                    0.05f,
                    0.02f,
                    0.5f,
                    135f,
                    45f,
                    Color.white,
                    Color.black),
                _fauxDepth = new TextStudio2DFauxDepthSpec(new Vector2(0.08f, -0.08f)),
                _decoration = new TextStudio2DDecorationSpec(
                    TextStudio2DDecorationKind.Underline,
                    0.05f),
                _mask = default,
                _materialResponse = TextStudio2DMaterialResponseSpec.Preset(
                    TextStudio2DMaterialResponseKind.Emboss),
                _graphicEffect = TextStudio2DGraphicEffectSpec.Preset(
                    TextStudio2DGraphicEffectKind.Halftone),
                _animatedEffect = TextStudio2DAnimatedEffectSpec.Preset(
                    TextStudio2DAnimatedEffectKind.LightSweep),
                _colorAdjustment = TextStudio2DColorAdjustmentSpec.Identity
            };
        }
    }

}
