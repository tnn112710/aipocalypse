using System.Runtime.CompilerServices;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Flags the evaluated glyph transform, geometry, and surface channels a renderer needs to consume.</summary>
    [System.Flags]
    public enum TextStudioRenderChannels : ushort
    {
        None = 0,
        Position = 1 << 0,
        Rotation = 1 << 1,
        Scale = 1 << 2,
        Alpha = 1 << 3,
        Color = 1 << 4,
        Skew = 1 << 5,
        Pivot = 1 << 6,
        CornerOffsets = 1 << 7,
        UvOffset = 1 << 8,
        Transform = Position | Rotation | Scale | Skew | Pivot,
        Geometry = CornerOffsets,
        Surface = Alpha | Color | UvOffset,
        Vertex = Transform | Geometry,
        VertexColor = Alpha | Color,
        All = Vertex | Surface
    }

    /// <summary>
    /// Independent 2D offsets for the four vertices of a glyph quad.
    /// Values use Text Studio effect units and are applied before the glyph transform.
    /// </summary>
    public readonly struct TextStudioCornerOffsets
    {
        public static readonly TextStudioCornerOffsets Zero = new TextStudioCornerOffsets(
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero);

        public Vector2 BottomLeft { get; }
        public Vector2 TopLeft { get; }
        public Vector2 TopRight { get; }
        public Vector2 BottomRight { get; }

        /// <summary>Creates canonical bottom-left, top-left, top-right, and bottom-right offsets in effect units.</summary>
        public TextStudioCornerOffsets(
            Vector2 bottomLeft,
            Vector2 topLeft,
            Vector2 topRight,
            Vector2 bottomRight)
        {
            BottomLeft = bottomLeft;
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
        }

        /// <summary>Gets an offset using canonical TMP quad vertex order, or zero for an invalid index.</summary>
        public Vector2 GetCanonicalVertexOffset(int vertexIndex)
        {
            return vertexIndex switch
            {
                0 => BottomLeft,
                1 => TopLeft,
                2 => TopRight,
                3 => BottomRight,
                _ => Vector2.zero
            };
        }

        internal bool HasAny =>
            BottomLeft.sqrMagnitude > 0.000001f ||
            TopLeft.sqrMagnitude > 0.000001f ||
            TopRight.sqrMagnitude > 0.000001f ||
            BottomRight.sqrMagnitude > 0.000001f;

        /// <summary>Adds corresponding corner offsets.</summary>
        public static TextStudioCornerOffsets Add(
            in TextStudioCornerOffsets a,
            in TextStudioCornerOffsets b)
        {
            return new TextStudioCornerOffsets(
                a.BottomLeft + b.BottomLeft,
                a.TopLeft + b.TopLeft,
                a.TopRight + b.TopRight,
                a.BottomRight + b.BottomRight);
        }

        /// <summary>Multiplies every corner offset by one scalar.</summary>
        public static TextStudioCornerOffsets Scale(in TextStudioCornerOffsets value, float scale)
        {
            return new TextStudioCornerOffsets(
                value.BottomLeft * scale,
                value.TopLeft * scale,
                value.TopRight * scale,
                value.BottomRight * scale);
        }

        /// <summary>Multiplies corresponding corner offset components.</summary>
        public static TextStudioCornerOffsets Multiply(
            in TextStudioCornerOffsets a,
            in TextStudioCornerOffsets b)
        {
            return new TextStudioCornerOffsets(
                Vector2.Scale(a.BottomLeft, b.BottomLeft),
                Vector2.Scale(a.TopLeft, b.TopLeft),
                Vector2.Scale(a.TopRight, b.TopRight),
                Vector2.Scale(a.BottomRight, b.BottomRight));
        }

        /// <summary>Interpolates corresponding corners with a clamped normalized amount.</summary>
        public static TextStudioCornerOffsets Lerp(
            in TextStudioCornerOffsets a,
            in TextStudioCornerOffsets b,
            float t)
        {
            return new TextStudioCornerOffsets(
                Vector2.LerpUnclamped(a.BottomLeft, b.BottomLeft, t),
                Vector2.LerpUnclamped(a.TopLeft, b.TopLeft, t),
                Vector2.LerpUnclamped(a.TopRight, b.TopRight, t),
                Vector2.LerpUnclamped(a.BottomRight, b.BottomRight, t));
        }

        internal TextStudioCornerOffsets FlipVerticalAxis()
        {
            return new TextStudioCornerOffsets(
                new Vector2(BottomLeft.x, -BottomLeft.y),
                new Vector2(TopLeft.x, -TopLeft.y),
                new Vector2(TopRight.x, -TopRight.y),
                new Vector2(BottomRight.x, -BottomRight.y));
        }
    }

    /// <summary>
    /// Per-glyph evaluated animation state. Output of the animation evaluation pipeline.
    /// 8 channels with per-channel blend mode support.
    /// </summary>
    public struct EvaluatedGlyphState
    {
        public Vector3 positionOffset;
        public Quaternion rotation;
        public Vector3 scale;
        public float alpha;
        public Color32 colorOverride;
        public TextStudioColorBlendMode colorBlendMode;
        public float colorBlendWeight;
        public Color colorMultiplier;
        public Color colorAdditive;
        public Vector2 uvOffset;
        public Vector2 skew;
        public Vector2 pivotOffset;
        public TextStudioCornerOffsets cornerOffsets;

        public AnimationPhase currentPhase;
        public float phaseProgress;

        internal uint colorCompositionVersion;
        internal TextStudioRenderChannels renderChannels;

        public Vector2 Pivot
        {
            get => pivotOffset + new Vector2(0.5f, 0.5f);
            set => pivotOffset = value - new Vector2(0.5f, 0.5f);
        }

        public static readonly EvaluatedGlyphState Identity = new EvaluatedGlyphState
        {
            positionOffset = Vector3.zero,
            rotation = Quaternion.identity,
            scale = Vector3.one,
            alpha = 1f,
            colorOverride = new Color32(0, 0, 0, 0),
            colorBlendMode = TextStudioColorBlendMode.Set,
            colorBlendWeight = 0f,
            colorMultiplier = Color.white,
            colorAdditive = Color.clear,
            uvOffset = Vector2.zero,
            skew = Vector2.zero,
            pivotOffset = Vector2.zero,
            cornerOffsets = TextStudioCornerOffsets.Zero,
            currentPhase = AnimationPhase.None,
            phaseProgress = 0f
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool HasSameRenderedValues(in EvaluatedGlyphState other)
        {
            return positionOffset.Equals(other.positionOffset) &&
                   rotation.Equals(other.rotation) &&
                   scale.Equals(other.scale) &&
                   alpha.Equals(other.alpha) &&
                   colorOverride.Equals(other.colorOverride) &&
                   colorBlendMode == other.colorBlendMode &&
                   colorBlendWeight.Equals(other.colorBlendWeight) &&
                   colorMultiplier.Equals(other.colorMultiplier) &&
                   colorAdditive.Equals(other.colorAdditive) &&
                   uvOffset.Equals(other.uvOffset) &&
                   skew.Equals(other.skew) &&
                   pivotOffset.Equals(other.pivotOffset) &&
                   cornerOffsets.BottomLeft.Equals(other.cornerOffsets.BottomLeft) &&
                   cornerOffsets.TopLeft.Equals(other.cornerOffsets.TopLeft) &&
                   cornerOffsets.TopRight.Equals(other.cornerOffsets.TopRight) &&
                   cornerOffsets.BottomRight.Equals(other.cornerOffsets.BottomRight) &&
                   colorCompositionVersion == other.colorCompositionVersion &&
                   renderChannels == other.renderChannels;
        }

        /// <summary>
        /// Linearly interpolate between two states.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvaluatedGlyphState Lerp(EvaluatedGlyphState a, EvaluatedGlyphState b, float t)
        {
            return new EvaluatedGlyphState
            {
                positionOffset = Vector3.Lerp(a.positionOffset, b.positionOffset, t),
                rotation = Quaternion.Slerp(a.rotation, b.rotation, t),
                scale = Vector3.Lerp(a.scale, b.scale, t),
                alpha = Mathf.Lerp(a.alpha, b.alpha, t),
                colorOverride = Color32.Lerp(a.colorOverride, b.colorOverride, t),
                colorBlendMode = t < 0.5f ? a.colorBlendMode : b.colorBlendMode,
                colorBlendWeight = Mathf.Lerp(a.colorBlendWeight, b.colorBlendWeight, t),
                colorMultiplier = Color.LerpUnclamped(a.colorMultiplier, b.colorMultiplier, t),
                colorAdditive = Color.LerpUnclamped(a.colorAdditive, b.colorAdditive, t),
                uvOffset = Vector2.Lerp(a.uvOffset, b.uvOffset, t),
                skew = Vector2.Lerp(a.skew, b.skew, t),
                pivotOffset = Vector2.Lerp(a.pivotOffset, b.pivotOffset, t),
                cornerOffsets = TextStudioCornerOffsets.Lerp(in a.cornerOffsets, in b.cornerOffsets, t),
                currentPhase = b.currentPhase,
                phaseProgress = Mathf.Lerp(a.phaseProgress, b.phaseProgress, t),
                renderChannels = t <= 0f
                    ? a.renderChannels
                    : (t >= 1f ? b.renderChannels : a.renderChannels | b.renderChannels),
                colorCompositionVersion = a.colorCompositionVersion == b.colorCompositionVersion
                    ? a.colorCompositionVersion
                    : unchecked(System.Math.Max(a.colorCompositionVersion, b.colorCompositionVersion) + 1u)
            };
        }

        /// <summary>
        /// Composes a color operation with all color effects evaluated before it.
        /// RGB composition never increases glyph visibility; target alpha multiplies glyph alpha.
        /// </summary>
        public void BlendColor(Color32 target, TextStudioColorBlendMode mode, float weight)
        {
            renderChannels |= TextStudioRenderChannels.Color;
            weight = Mathf.Clamp01(weight);
            EnsureColorCompositionInitialized();
            Color targetColor = target;
            if (mode == TextStudioColorBlendMode.Set && weight >= 0.999999f)
            {
                alpha *= targetColor.a;
                colorMultiplier = new Color(0f, 0f, 0f, 1f);
                colorAdditive = new Color(targetColor.r, targetColor.g, targetColor.b, 0f);
                colorOverride = target;
                colorBlendMode = mode;
                colorBlendWeight = 1f;
                colorCompositionVersion++;
                return;
            }

            switch (mode)
            {
                case TextStudioColorBlendMode.AlphaOnly:
                    alpha *= Mathf.Lerp(1f, targetColor.a, weight);
                    break;
                case TextStudioColorBlendMode.Multiply:
                {
                    Color factor = Color.Lerp(Color.white, targetColor, weight);
                    alpha *= Mathf.Lerp(1f, targetColor.a, weight);
                    factor.a = 1f;
                    colorMultiplier = MultiplyRgb(colorMultiplier, factor);
                    colorAdditive = MultiplyRgb(colorAdditive, factor);
                    break;
                }
                case TextStudioColorBlendMode.Set:
                default:
                {
                    alpha *= Mathf.Lerp(1f, targetColor.a, weight);
                    float retained = 1f - weight;
                    colorMultiplier = new Color(
                        colorMultiplier.r * retained,
                        colorMultiplier.g * retained,
                        colorMultiplier.b * retained,
                        1f);
                    colorAdditive = new Color(
                        colorAdditive.r * retained + targetColor.r * weight,
                        colorAdditive.g * retained + targetColor.g * weight,
                        colorAdditive.b * retained + targetColor.b * weight,
                        0f);
                    break;
                }
            }

            colorOverride = target;
            colorBlendMode = mode;
            colorBlendWeight = weight;
            colorCompositionVersion++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetColor(Color32 target)
        {
            renderChannels |= TextStudioRenderChannels.Color;
            EnsureColorCompositionInitialized();
            alpha *= target.a / 255f;
            colorMultiplier = new Color(0f, 0f, 0f, 1f);
            colorAdditive = new Color(
                target.r / 255f,
                target.g / 255f,
                target.b / 255f,
                0f);
            colorOverride = target;
            colorBlendMode = TextStudioColorBlendMode.Set;
            colorBlendWeight = 1f;
            colorCompositionVersion++;
        }

        /// <summary>Applies the accumulated RGB transform to a renderer's source vertex color.</summary>
        public Color32 ResolveColor(Color32 source)
        {
            if (colorCompositionVersion != 0u &&
                colorBlendMode == TextStudioColorBlendMode.Set &&
                colorBlendWeight >= 0.999999f)
            {
                Color32 resolvedOverride = colorOverride;
                resolvedOverride.a = source.a;
                return resolvedOverride;
            }

            if (colorCompositionVersion == 0u)
                return source;

            Color sourceColor = source;
            Color resolved = new Color(
                sourceColor.r * colorMultiplier.r + colorAdditive.r,
                sourceColor.g * colorMultiplier.g + colorAdditive.g,
                sourceColor.b * colorMultiplier.b + colorAdditive.b,
                sourceColor.a);
            resolved.r = Mathf.Clamp01(resolved.r);
            resolved.g = Mathf.Clamp01(resolved.g);
            resolved.b = Mathf.Clamp01(resolved.b);
            return (Color32)resolved;
        }

        void EnsureColorCompositionInitialized()
        {
            if (colorCompositionVersion != 0u)
                return;
            colorMultiplier = Color.white;
            colorAdditive = Color.clear;
        }

        static Color MultiplyRgb(Color a, Color b)
        {
            return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a);
        }
    }
}
