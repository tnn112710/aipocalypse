using System.Runtime.CompilerServices;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Resolved transform for one glyph quad. Render backends use this to apply
    /// Text Studio placement, animation, pivot, color, and alpha consistently.
    /// </summary>
    public readonly struct TextStudioGlyphQuadTransform
    {
        const byte ScaleFlag = 1 << 0;
        const byte SkewFlag = 1 << 1;
        const byte RotationFlag = 1 << 2;
        const byte ZRotationFlag = 1 << 3;

        readonly Vector3 _center;
        readonly Quaternion _rotation;
        readonly Vector3 _scale;
        readonly Vector2 _skew;
        readonly Vector3 _translation;
        readonly Color32 _colorOverride;
        readonly TextStudioColorBlendMode _colorBlendMode;
        readonly float _colorBlendWeight;
        readonly Color _colorMultiplier;
        readonly Color _colorAdditive;
        readonly bool _useColorComposition;
        readonly byte _alpha;
        readonly byte _positionFlags;

        /// <summary>Creates a transform with translation, rotation, scale, color override, and alpha.</summary>
        public TextStudioGlyphQuadTransform(
            Vector3 center,
            Quaternion rotation,
            Vector3 scale,
            Vector3 translation,
            Color32 colorOverride,
            byte alpha)
            : this(
                center,
                rotation,
                scale,
                Vector2.zero,
                translation,
                colorOverride,
                TextStudioColorBlendMode.Set,
                0f,
                alpha)
        {
        }

        /// <summary>Creates a transform including skew and explicit color blending.</summary>
        public TextStudioGlyphQuadTransform(
            Vector3 center,
            Quaternion rotation,
            Vector3 scale,
            Vector2 skew,
            Vector3 translation,
            Color32 colorOverride,
            TextStudioColorBlendMode colorBlendMode,
            float colorBlendWeight,
            byte alpha)
        {
            _center = center;
            _rotation = rotation;
            _scale = scale;
            _skew = skew;
            _translation = translation;
            _colorOverride = colorOverride;
            _colorBlendMode = colorBlendMode;
            _colorBlendWeight = Mathf.Clamp01(colorBlendWeight);
            _colorMultiplier = Color.white;
            _colorAdditive = Color.clear;
            _useColorComposition = false;
            _alpha = alpha;
            _positionFlags = ResolvePositionFlags(rotation, scale, skew);
        }

        internal TextStudioGlyphQuadTransform(
            Vector3 center,
            Quaternion rotation,
            Vector3 scale,
            Vector2 skew,
            Vector3 translation,
            Color colorMultiplier,
            Color colorAdditive,
            byte alpha)
        {
            _center = center;
            _rotation = rotation;
            _scale = scale;
            _skew = skew;
            _translation = translation;
            _colorOverride = default;
            _colorBlendMode = TextStudioColorBlendMode.Set;
            _colorBlendWeight = 0f;
            _colorMultiplier = colorMultiplier;
            _colorAdditive = colorAdditive;
            _useColorComposition = true;
            _alpha = alpha;
            _positionFlags = ResolvePositionFlags(rotation, scale, skew);
        }

        /// <summary>Applies pivoted scale, skew, rotation, and translation to one source vertex.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ApplyPosition(Vector3 source)
        {
            if ((_positionFlags & (ScaleFlag | SkewFlag | RotationFlag)) == 0)
                return source + _translation;

            Vector3 offset = source - _center;
            if ((_positionFlags & ScaleFlag) != 0)
                offset = Vector3.Scale(offset, _scale);
            if ((_positionFlags & SkewFlag) != 0)
            {
                float x = offset.x;
                float y = offset.y;
                offset.x = x + y * _skew.x;
                offset.y = y + x * _skew.y;
            }

            if ((_positionFlags & RotationFlag) != 0)
            {
                if ((_positionFlags & ZRotationFlag) != 0)
                {
                    float doubledZ = 2f * _rotation.z;
                    float diagonal = 1f - doubledZ * _rotation.z;
                    float cross = doubledZ * _rotation.w;
                    float x = offset.x;
                    float y = offset.y;
                    offset.x = diagonal * x - cross * y;
                    offset.y = cross * x + diagonal * y;
                }
                else
                {
                    offset = _rotation * offset;
                }
            }

            return _center + offset + _translation;
        }

        /// <summary>Applies a per-corner effect-unit deformation before the glyph transform.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ApplyPosition(Vector3 source, Vector2 cornerOffset, float effectUnitScale)
        {
            if (cornerOffset.sqrMagnitude <= 0.000001f)
                return ApplyPosition(source);

            Vector3 deformed = source + new Vector3(
                cornerOffset.x * effectUnitScale,
                cornerOffset.y * effectUnitScale,
                0f);
            return ApplyPosition(deformed);
        }

        static byte ResolvePositionFlags(Quaternion rotation, Vector3 scale, Vector2 skew)
        {
            byte flags = 0;
            if (scale.x != 1f || scale.y != 1f || scale.z != 1f)
                flags |= ScaleFlag;
            if (skew.sqrMagnitude > 0.000001f)
                flags |= SkewFlag;
            if (Mathf.Abs(rotation.x) > 0.000001f ||
                Mathf.Abs(rotation.y) > 0.000001f ||
                Mathf.Abs(rotation.z) > 0.000001f ||
                Mathf.Abs(rotation.w - 1f) > 0.000001f)
            {
                flags |= RotationFlag;
                if (Mathf.Abs(rotation.x) <= 0.000001f && Mathf.Abs(rotation.y) <= 0.000001f)
                    flags |= ZRotationFlag;
            }

            return flags;
        }

        /// <summary>Applies evaluated alpha and sequential color composition to one source color.</summary>
        public Color32 ApplyColor(Color32 source)
        {
            Color32 color = source;
            color.a = MultiplyByte(source.a, _alpha);

            if (_useColorComposition)
            {
                if (_colorMultiplier.r == 0f &&
                    _colorMultiplier.g == 0f &&
                    _colorMultiplier.b == 0f)
                {
                    Color32 direct = _colorAdditive;
                    color.r = direct.r;
                    color.g = direct.g;
                    color.b = direct.b;
                    return color;
                }

                Color sourceColor = color;
                Color resolved = new Color(
                    sourceColor.r * _colorMultiplier.r + _colorAdditive.r,
                    sourceColor.g * _colorMultiplier.g + _colorAdditive.g,
                    sourceColor.b * _colorMultiplier.b + _colorAdditive.b,
                    sourceColor.a);
                resolved.r = Mathf.Clamp01(resolved.r);
                resolved.g = Mathf.Clamp01(resolved.g);
                resolved.b = Mathf.Clamp01(resolved.b);
                color = (Color32)resolved;
            }
            else if (_colorBlendWeight > 0f)
            {
                ApplyColorBlend(ref color);
            }
            else if (_colorOverride.a > 0)
            {
                color.r = _colorOverride.r;
                color.g = _colorOverride.g;
                color.b = _colorOverride.b;
            }

            return color;
        }

        void ApplyColorBlend(ref Color32 color)
        {
            float weight = Mathf.Clamp01(_colorBlendWeight);
            switch (_colorBlendMode)
            {
                case TextStudioColorBlendMode.AlphaOnly:
                    color.a = LerpByte(color.a, _colorOverride.a, weight);
                    break;
                case TextStudioColorBlendMode.Multiply:
                    Color32 multiplied = new Color32(
                        MultiplyByte(color.r, _colorOverride.r),
                        MultiplyByte(color.g, _colorOverride.g),
                        MultiplyByte(color.b, _colorOverride.b),
                        MultiplyByte(color.a, _colorOverride.a));
                    color.r = LerpByte(color.r, multiplied.r, weight);
                    color.g = LerpByte(color.g, multiplied.g, weight);
                    color.b = LerpByte(color.b, multiplied.b, weight);
                    color.a = LerpByte(color.a, multiplied.a, weight);
                    break;
                default:
                    color.r = LerpByte(color.r, _colorOverride.r, weight);
                    color.g = LerpByte(color.g, _colorOverride.g, weight);
                    color.b = LerpByte(color.b, _colorOverride.b, weight);
                    color.a = LerpByte(color.a, _colorOverride.a, weight);
                    break;
            }
        }

        static byte MultiplyByte(byte source, byte target)
        {
            return (byte)((source * target) / 255);
        }

        static byte LerpByte(byte source, byte target, float weight)
        {
            return (byte)Mathf.Clamp(
                Mathf.FloorToInt(source + (target - source) * Mathf.Clamp01(weight)),
                0,
                255);
        }
    }

    /// <summary>
    /// Shared quad-space transform utility for TMP-style render backends.
    /// </summary>
    public static class TextStudioGlyphQuadTransformer
    {
        /// <summary>Builds a renderer transform from evaluated glyph state and optional placement.</summary>
        public static TextStudioGlyphQuadTransform Create(
            Vector3 sourceCenter,
            in EvaluatedGlyphState state,
            in GlyphPlacement placement,
            bool usePlacement,
            float effectUnitScale,
            float alphaMultiplier)
        {
            return Create(
                sourceCenter,
                sourceCenter,
                in state,
                in placement,
                usePlacement,
                effectUnitScale,
                alphaMultiplier);
        }

        /// <summary>Builds a renderer transform using complete source quad geometry for custom pivot resolution.</summary>
        public static TextStudioGlyphQuadTransform CreateFromQuad(
            Vector3 sourceVertex0,
            Vector3 sourceVertex1,
            Vector3 sourceVertex2,
            Vector3 sourceVertex3,
            Vector3 sourceCenter,
            in EvaluatedGlyphState state,
            in GlyphPlacement placement,
            bool usePlacement,
            float effectUnitScale,
            float alphaMultiplier)
        {
            Vector3 pivotCenter = ResolvePivotCenter(
                sourceVertex0,
                sourceVertex1,
                sourceVertex2,
                sourceVertex3,
                sourceCenter,
                in state);

            return Create(
                sourceCenter,
                pivotCenter,
                in state,
                in placement,
                usePlacement,
                effectUnitScale,
                alphaMultiplier);
        }

        static TextStudioGlyphQuadTransform Create(
            Vector3 sourceCenter,
            Vector3 pivotCenter,
            in EvaluatedGlyphState state,
            in GlyphPlacement placement,
            bool usePlacement,
            float effectUnitScale,
            float alphaMultiplier)
        {
            Vector3 placementDelta = Vector3.zero;
            Quaternion placementRotation = Quaternion.identity;
            Vector3 placementScale = Vector3.one;
            if (usePlacement)
            {
                placementRotation = placement.rotation;
                placementScale = placement.scale;
                Vector3 sourcePivotOffset = pivotCenter - sourceCenter;
                Vector3 placedPivotOffset = placementRotation *
                    Vector3.Scale(placementScale, sourcePivotOffset);
                placementDelta = placement.position + placedPivotOffset - pivotCenter;
            }

            Quaternion combinedRotation = placementRotation * state.rotation;
            Vector3 combinedScale = Vector3.Scale(placementScale, state.scale);
            Vector3 localEffectOffset = Vector3.Scale(
                placementScale,
                state.positionOffset * effectUnitScale);
            Vector3 translation = placementDelta + placementRotation * localEffectOffset;
            byte alpha = (byte)(Mathf.Clamp01(state.alpha * alphaMultiplier) * 255);

            return new TextStudioGlyphQuadTransform(
                pivotCenter,
                combinedRotation,
                combinedScale,
                state.skew,
                translation,
                state.colorMultiplier,
                state.colorAdditive,
                alpha);
        }

        static Vector3 ResolvePivotCenter(
            Vector3 sourceVertex0,
            Vector3 sourceVertex1,
            Vector3 sourceVertex2,
            Vector3 sourceVertex3,
            Vector3 sourceCenter,
            in EvaluatedGlyphState state)
        {
            if (state.pivotOffset.sqrMagnitude <= 0.000001f)
                return sourceCenter;

            float minX = Mathf.Min(Mathf.Min(sourceVertex0.x, sourceVertex1.x), Mathf.Min(sourceVertex2.x, sourceVertex3.x));
            float maxX = Mathf.Max(Mathf.Max(sourceVertex0.x, sourceVertex1.x), Mathf.Max(sourceVertex2.x, sourceVertex3.x));
            float minY = Mathf.Min(Mathf.Min(sourceVertex0.y, sourceVertex1.y), Mathf.Min(sourceVertex2.y, sourceVertex3.y));
            float maxY = Mathf.Max(Mathf.Max(sourceVertex0.y, sourceVertex1.y), Mathf.Max(sourceVertex2.y, sourceVertex3.y));
            Vector2 pivot = state.Pivot;
            return new Vector3(
                Mathf.LerpUnclamped(minX, maxX, pivot.x),
                Mathf.LerpUnclamped(minY, maxY, pivot.y),
                sourceCenter.z);
        }

        /// <summary>Transforms a bounded source vertex and color range into caller-owned destination buffers.</summary>
        public static void Apply(
            Vector3[] sourceVertices,
            int sourceVertexIndex,
            Color32[] sourceColors,
            int sourceColorIndex,
            Vector3 sourceCenter,
            in EvaluatedGlyphState state,
            in GlyphPlacement placement,
            bool usePlacement,
            float effectUnitScale,
            float alphaMultiplier,
            Vector3[] destinationVertices,
            int destinationVertexIndex,
            Color32[] destinationColors,
            int destinationColorIndex)
        {
            TextStudioGlyphQuadTransform transform = CreateFromQuad(
                sourceVertices[sourceVertexIndex],
                sourceVertices[sourceVertexIndex + 1],
                sourceVertices[sourceVertexIndex + 2],
                sourceVertices[sourceVertexIndex + 3],
                sourceCenter,
                in state,
                in placement,
                usePlacement,
                effectUnitScale,
                alphaMultiplier);

            for (int i = 0; i < 4; i++)
            {
                destinationVertices[destinationVertexIndex + i] =
                    transform.ApplyPosition(
                        sourceVertices[sourceVertexIndex + i],
                        state.cornerOffsets.GetCanonicalVertexOffset(i),
                        effectUnitScale);
                destinationColors[destinationColorIndex + i] =
                    transform.ApplyColor(sourceColors[sourceColorIndex + i]);
            }
        }
    }
}
