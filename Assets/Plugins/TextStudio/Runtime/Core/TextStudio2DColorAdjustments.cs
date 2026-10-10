using System;
using UnityEngine;

namespace TextStudio
{
    public enum TextStudio2DColorAdjustmentMode : byte { Matrix, Standard }
    public enum TextStudio2DSurfaceSelection : byte { WholeSurface, ExcludePartial }

    public partial struct TextStudio2DColorAdjustmentSpec
    {
        [SerializeField] TextStudio2DColorAdjustmentMode _mode;
        [SerializeField, Range(0f, 3f)] float _saturation;
        [SerializeField, Range(-180f, 180f)] float _hueDegrees;
        [SerializeField, Range(-10f, 10f)] float _exposure;
        [SerializeField, Range(-1f, 1f)] float _brightness;
        [SerializeField, Range(0f, 3f)] float _contrast;
        [SerializeField, Range(0f, 1f)] float _contrastPivot;
        [SerializeField] Color _tint;
        [SerializeField, Range(0f, 1f)] float _grayscale;
        [SerializeField, Range(0f, 1f)] float _sepia;
        [SerializeField, Range(0f, 1f)] float _invert;
        [SerializeField] TextStudio2DSurfaceSelection _surfaceSelection;

        public TextStudio2DColorAdjustmentMode Mode => _mode;
        public TextStudio2DSurfaceSelection SurfaceSelection => _surfaceSelection;
        internal bool RequiresIndependentCoverage
        {
            get
            {
                TextStudio2DColorAdjustmentSpec value = Resolve();
                return value.Alpha != new Vector4(0, 0, 0, 1) || value.Offset.w != 0 ||
                       value.Red.w != 0 || value.Green.w != 0 || value.Blue.w != 0;
            }
        }
        internal bool AreParametersValid => (uint)_mode <= (uint)TextStudio2DColorAdjustmentMode.Standard &&
            (uint)_surfaceSelection <= (uint)TextStudio2DSurfaceSelection.ExcludePartial &&
            InRange(_saturation, 0, 3) && InRange(_hueDegrees, -180, 180) && InRange(_exposure, -10, 10) &&
            InRange(_brightness, -1, 1) && InRange(_contrast, 0, 3) && InRange(_contrastPivot, 0, 1) &&
            InRange(_grayscale, 0, 1) && InRange(_sepia, 0, 1) && InRange(_invert, 0, 1) && InRange(_strength, 0, 1) &&
            float.IsFinite(_tint.r) && float.IsFinite(_tint.g) && float.IsFinite(_tint.b) && float.IsFinite(_tint.a) &&
            Finite(_red) && Finite(_green) && Finite(_blue) && Finite(_alpha) && Finite(_offset);

        static bool InRange(float value, float minimum, float maximum) => float.IsFinite(value) && value >= minimum && value <= maximum;
        static bool Finite(Vector4 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z) && float.IsFinite(value.w);

        /// <summary>Named controls are evaluated in linear light. Contrast uses an explicit linear pivot.</summary>
        public static TextStudio2DColorAdjustmentSpec Adjust(float saturation = 1f, float hueDegrees = 0f, float exposure = 0f,
            float brightness = 0f, float contrast = 1f, Color? tint = null, float grayscale = 0f, float sepia = 0f,
            float invert = 0f, float contrastPivot = .18f, float strength = 1f, bool clampOutput = false)
        {
            return new TextStudio2DColorAdjustmentSpec
            {
                _mode = TextStudio2DColorAdjustmentMode.Standard,
                _saturation = saturation,
                _hueDegrees = hueDegrees,
                _exposure = exposure,
                _brightness = brightness,
                _contrast = contrast,
                _contrastPivot = contrastPivot,
                _tint = tint ?? Color.white,
                _grayscale = grayscale,
                _sepia = sepia,
                _invert = invert,
                _strength = strength,
                _clampOutput = clampOutput
            };
        }

        /// <summary>Returns a color-adjustment specification copy targeting the supplied surface selection.</summary>
        public TextStudio2DColorAdjustmentSpec WithSurfaceSelection(TextStudio2DSurfaceSelection selection)
        {
            TextStudio2DColorAdjustmentSpec value = this;
            value._surfaceSelection = selection;
            return value;
        }

