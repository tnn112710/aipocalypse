using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Professional editorial and print-inspired treatments for the 2D style stack.</summary>
    public enum TextStudio2DGraphicEffectKind : byte
    {
        Halftone = 0,
        Grain = 1,
        Duotone = 2,
        Distress = 3,
        InkBleed = 4,
        Letterpress = 5,
        StencilCutout = 6,
        StickerPlate = 7,
        CmykOffset = 8,
        RgbSeparation = 9,
        PrismaticEdge = 10,
        EngravedLine = 11
    }

    /// <summary>Optional directional radius modulation for the halftone dot field.</summary>
    [Serializable]
    public struct TextStudio2DHalftoneSizeRamp
    {
        [SerializeField] bool _enabled;
        [SerializeField, TextStudio2DParameter(TextStudio2DParameterKind.HalftoneMinimumRadius)] float _minimumRadius;
        [SerializeField, TextStudio2DParameter(TextStudio2DParameterKind.HalftoneMaximumRadius)] float _maximumRadius;
        [SerializeField] float _angleDegrees;

        public bool Enabled => _enabled;
        public float MinimumRadius => _minimumRadius;
        public float MaximumRadius => _maximumRadius;
        public float AngleDegrees => _angleDegrees;

        /// <summary>Creates a halftone size ramp with the supplied enabled state, minimum and maximum radii, and angle in degrees.</summary>
        public TextStudio2DHalftoneSizeRamp(
            bool enabled,
            float minimumRadius,
            float maximumRadius,
            float angleDegrees)
        {
            _enabled = enabled;
            _minimumRadius = minimumRadius;
            _maximumRadius = maximumRadius;
            _angleDegrees = angleDegrees;
        }
    }

    /// <summary>
    /// Compact parameters shared by the editorial effect family. Presets provide finished
    /// defaults; fluent modifiers expose deterministic expert controls without shader variants.
    /// </summary>
    [Serializable]
    public struct TextStudio2DGraphicEffectSpec
    {
        [SerializeField] TextStudio2DGraphicEffectKind _kind;
        [SerializeField] TextStudio2DMappingSpace _mapping;
        [SerializeField, Range(0f, 4f), Tooltip("Treatment intensity from 0 to 4. Values above 1 strengthen the effect.")] float _intensity;
        [SerializeField, TextStudio2DParameter(TextStudio2DParameterKind.PatternScale), Tooltip("Pattern frequency in mapping-space units, from 0.01 to 4096. The slider is logarithmic; the numeric input uses the exact frequency.")] float _scale;
        [SerializeField, Range(0f, 1f), Tooltip("Fine pattern detail from 0 to 1.")] float _detail;
        [SerializeField, Range(0f, 2f), Tooltip("Treatment width from 0 to 2 in the effect's normalized field or pattern domain.")] float _width;
        [SerializeField, Range(0f, 2f), Tooltip("Treatment edge softness from 0 to 2 in the effect's normalized domain.")] float _softness;
        [SerializeField] float _angle;
        [SerializeField] Vector2 _offset;
        [SerializeField] Color _primaryColor;
        [SerializeField] Color _secondaryColor;
        [SerializeField] int _seed;
        [SerializeField] TextStudio2DHalftoneSizeRamp _halftoneSizeRamp;

        public TextStudio2DGraphicEffectKind Kind => _kind;
        public TextStudio2DMappingSpace Mapping => _mapping;
        public float Intensity => _intensity;
        public float Scale => _scale;
        public float Detail => _detail;
        public float Width => _width;
        public float Softness => _softness;
        public float Angle => _angle;
        public Vector2 Offset => _offset;
        public Color PrimaryColor => _primaryColor;
        public Color SecondaryColor => _secondaryColor;
        public int Seed => _seed;
        public TextStudio2DHalftoneSizeRamp HalftoneSizeRamp => _halftoneSizeRamp;

        /// <summary>Creates a graphic-effect specification with the supplied mapping, intensity, pattern geometry, colors, and deterministic seed.</summary>
        public TextStudio2DGraphicEffectSpec(
            TextStudio2DGraphicEffectKind kind,
            TextStudio2DMappingSpace mapping,
            float intensity,
            float scale,
            float detail,
            float width,
            float softness,
            float angle,
            Vector2 offset,
            Color primaryColor,
            Color secondaryColor,
            int seed)
        {
            _kind = kind;
            _mapping = mapping;
            _intensity = intensity;
            _scale = scale;
            _detail = detail;
            _width = width;
            _softness = softness;
            _angle = angle;
            _offset = offset;
            _primaryColor = primaryColor;
            _secondaryColor = secondaryColor;
            _seed = seed;
            _halftoneSizeRamp = default;
        }

        /// <summary>Returns a polished deterministic starting point for an editorial family.</summary>
        public static TextStudio2DGraphicEffectSpec Preset(
            TextStudio2DGraphicEffectKind kind)
        {
            switch (kind)
            {
                case TextStudio2DGraphicEffectKind.Grain:
                    return Create(kind, TextStudio2DMappingSpace.Text, 0.42f, 64f, 0.82f, 0.5f, 0.08f, 0f, Vector2.zero,
                        new Color(1f, 0.96f, 0.88f, 0.42f), new Color(0.08f, 0.055f, 0.035f, 0.32f), 191);
                case TextStudio2DGraphicEffectKind.Duotone:
                    return Create(kind, TextStudio2DMappingSpace.Text, 0.78f, 7f, 0.62f, 0.5f, 0.12f, -18f, Vector2.zero,
                        new Color(1f, 0.2f, 0.48f, 1f), new Color(0.035f, 0.06f, 0.18f, 1f), 211);
                case TextStudio2DGraphicEffectKind.Distress:
                    return Create(kind, TextStudio2DMappingSpace.Text, 0.72f, 15f, 0.78f, 0.52f, 0.075f, 13f, Vector2.zero,
                        new Color(0.96f, 0.9f, 0.74f, 1f), new Color(0.12f, 0.075f, 0.035f, 1f), 233);
                case TextStudio2DGraphicEffectKind.InkBleed:
                    return Create(kind, TextStudio2DMappingSpace.Glyph, 0.68f, 11f, 0.72f, 0.055f, 0.025f, 0f, Vector2.zero,
                        new Color(0.035f, 0.025f, 0.04f, 1f), new Color(0.22f, 0.055f, 0.08f, 0.65f), 257);
                case TextStudio2DGraphicEffectKind.Letterpress:
                    return Create(kind, TextStudio2DMappingSpace.Glyph, 0.84f, 9f, 0.45f, 0.11f, 0.035f, 135f, Vector2.zero,
                        new Color(1f, 0.94f, 0.78f, 0.72f), new Color(0.055f, 0.035f, 0.025f, 0.82f), 271);
                case TextStudio2DGraphicEffectKind.StencilCutout:
                    return Create(kind, TextStudio2DMappingSpace.Glyph, 0.88f, 10f, 0.3f, 0.24f, 0.035f, 90f, Vector2.zero,
                        new Color(0.92f, 0.94f, 0.88f, 1f), new Color(0.08f, 0.09f, 0.075f, 1f), 293);
                case TextStudio2DGraphicEffectKind.StickerPlate:
                    return Create(kind, TextStudio2DMappingSpace.Glyph, 1f, 1f, 0f, 0.14f, 0.025f, 0f, new Vector2(0.075f, -0.065f),
                        new Color(1f, 0.98f, 0.92f, 1f), new Color(0.05f, 0.06f, 0.085f, 0.82f), 307);
                case TextStudio2DGraphicEffectKind.CmykOffset:
                    return Create(kind, TextStudio2DMappingSpace.Glyph, 0.88f, 1f, 0f, 0.025f, 0.015f, 0f, new Vector2(0.12f, 0.085f),
                        Color.white, new Color(0.035f, 0.035f, 0.04f, 0.9f), 331);
                case TextStudio2DGraphicEffectKind.RgbSeparation:
                    return Create(kind, TextStudio2DMappingSpace.Glyph, 0.9f, 1f, 0f, 0.02f, 0.012f, 0f, new Vector2(0.11f, 0.045f),
                        Color.white, Color.black, 347);
                case TextStudio2DGraphicEffectKind.PrismaticEdge:
                    return Create(kind, TextStudio2DMappingSpace.Glyph, 0.92f, 4.5f, 0.35f, 0.12f, 0.025f, 24f, Vector2.zero,
                        new Color(1f, 1f, 1f, 0.92f), new Color(0.12f, 0.05f, 0.28f, 0.42f), 359);
                case TextStudio2DGraphicEffectKind.EngravedLine:
                    return Create(kind, TextStudio2DMappingSpace.Glyph, 0.82f, 18f, 0.28f, 0.065f, 0.018f, 42f, Vector2.zero,
                        new Color(0.96f, 0.88f, 0.66f, 0.82f), new Color(0.07f, 0.045f, 0.028f, 0.88f), 379);
                default:
                    return Create(kind, TextStudio2DMappingSpace.Text, 0.82f, 18f, 0.35f, 0.34f, 0.045f, 22.5f, Vector2.zero,
                        new Color(0.96f, 0.92f, 0.78f, 1f), new Color(0.08f, 0.06f, 0.045f, 1f), 173);
            }
        }

        /// <summary>Returns a graphic-effect copy with the supplied intensity, scale, detail, width, softness, angle, and offset.</summary>
        public TextStudio2DGraphicEffectSpec WithGeometry(
            float intensity,
            float scale,
            float detail,
            float width,
            float softness,
            float angle,
            Vector2 offset)
        {
            TextStudio2DGraphicEffectSpec value = this;
            value._intensity = intensity;
            value._scale = scale;
            value._detail = detail;
            value._width = width;
            value._softness = softness;
            value._angle = angle;
            value._offset = offset;
            return value;
        }

        /// <summary>Returns a graphic-effect copy with the supplied primary and secondary colors.</summary>
        public TextStudio2DGraphicEffectSpec WithColors(
            Color primary,
            Color secondary)
        {
            TextStudio2DGraphicEffectSpec value = this;
            value._primaryColor = primary;
            value._secondaryColor = secondary;
            return value;
        }

        /// <summary>Returns a graphic-effect copy using the supplied mapping space.</summary>
        public TextStudio2DGraphicEffectSpec WithMapping(TextStudio2DMappingSpace mapping)
        {
            TextStudio2DGraphicEffectSpec value = this;
            value._mapping = mapping;
            return value;
        }

        /// <summary>Returns a graphic-effect copy using the supplied deterministic seed.</summary>
        public TextStudio2DGraphicEffectSpec WithSeed(int seed)
        {
            TextStudio2DGraphicEffectSpec value = this;
            value._seed = seed;
            return value;
        }

        /// <summary>Sets optional directional dot sizing used by the Halftone family.</summary>
        public TextStudio2DGraphicEffectSpec WithHalftoneSizeRamp(
            TextStudio2DHalftoneSizeRamp ramp)
        {
            TextStudio2DGraphicEffectSpec value = this;
            value._halftoneSizeRamp = ramp;
            return value;
        }

        /// <summary>Enables a directional halftone dot-size ramp.</summary>
        public TextStudio2DGraphicEffectSpec WithHalftoneSizeRamp(
            float minimumRadius,
            float maximumRadius,
            float angleDegrees)
        {
            return WithHalftoneSizeRamp(new TextStudio2DHalftoneSizeRamp(
                true,
                minimumRadius,
                maximumRadius,
                angleDegrees));
        }

        static TextStudio2DGraphicEffectSpec Create(
            TextStudio2DGraphicEffectKind kind,
            TextStudio2DMappingSpace mapping,
            float intensity,
            float scale,
            float detail,
            float width,
            float softness,
            float angle,
            Vector2 offset,
            Color primary,
            Color secondary,
            int seed)
        {
            return new TextStudio2DGraphicEffectSpec(
                kind,
                mapping,
                intensity,
                scale,
                detail,
                width,
                softness,
                angle,
                offset,
                primary,
                secondary,
                seed);
        }
    }

    internal static class TextStudio2DGraphicEffectUtility
    {
        internal static TextStudioVisualPrimitiveContract GetContract(
            TextStudio2DGraphicEffectKind kind)
        {
            return TextStudioVisualPrimitiveCatalog.TryGet(
                kind,
                out TextStudioVisualPrimitiveContract contract)
                    ? contract
                    : default;
        }

        internal static bool UsesNoise(TextStudio2DGraphicEffectKind kind)
        {
            return GetContract(kind).UsesProceduralNoise;
        }

        internal static bool UsesFieldNormal(TextStudio2DGraphicEffectKind kind)
        {
            return GetContract(kind).UsesFieldNormal;
        }

        internal static int OperationWeight(TextStudio2DGraphicEffectKind kind)
        {
            TextStudioVisualPrimitiveContract contract = GetContract(kind);
            return contract.IsDefined ? contract.OperationWeight : 3;
        }
    }

    internal enum TextStudio2DTreatmentDefinitionKind : byte
    {
        IrreduciblePrimitive = 0,
        CalibratedPreset = 1
    }

    internal readonly struct TextStudio2DTreatmentDefinition
    {
        internal TextStudio2DTreatmentDefinitionKind DefinitionKind { get; }
        internal TextStudioVisualPrimitiveContract Primitive { get; }

        internal TextStudio2DTreatmentDefinition(
            TextStudio2DTreatmentDefinitionKind definitionKind,
            in TextStudioVisualPrimitiveContract primitive)
        {
            DefinitionKind = definitionKind;
            Primitive = primitive;
        }
    }

    /// <summary>
    /// Classifies named 2D treatments without changing their authoring workflows. Editorial and
    /// animated kinds are irreducible algorithms; material families are calibrated presets over
    /// one shared surface-response primitive.
    /// </summary>
    internal static class TextStudio2DTreatmentCatalog
    {
        internal static bool TryGet(
            TextStudio2DGraphicEffectKind kind,
            out TextStudio2DTreatmentDefinition definition)
        {
            if (TextStudioVisualPrimitiveCatalog.TryGet(
                    kind,
                    out TextStudioVisualPrimitiveContract primitive))
            {
                definition = new TextStudio2DTreatmentDefinition(
                    TextStudio2DTreatmentDefinitionKind.IrreduciblePrimitive,
                    in primitive);
                return true;
            }
            definition = default;
            return false;
        }

        internal static bool TryGet(
            TextStudio2DAnimatedEffectKind kind,
            out TextStudio2DTreatmentDefinition definition)
        {
            if (TextStudioVisualPrimitiveCatalog.TryGet(
                    kind,
                    out TextStudioVisualPrimitiveContract primitive))
            {
                definition = new TextStudio2DTreatmentDefinition(
                    TextStudio2DTreatmentDefinitionKind.IrreduciblePrimitive,
                    in primitive);
                return true;
            }
            definition = default;
            return false;
        }

        internal static bool TryGet(
            TextStudio2DMaterialResponseKind kind,
            out TextStudio2DTreatmentDefinition definition)
        {
            if ((uint)kind >
                (uint)TextStudio2DMaterialResponseKind.HolographicFoil)
            {
                definition = default;
                return false;
            }
            TextStudioVisualPrimitiveContract primitive =
                TextStudioVisualPrimitiveCatalog.GetMaterialResponse();
            definition = new TextStudio2DTreatmentDefinition(
                TextStudio2DTreatmentDefinitionKind.CalibratedPreset,
                in primitive);
            return true;
        }
    }
}
