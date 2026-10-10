using System;
using UnityEngine;

namespace TextStudio
{
    [Flags]
    public enum TextStudio2DPaintOverrides : ushort
    {
        None = 0,
        Mapping = 1 << 0,
        Tint = 1 << 1,
        Transform = 1 << 2,
        Spread = 1 << 3,
        TextureFit = 1 << 4,
        Distance = 1 << 5,
        All = Mapping | Tint | Transform | Spread | TextureFit | Distance
    }

    public enum TextStudio2DRangeMapping : byte
    {
        EachMatch = 0,
        EntireSelection = 1
    }

    /// <summary>A directly referenced paint. Layers resolve it once during style compilation.</summary>
    [CreateAssetMenu(fileName = "TextStudioPaint", menuName = "Text Studio/Appearance/Paint", order = 102)]
    public sealed class TextStudio2DPaintAsset : ScriptableObject
    {
        [SerializeField] TextStudio2DPaint _paint = TextStudio2DPaint.Solid(Color.white);
        [NonSerialized] int _revision;

        public int Revision => _revision;
        public TextStudio2DPaint Paint => _paint.Clone();
        internal TextStudio2DPaint Value => _paint;

        /// <summary>Stores an independently owned local paint value and advances the asset revision.</summary>
        public void SetPaint(TextStudio2DPaint paint)
        {
            // Assets own a concrete value. Reference chains have no useful authoring semantics.
            _paint = paint.MakeLocal();
            unchecked { _revision++; }
        }

        void OnValidate()
        {
            _paint = _paint.MakeLocal();
            unchecked { _revision++; }
        }
    }

    public partial struct TextStudio2DPaint
    {
        [SerializeField] TextStudio2DPaintAsset _source;
        [SerializeField] bool _useSource;
        [SerializeField] TextStudio2DPaintOverrides _overrides;
        [SerializeField] TextStudio2DRangeMapping _rangeMapping;

        public TextStudio2DPaintAsset Source => _source;
        public bool UsesSharedSource => _useSource;
        public TextStudio2DPaintOverrides Overrides => _overrides;
        public TextStudio2DRangeMapping RangeMapping => _rangeMapping;

        /// <summary>Creates a binding to a paint asset with no local overrides.</summary>
        public static TextStudio2DPaint Reference(TextStudio2DPaintAsset source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            TextStudio2DPaint paint = source.Paint;
            paint._source = source;
            paint._useSource = true;
            paint._overrides = TextStudio2DPaintOverrides.None;
            return paint;
        }

        /// <summary>Returns a paint copy with the specified set of local overrides for referenced paint.</summary>
        public TextStudio2DPaint WithOverrides(TextStudio2DPaintOverrides overrides)
        {
            TextStudio2DPaint paint = Clone();
            paint._overrides = overrides;
            return paint;
        }

        /// <summary>Returns a paint copy with locally overridden mapping space and range mapping behavior.</summary>
        public TextStudio2DPaint WithMapping(
            TextStudio2DMappingSpace mapping,
            TextStudio2DRangeMapping range = TextStudio2DRangeMapping.EachMatch)
        {
            TextStudio2DPaint paint = Clone();
            paint._mapping = mapping;
            paint._rangeMapping = range;
            paint._overrides |= TextStudio2DPaintOverrides.Mapping;
            return paint;
        }

        /// <summary>Returns a paint copy with the supplied tint marked as a local override.</summary>
        public TextStudio2DPaint WithTint(Color tint)
        {
            TextStudio2DPaint paint = Clone();
            paint._color = tint;
            paint._overrides |= TextStudio2DPaintOverrides.Tint;
            return paint;
        }

        /// <summary>Returns a paint copy with locally overridden angle, center, scale, and offset.</summary>
        public TextStudio2DPaint WithTransform(float angle, Vector2 center, Vector2 scale, Vector2 offset)
        {
            TextStudio2DPaint paint = Clone();
            paint._angle = angle;
            paint._center = center;
            paint._scale = scale;
            paint._offset = offset;
            paint._overrides |= TextStudio2DPaintOverrides.Transform;
            return paint;
        }

        /// <summary>Copies the resolved value and severs the asset reference.</summary>
        public TextStudio2DPaint MakeLocal()
        {
            return Resolve().Clone();
        }

        internal TextStudio2DPaint Resolve()
        {
            TextStudio2DPaint paint = _source != null ? _source.Value : this;
            if (_source != null)
            {
                if ((_overrides & TextStudio2DPaintOverrides.Mapping) != 0)
                {
                    paint._mapping = _mapping;
                    paint._rangeMapping = _rangeMapping;
                }
                if ((_overrides & TextStudio2DPaintOverrides.Tint) != 0)
                    paint._color = _color;
                if ((_overrides & TextStudio2DPaintOverrides.Transform) != 0)
                {
                    paint._angle = _angle;
                    paint._center = _center;
                    paint._scale = _scale;
                    paint._offset = _offset;
                }
                if ((_overrides & TextStudio2DPaintOverrides.Spread) != 0)
                    paint._spread = _spread;
                if ((_overrides & TextStudio2DPaintOverrides.TextureFit) != 0)
                    paint._textureFit = _textureFit;
                if ((_overrides & TextStudio2DPaintOverrides.Distance) != 0)
                {
                    paint._distanceDomain = _distanceDomain;
                    paint._distanceRange = _distanceRange;
                }
            }
            paint._source = null;
            paint._useSource = false;
            paint._overrides = TextStudio2DPaintOverrides.None;
            return paint;
        }
    }
}