        /// <summary>Resolves authored color adjustments into a matrix transform while preserving strength, clamping, and surface selection.</summary>
        public TextStudio2DColorAdjustmentSpec Resolve()
        {
            if (_mode == TextStudio2DColorAdjustmentMode.Matrix) return this;
            Matrix4x4 matrix = Matrix4x4.identity;
            Vector4 offset = Vector4.zero;
            float exposure = Mathf.Pow(2f, _exposure);
            matrix.m00 = matrix.m11 = matrix.m22 = exposure * _contrast;
            float bias = _brightness * _contrast + _contrastPivot * (1f - _contrast);
            offset.x = offset.y = offset.z = bias;

            Append(ref matrix, ref offset, SaturationMatrix(_saturation), Vector4.zero);
            float angle = _hueDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
            Matrix4x4 hue = Matrix4x4.identity;
            hue.SetRow(0, new Vector4(.2126f + .7874f * cosine - .2126f * sine, .7152f - .7152f * cosine - .7152f * sine, .0722f - .0722f * cosine + .9278f * sine, 0));
            hue.SetRow(1, new Vector4(.2126f - .2126f * cosine + .143f * sine, .7152f + .2848f * cosine + .14f * sine, .0722f - .0722f * cosine - .283f * sine, 0));
            hue.SetRow(2, new Vector4(.2126f - .2126f * cosine - .7874f * sine, .7152f - .7152f * cosine + .7152f * sine, .0722f + .9278f * cosine + .0722f * sine, 0));
            Append(ref matrix, ref offset, hue, Vector4.zero);
            Matrix4x4 tint = Matrix4x4.identity;
            tint.m00 = _tint.r; tint.m11 = _tint.g; tint.m22 = _tint.b; tint.m33 = _tint.a;
            Append(ref matrix, ref offset, tint, Vector4.zero);
            Append(ref matrix, ref offset, SaturationMatrix(1f - _grayscale), Vector4.zero);
            Matrix4x4 sepia = Matrix4x4.identity;
            sepia.SetRow(0, Vector4.LerpUnclamped(sepia.GetRow(0), new Vector4(.393f, .769f, .189f, 0), _sepia));
            sepia.SetRow(1, Vector4.LerpUnclamped(sepia.GetRow(1), new Vector4(.349f, .686f, .168f, 0), _sepia));
            sepia.SetRow(2, Vector4.LerpUnclamped(sepia.GetRow(2), new Vector4(.272f, .534f, .131f, 0), _sepia));
            Append(ref matrix, ref offset, sepia, Vector4.zero);
            Matrix4x4 invert = Matrix4x4.identity;
            invert.m00 = invert.m11 = invert.m22 = 1f - 2f * _invert;
            Append(ref matrix, ref offset, invert, new Vector4(_invert, _invert, _invert, 0));
            return new TextStudio2DColorAdjustmentSpec(matrix.GetRow(0), matrix.GetRow(1), matrix.GetRow(2), matrix.GetRow(3), offset, _strength, _clampOutput)
                .WithSurfaceSelection(_surfaceSelection);
        }

        public bool IsNeutral
        {
            get
            {
                if (!AreParametersValid) return false;
                TextStudio2DColorAdjustmentSpec value = Resolve();
                return value.Strength == 0 || !value.ClampOutput && value._red == new Vector4(1, 0, 0, 0) &&
                    value._green == new Vector4(0, 1, 0, 0) && value._blue == new Vector4(0, 0, 1, 0) &&
                    value._alpha == new Vector4(0, 0, 0, 1) && value._offset == Vector4.zero;
            }
        }

        /// <summary>CPU reference for the GPU contract. Input and output colors use straight linear RGBA.</summary>
        public Color Apply(Color straight)
        {
            TextStudio2DColorAdjustmentSpec value = Resolve();
            Vector4 color = straight;
            Vector4 transformed = new Vector4(Vector4.Dot(value._red, color), Vector4.Dot(value._green, color),
                Vector4.Dot(value._blue, color), Vector4.Dot(value._alpha, color)) + value._offset;
            if (value._clampOutput)
                for (int channel = 0; channel < 4; channel++) transformed[channel] = Mathf.Clamp01(transformed[channel]);
            return Vector4.LerpUnclamped(color, transformed, Mathf.Clamp01(value._strength));
        }

        /// <summary>Transforms straight RGBA and returns a premultiplied contribution weighted by coverage and opacity.</summary>
        public Color ApplyContribution(Color straight, float coverage, float opacity = 1f)
        {
            if (coverage <= 0 || opacity <= 0) return Color.clear;
            Color adjusted = Apply(straight);
            float alpha = Mathf.Clamp01(adjusted.a) * Mathf.Clamp01(coverage) * Mathf.Clamp01(opacity);
            return new Color(adjusted.r * alpha, adjusted.g * alpha, adjusted.b * alpha, alpha);
        }

        static Matrix4x4 SaturationMatrix(float saturation)
        {
            Vector4 luminance = new Vector4(.2126f, .7152f, .0722f, 0);
            Matrix4x4 result = Matrix4x4.identity;
            for (int row = 0; row < 3; row++) result.SetRow(row, Vector4.LerpUnclamped(luminance, result.GetRow(row), saturation));
            return result;
        }

        static void Append(ref Matrix4x4 matrix, ref Vector4 offset, Matrix4x4 next, Vector4 nextOffset)
        {
            matrix = next * matrix;
            offset = next * offset + nextOffset;
        }
    }

    internal static class TextStudio2DColorAdjustmentUtility
    {
        internal static bool IsAdjustment(TextStudio2DLayerKind kind) => kind == TextStudio2DLayerKind.ColorAdjustment || kind == TextStudio2DLayerKind.CompositeAdjustment;

        internal static bool Affects(CompiledTextStudio2DStyle style, int adjustmentIndex, int contributionIndex)
        {
            if (contributionIndex <= adjustmentIndex) return false;
            ref readonly TextStudio2DCompiledOperation adjustment = ref style.OperationAt(adjustmentIndex);
            int parent = adjustment.ParentOperationIndex;
            int sibling = contributionIndex;
            for (int depth = 0; depth < style.OperationCount; depth++)
            {
                int ancestor = style.OperationAt(sibling).ParentOperationIndex;
                if (ancestor == parent) return sibling > adjustmentIndex;
                if (ancestor < 0) return false;
                sibling = ancestor;
            }
            return false;
        }

        internal static Vector4 ContributionMask(CompiledTextStudio2DStyle style, int adjustmentIndex)
        {
            Vector4 mask = Vector4.zero;
            for (int index = adjustmentIndex + 1; index < Mathf.Min(64, style.OperationCount); index++)
                if (Affects(style, adjustmentIndex, index)) mask[index >> 4] = (uint)mask[index >> 4] | 1u << (index & 15);
            return mask;
        }
    }
}
