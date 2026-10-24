using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>The authored visual promise made by a writing style.</summary>
    public enum TextStudioWritingMode : byte
    {
        None = 0,
        InkReveal = 5,
        OutlineTrace = 6,
        AuthoredCenterline = 7,
        CalligraphyPressure = 8,
        Marker = 9,
        Chalk = 10,
        StrokeToFill = 11,
    }

    /// <summary>Describes the strongest source data backing a resolved writing result.</summary>
    public enum TextStudioWritingDataLevel : byte
    {
        None = 0,
        OrdinaryTypewriter = 1,
        InkCoverage = 2,
        PreparedOutline = 3,
        AuthoredCenterline = 4
    }

    /// <summary>Selects the cardinal or diagonal direction used by directional Writing reveals.</summary>
    public enum TextStudioWritingDirection : byte
    {
        LeftToRight = 0,
        RightToLeft = 1,
        TopToBottom = 2,
        BottomToTop = 3,
        TopLeftToBottomRight = 4,
        TopRightToBottomLeft = 5,
        BottomLeftToTopRight = 6,
        BottomRightToTopLeft = 7
    }

    /// <summary>Analytical retained-ribbon profile used without extra textures, materials, or draws.</summary>
    public enum TextStudioWritingBrush : byte
    {
        Solid = 0,
        Ink = 1,
        Calligraphy = 2,
        Marker = 3,
        Chalk = 4
    }

    /// <summary>Selects how one Writing Style is presented by an extruded 3D renderer.</summary>
    public enum TextStudioWriting3DMode : byte
    {
        /// <summary>Reveals the existing 3D surfaces without adding auxiliary geometry.</summary>
        SurfaceReveal = 0,
        /// <summary>Places one retained, shallow ribbon over the front surface.</summary>
        RaisedRibbon = 1,
        /// <summary>Places one retained volumetric tube along prepared writing paths.</summary>
        VolumetricTube = 2
    }

    /// <summary>Distinguishes a prepared Writing path along a stroke centerline from one following a glyph outline.</summary>
    public enum TextStudioWritingStrokeRole : byte
    {
        Centerline = 0,
        Outline = 1
    }

    /// <summary>Selects the round, butt, or square shape used at the ends of a rendered Writing stroke.</summary>
    public enum TextStudioWritingStrokeCap : byte
    {
        Round = 0,
        Butt = 1,
        Square = 2
    }

    /// <summary>Selects the round, bevel, or miter shape used where adjacent Writing stroke segments meet.</summary>
    public enum TextStudioWritingStrokeJoin : byte
    {
        Round = 0,
        Bevel = 1,
        Miter = 2
    }

    /// <summary>One cubic path knot. Control points are stored in the same coordinate space as the knot.</summary>
    [Serializable]
    public struct TextStudioWritingKnot
    {
        [SerializeField] Vector2 _position;
        [SerializeField] Vector2 _incomingControl;
        [SerializeField] Vector2 _outgoingControl;
        [SerializeField] float _pressure;
        [SerializeField] float _time;

        public Vector2 Position => _position;
        public Vector2 IncomingControl => _incomingControl;
        public Vector2 OutgoingControl => _outgoingControl;
        public float Pressure => _pressure;
        public float Time => _time;

        /// <summary>Creates a cubic path knot with absolute control points, clamping pressure and time to zero or greater.</summary>
        public TextStudioWritingKnot(
            Vector2 position,
            Vector2 incomingControl,
            Vector2 outgoingControl,
            float pressure = 1f,
            float time = 0f)
        {
            _position = position;
            _incomingControl = incomingControl;
            _outgoingControl = outgoingControl;
            _pressure = Mathf.Max(0f, pressure);
            _time = Mathf.Max(0f, time);
        }

        /// <summary>Creates a knot whose control points coincide with its position for a corner between adjoining curve segments.</summary>
        public static TextStudioWritingKnot Corner(
            Vector2 position,
            float pressure = 1f,
            float time = 0f)
        {
            return new TextStudioWritingKnot(
                position,
                position,
                position,
                pressure,
                time);
        }
    }

    /// <summary>An authored pen-down path. Separate rows represent explicit pen lifts.</summary>
    [Serializable]
    public struct TextStudioWritingStroke
    {
        [SerializeField] TextStudioStackItemId _strokeId;
        [SerializeField] TextStudioWritingStrokeRole _role;
        [SerializeField] bool _closed;
        [SerializeField] TextStudioWritingKnot[] _knots;

        public TextStudioStackItemId StrokeId => _strokeId;
        public TextStudioWritingStrokeRole Role => _role;
        public bool Closed => _closed;
        public int KnotCount => _knots?.Length ?? 0;

        /// <summary>Creates a pen-down stroke with a role, closure flag, and its own copy of the knot array.</summary>
        public TextStudioWritingStroke(
            TextStudioStackItemId strokeId,
            TextStudioWritingStrokeRole role,
            TextStudioWritingKnot[] knots,
            bool closed = false)
        {
            _strokeId = strokeId;
            _role = role;
            _closed = closed;
            _knots = knots != null
                ? (TextStudioWritingKnot[])knots.Clone()
                : Array.Empty<TextStudioWritingKnot>();
        }

        /// <summary>Returns the cubic knot at the specified index, including its pressure, time, and absolute control points.</summary>
        public TextStudioWritingKnot GetKnot(int index)
        {
            if (_knots == null || index < 0 || index >= _knots.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _knots[index];
        }

        internal TextStudioWritingStroke Clone()
        {
            return new TextStudioWritingStroke(
                _strokeId,
                _role,
                _knots,
                _closed);
        }

        internal TextStudioWritingStroke WithRepairedIdentity(
            TextStudioStackItemId identity)
        {
            TextStudioWritingStroke copy = Clone();
            copy._strokeId = identity;
            return copy;
        }
    }

    /// <summary>Stable font, glyph, and variation identity for an authored glyph path.</summary>
    [Serializable]
    public struct TextStudioWritingGlyphKey : IEquatable<TextStudioWritingGlyphKey>
    {
        [SerializeField] ulong _fontIdentity;
        [SerializeField] uint _glyphIndex;
        [SerializeField] string _variationFingerprint;

        public ulong FontIdentity => _fontIdentity;
        public uint GlyphIndex => _glyphIndex;
        public string VariationFingerprint => _variationFingerprint ?? string.Empty;

        /// <summary>Combines font identity, glyph index, and variation fingerprint into a writing path lookup key; null variation text becomes empty.</summary>
        public TextStudioWritingGlyphKey(
            ulong fontIdentity,
            uint glyphIndex,
            string variationFingerprint = "")
        {
            _fontIdentity = fontIdentity;
            _glyphIndex = glyphIndex;
            _variationFingerprint = variationFingerprint ?? string.Empty;
        }

        public bool Equals(TextStudioWritingGlyphKey other)
        {
            return _fontIdentity == other._fontIdentity &&
                   _glyphIndex == other._glyphIndex &&
                   string.Equals(
                       VariationFingerprint,
                       other.VariationFingerprint,
                       StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioWritingGlyphKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _fontIdentity.GetHashCode();
                hash = hash * 397 ^ (int)_glyphIndex;
                return hash * 397 ^
                       StringComparer.Ordinal.GetHashCode(VariationFingerprint);
            }
        }
    }

    /// <summary>Reusable writing appearance and playback settings.</summary>
    [Serializable]
    public struct TextStudioWritingConfiguration
    {
        [SerializeField] TextStudioWritingMode _mode;
        [SerializeField] TextStudioWritingDirection _direction;
        [SerializeField] TextStudioWritingBrush _brush;
        [SerializeField] TextStudioWritingStrokeCap _cap;
        [SerializeField] TextStudioWritingStrokeJoin _join;
        [SerializeField] TextStudioReducedMotionBehavior _reducedMotionBehavior;
        [SerializeField] Color _color;
        [SerializeField, Range(0.001f, 4f)] float _width;
        [SerializeField, Range(0f, 1f)] float _edgeSoftness;
        [SerializeField, Range(0f, 0.25f)] float _strokeGap;
        [SerializeField, Range(0f, 1f)] float _fillStart;
        [SerializeField, Range(0.001f, 1f)] float _fillDuration;
        [SerializeField, Range(1f, 16f)] float _miterLimit;
        [SerializeField] bool _reverse;
        [SerializeField] TextStudioWriting3DMode _threeDMode;
        [SerializeField, Range(-1f, 4f)] float _threeDSurfaceOffset;
        [SerializeField, Range(0.001f, 4f)] float _threeDReliefDepth;
        [SerializeField, Range(3, 16)] int _threeDRadialSegments;

        public TextStudioWritingMode Mode => _mode;
        public TextStudioWritingDirection Direction => _direction;
        public TextStudioWritingBrush Brush => _brush;
        public TextStudioWritingStrokeCap Cap => _cap;
        public TextStudioWritingStrokeJoin Join => _join;
        public TextStudioReducedMotionBehavior ReducedMotionBehavior =>
            _reducedMotionBehavior;
        public Color Color => _color;
        public float Width => _width;
        public float EdgeSoftness => _edgeSoftness;
        public float StrokeGap => _strokeGap;
        public float FillStart => _fillStart;
        public float FillDuration => _fillDuration;
        public float MiterLimit => _miterLimit;
        public bool Reverse => _reverse;
        public TextStudioWriting3DMode ThreeDMode => _threeDMode;
        public float ThreeDSurfaceOffset => _threeDSurfaceOffset;
        public float ThreeDReliefDepth => _threeDReliefDepth;
        public int ThreeDRadialSegments => _threeDRadialSegments;

        /// <summary>Creates authored writing settings for brush appearance, stroke geometry, reveal timing, reduced motion, and 3D presentation.</summary>
        public TextStudioWritingConfiguration(
            TextStudioWritingMode mode,
            TextStudioWritingDirection direction,
            Color color,
            float width = 0.08f,
            float edgeSoftness = 0.02f,
            float strokeGap = 0.02f,
            float fillStart = 0.72f,
            float fillDuration = 0.28f,
            bool reverse = false,
            TextStudioWritingStrokeCap cap = TextStudioWritingStrokeCap.Round,
            TextStudioWritingStrokeJoin join = TextStudioWritingStrokeJoin.Round,
            float miterLimit = 4f,
            TextStudioReducedMotionBehavior reducedMotionBehavior =
                TextStudioReducedMotionBehavior.Reduce,
            TextStudioWriting3DMode threeDMode =
                TextStudioWriting3DMode.SurfaceReveal,
            float threeDSurfaceOffset = 0.002f,
            float threeDReliefDepth = 0.025f,
            int threeDRadialSegments = 8,
            TextStudioWritingBrush brush = TextStudioWritingBrush.Solid)
        {
            _mode = mode;
            _direction = direction;
            _brush = brush;
            _cap = cap;
            _join = join;
            _reducedMotionBehavior = reducedMotionBehavior;
            _color = color;
            _width = width;
            _edgeSoftness = edgeSoftness;
            _strokeGap = strokeGap;
            _fillStart = fillStart;
            _fillDuration = fillDuration;
            _miterLimit = miterLimit;
            _reverse = reverse;
            _threeDMode = threeDMode;
            _threeDSurfaceOffset = threeDSurfaceOffset;
            _threeDReliefDepth = threeDReliefDepth;
            _threeDRadialSegments = threeDRadialSegments;
        }

        /// <summary>Returns a copy that draws its strokes in <paramref name="color"/> and keeps every other setting.</summary>
        public TextStudioWritingConfiguration WithColor(Color color)
        {
            TextStudioWritingConfiguration copy = this;
            copy._color = color;
            return copy;
        }

        public static TextStudioWritingConfiguration Default =>
            new TextStudioWritingConfiguration(
                TextStudioWritingMode.InkReveal,
                TextStudioWritingDirection.LeftToRight,
                Color.white);

        internal TextStudioWritingConfiguration Sanitized()
        {
            TextStudioWritingConfiguration copy = this;
            if ((byte)_brush > (byte)TextStudioWritingBrush.Chalk)
                copy._brush = TextStudioWritingBrush.Solid;
            copy._width = Mathf.Clamp(_width, 0.001f, 4f);
            copy._edgeSoftness = Mathf.Clamp(_edgeSoftness, 0f, 1f);
            copy._strokeGap = Mathf.Clamp(_strokeGap, 0f, 0.25f);
            copy._fillStart = Mathf.Clamp01(_fillStart);
            copy._fillDuration = Mathf.Clamp(_fillDuration, 0.001f, 1f);
            copy._miterLimit = Mathf.Clamp(_miterLimit, 1f, 16f);
            copy._threeDSurfaceOffset = Mathf.Clamp(
                _threeDSurfaceOffset,
                -1f,
                4f);
            copy._threeDReliefDepth = Mathf.Clamp(
                _threeDReliefDepth,
                0.001f,
                4f);
            copy._threeDRadialSegments = Mathf.Clamp(
                _threeDRadialSegments,
                3,
                16);
            return copy;
        }
    }

    /// <summary>Maps writing modes to the source data required by their authored appearance.</summary>
    public static class TextStudioWritingModeUtility
    {
        /// <summary>Maps each writing mode to its required data level, including authored centerlines for calligraphy, marker, chalk, and stroke-to-fill treatments.</summary>
        public static TextStudioWritingDataLevel RequestedDataLevel(
            TextStudioWritingMode mode)
        {
            switch (mode)
            {
                case TextStudioWritingMode.None:
                    return TextStudioWritingDataLevel.None;
                case TextStudioWritingMode.OutlineTrace:
                    return TextStudioWritingDataLevel.PreparedOutline;
                case TextStudioWritingMode.AuthoredCenterline:
                case TextStudioWritingMode.CalligraphyPressure:
                case TextStudioWritingMode.Marker:
                case TextStudioWritingMode.Chalk:
                case TextStudioWritingMode.StrokeToFill:
                    return TextStudioWritingDataLevel.AuthoredCenterline;
                default:
                    return TextStudioWritingDataLevel.InkCoverage;
            }
        }
    }
}
