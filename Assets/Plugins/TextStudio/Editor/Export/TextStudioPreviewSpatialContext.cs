using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TextStudio.Editor
{
    internal readonly struct TextStudioPreviewSpatialTarget
    {
        internal readonly Vector3 LocalPosition;
        internal readonly Vector3 SourceTextNormalizedPosition;
        internal readonly Vector3 WorldPosition;

        internal TextStudioPreviewSpatialTarget(
            Vector3 localPosition,
            Vector3 sourceTextNormalizedPosition,
            Vector3 worldPosition)
        {
            LocalPosition = localPosition;
            SourceTextNormalizedPosition = sourceTextNormalizedPosition;
            WorldPosition = worldPosition;
        }
    }

    internal readonly struct TextStudioPreviewSpatialEvaluationContext
    {
        internal readonly Vector3[] SourcePositions;
        internal readonly TextStudioGlyphVisualBounds[] SourceVisualBounds;
        internal readonly Vector3[] SourceTextNormalizedPositions;
        internal readonly TextStudioGlyphColorQuad[] SourceColors;
        internal readonly bool[] SourceColorValidity;
        internal readonly Vector4 SourceTextBounds;
        internal readonly int GlyphCount;
        internal readonly Matrix4x4 LocalToWorld;
        internal readonly Vector3[] InteractiveSourcePositions;
        internal readonly Matrix4x4 InteractiveLocalToWorld;
        internal readonly float InteractiveLocalSpatialScale;
        internal readonly bool HasInfluenceTarget;
        internal readonly TextStudioPreviewSpatialTarget InfluenceTarget;

        internal TextStudioPreviewSpatialEvaluationContext(
            Vector3[] sourcePositions,
            TextStudioGlyphVisualBounds[] sourceVisualBounds,
            Vector3[] sourceTextNormalizedPositions,
            TextStudioGlyphColorQuad[] sourceColors,
            bool[] sourceColorValidity,
            Vector4 sourceTextBounds,
            int glyphCount,
            Matrix4x4 localToWorld,
            Vector3[] interactiveSourcePositions,
            Matrix4x4 interactiveLocalToWorld,
            float interactiveLocalSpatialScale,
            bool hasInfluenceTarget = false,
            TextStudioPreviewSpatialTarget influenceTarget = default)
        {
            SourcePositions = sourcePositions;
            SourceVisualBounds = sourceVisualBounds;
            SourceTextNormalizedPositions = sourceTextNormalizedPositions;
            SourceColors = sourceColors;
            SourceColorValidity = sourceColorValidity;
            SourceTextBounds = sourceTextBounds;
            GlyphCount = Mathf.Max(0, glyphCount);
            LocalToWorld = localToWorld;
            InteractiveSourcePositions = interactiveSourcePositions ?? sourcePositions;
            InteractiveLocalToWorld = interactiveLocalToWorld;
            InteractiveLocalSpatialScale =
                float.IsFinite(interactiveLocalSpatialScale) &&
                interactiveLocalSpatialScale > 0.0001f
                    ? interactiveLocalSpatialScale
                    : 1f;
            HasInfluenceTarget = hasInfluenceTarget;
            InfluenceTarget = influenceTarget;
        }

        internal bool IsCreated =>
            GlyphCount > 0 &&
            SourcePositions != null &&
            SourceTextNormalizedPositions != null;

        internal TextStudioPreviewSpatialEvaluationContext WithTarget(
            in TextStudioPreviewSpatialTarget target)
        {
            return new TextStudioPreviewSpatialEvaluationContext(
                InteractiveSourcePositions,
                SourceVisualBounds,
                SourceTextNormalizedPositions,
                SourceColors,
                SourceColorValidity,
                SourceTextBounds,
                GlyphCount,
                InteractiveLocalToWorld,
                InteractiveSourcePositions,
                InteractiveLocalToWorld,
                InteractiveLocalSpatialScale,
                true,
                target);
        }

        internal Vector3 ResolveLocalPosition(
            TextStudioInfluenceFieldSpace space,
            Vector3 position)
        {
            switch (space)
            {
                case TextStudioInfluenceFieldSpace.SourceTextNormalized:
                    return new Vector3(
                        Mathf.LerpUnclamped(SourceTextBounds.x, SourceTextBounds.z, position.x),
                        Mathf.LerpUnclamped(SourceTextBounds.y, SourceTextBounds.w, position.y),
                        position.z);
                case TextStudioInfluenceFieldSpace.World:
                    return LocalToWorld.inverse.MultiplyPoint3x4(position);
                default:
                    return position;
            }
        }

        internal Vector3 ResolveWorldPosition(
            TextStudioInfluenceFieldSpace space,
            Vector3 position)
        {
            return space switch
            {
                TextStudioInfluenceFieldSpace.World => position,
                TextStudioInfluenceFieldSpace.SourceTextNormalized =>
                    LocalToWorld.MultiplyPoint3x4(
                        ResolveLocalPosition(space, position)),
                _ => InteractiveLocalToWorld.MultiplyPoint3x4(position)
            };
        }

        internal Vector3 NormalizeLocalPosition(Vector3 localPosition)
        {
            return TextStudioText.NormalizeInfluenceTarget(
                localPosition,
                SourceTextBounds);
        }
    }

    internal interface ITextStudioInteractiveSpatialPreviewRenderer
    {
        bool TryResolveTarget(
            Vector2 normalizedViewportPosition,
            out TextStudioPreviewSpatialTarget target);

        bool TryProjectPoint(
            TextStudioInfluenceFieldSpace space,
            Vector3 position,
            out Vector2 normalizedViewportPosition);

        void SetPreviewTarget(
            bool enabled,
            in TextStudioPreviewSpatialTarget target);
    }

    internal static class TextStudioPreviewTMPSpatialContextBuilder
    {
        const float TargetRadiusTextSpanFraction = 0.35f;
        const float MaximumInteractiveLocalSpatialScale = 10000f;

        internal static TextStudioPreviewSpatialEvaluationContext Build(
            TMP_Text text,
            TextStudioCompiledPlan plan)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            TMP_TextInfo textInfo = text.textInfo;
            int glyphCount = Mathf.Max(0, plan.VisibleGlyphCount);
            Vector3[] sourcePositions = new Vector3[glyphCount];
            TextStudioGlyphVisualBounds[] sourceVisualBounds =
                new TextStudioGlyphVisualBounds[glyphCount];
            Vector3[] normalizedPositions = new Vector3[glyphCount];
            TextStudioGlyphColorQuad[] sourceColors =
                new TextStudioGlyphColorQuad[glyphCount];
            bool[] sourceColorValidity = new bool[glyphCount];
            bool[] mapped = new bool[glyphCount];

            Vector2 minimum = new Vector2(
                float.PositiveInfinity,
                float.PositiveInfinity);
            Vector2 maximum = new Vector2(
                float.NegativeInfinity,
                float.NegativeInfinity);
            bool hasVisibleBounds = false;
            if (textInfo?.characterInfo != null)
            {
                int characterCount = Mathf.Min(
                    textInfo.characterCount,
                    textInfo.characterInfo.Length);
                for (int characterIndex = 0;
                     characterIndex < characterCount;
                     characterIndex++)
                {
                    TMP_CharacterInfo character = textInfo.characterInfo[characterIndex];
                    if (character.elementType != TMP_TextElementType.Character)
                        continue;

                    int glyphIndex = plan.GetUnitIndexForDisplayChar(character.index);
                    if (glyphIndex < 0 ||
                        glyphIndex >= glyphCount ||
                        mapped[glyphIndex])
                    {
                        continue;
                    }

                    mapped[glyphIndex] = true;
                    sourceColorValidity[glyphIndex] = true;
                    sourceColors[glyphIndex] = TextStudioGlyphColorQuad.Uniform(
                        ResolveSemanticColor(in character));

                    if (TryGetCharacterQuad(
                            textInfo,
                            in character,
                            out Vector3[] vertices,
                            out Color32[] colors,
                            out int vertexIndex))
                    {
                        sourcePositions[glyphIndex] =
                            (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;
                        Vector2 glyphMinimum = new Vector2(
                            float.PositiveInfinity,
                            float.PositiveInfinity);
                        Vector2 glyphMaximum = new Vector2(
                            float.NegativeInfinity,
                            float.NegativeInfinity);
                        for (int vertex = 0; vertex < 4; vertex++)
                        {
                            Vector3 sourceVertex = vertices[vertexIndex + vertex];
                            Vector2 sourcePoint = new Vector2(sourceVertex.x, sourceVertex.y);
                            glyphMinimum = Vector2.Min(glyphMinimum, sourcePoint);
                            glyphMaximum = Vector2.Max(glyphMaximum, sourcePoint);
                        }
                        sourceVisualBounds[glyphIndex] = new TextStudioGlyphVisualBounds(
                            glyphMinimum,
                            glyphMaximum,
                            character.isVisible);
                        if (colors != null && vertexIndex + 3 < colors.Length)
                        {
                            sourceColors[glyphIndex] = new TextStudioGlyphColorQuad(
                                colors[vertexIndex],
                                colors[vertexIndex + 1],
                                colors[vertexIndex + 2],
                                colors[vertexIndex + 3]);
                        }
                    }
                    else
                    {
                        sourcePositions[glyphIndex] = new Vector3(
                            (character.origin + character.xAdvance) * 0.5f,
                            character.baseLine,
                            0f);
                        Vector2 fallbackPoint = new Vector2(
                            sourcePositions[glyphIndex].x,
                            sourcePositions[glyphIndex].y);
                        sourceVisualBounds[glyphIndex] = new TextStudioGlyphVisualBounds(
                            fallbackPoint,
                            fallbackPoint,
                            false);
                    }

                    if (character.isVisible &&
                        IsFinite(character.bottomLeft) &&
                        IsFinite(character.topRight))
                    {
                        minimum = Vector2.Min(minimum, character.bottomLeft);
                        maximum = Vector2.Max(maximum, character.topRight);
                        hasVisibleBounds = true;
                    }
                }
            }

            FillUnmappedGlyphs(
                sourcePositions,
                sourceColors,
                sourceColorValidity,
                mapped);
            for (int i = 0; i < glyphCount; i++)
            {
                if (mapped[i])
                    continue;
                Vector2 point = new Vector2(sourcePositions[i].x, sourcePositions[i].y);
                sourceVisualBounds[i] = new TextStudioGlyphVisualBounds(
                    point,
                    point,
                    false);
            }

            if (!hasVisibleBounds)
            {
                Bounds fallback = text.textBounds;
                minimum = fallback.min;
                maximum = fallback.max;
            }
            if (!IsFinite(minimum) || !IsFinite(maximum))
            {
                minimum = Vector2.zero;
                maximum = Vector2.zero;
            }

            Vector4 sourceTextBounds = new Vector4(
                minimum.x,
                minimum.y,
                maximum.x,
                maximum.y);
            for (int i = 0; i < glyphCount; i++)
            {
                normalizedPositions[i] = TextStudioText.NormalizeInfluenceTarget(
                    sourcePositions[i],
                    sourceTextBounds);
            }

            float interactiveLocalSpatialScale =
                ResolveInteractiveLocalSpatialScale(plan, sourceTextBounds);
            Vector3[] interactiveSourcePositions = sourcePositions;
            Matrix4x4 localToWorld = text.transform.localToWorldMatrix;
            Matrix4x4 interactiveLocalToWorld = localToWorld;
            if (interactiveLocalSpatialScale > 1.0001f)
            {
                interactiveSourcePositions = new Vector3[glyphCount];
                for (int i = 0; i < glyphCount; i++)
                {
                    interactiveSourcePositions[i] =
                        sourcePositions[i] * interactiveLocalSpatialScale;
                }
                float inverseScale = 1f / interactiveLocalSpatialScale;
                interactiveLocalToWorld = localToWorld * Matrix4x4.Scale(
                    new Vector3(inverseScale, inverseScale, inverseScale));
            }

            return new TextStudioPreviewSpatialEvaluationContext(
                sourcePositions,
                sourceVisualBounds,
                normalizedPositions,
                sourceColors,
                sourceColorValidity,
                sourceTextBounds,
                glyphCount,
                localToWorld,
                interactiveSourcePositions,
                interactiveLocalToWorld,
                interactiveLocalSpatialScale);
        }

        static float ResolveInteractiveLocalSpatialScale(
            TextStudioCompiledPlan plan,
            Vector4 sourceTextBounds)
        {
            float width = Mathf.Abs(sourceTextBounds.z - sourceTextBounds.x);
            float height = Mathf.Abs(sourceTextBounds.w - sourceTextBounds.y);
            float textSpan = Mathf.Max(width, height);
            if (!float.IsFinite(textSpan) || textSpan <= 0.0001f)
                return 1f;

            List<float> targetRadii = new List<float>(4);
            HashSet<TextStudioInfluenceField> visited =
                new HashSet<TextStudioInfluenceField>();
            List<TextStudioInfluenceField> fields =
                new List<TextStudioInfluenceField>(8);
            int bindingCount = plan?.EffectBindings != null
                ? Mathf.Min(plan.EffectBindingCount, plan.EffectBindings.Length)
                : 0;
            for (int bindingIndex = 0;
                 bindingIndex < bindingCount;
                 bindingIndex++)
            {
                TextStudioEffectScopeChain scope =
                    plan.EffectBindings[bindingIndex].scope;
                if (scope == null)
                    continue;
                fields.Clear();
                scope.AppendInfluenceFields(fields);
                for (int fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                {
                    TextStudioInfluenceField field = fields[fieldIndex];
                    if (field == null ||
                        !visited.Add(field) ||
                        field.Shape != TextStudioInfluenceFieldShape.TargetDistance ||
                        (field.Space != TextStudioInfluenceFieldSpace.SourceLayout &&
                         field.Space != TextStudioInfluenceFieldSpace.PlacedText))
                    {
                        continue;
                    }
                    targetRadii.Add(field.Radius);
                }
            }

            if (targetRadii.Count == 0)
                return 1f;
            targetRadii.Sort();
            float representativeRadius = targetRadii[targetRadii.Count / 2];
            float fittedSpan = textSpan * TargetRadiusTextSpanFraction;
            if (!float.IsFinite(representativeRadius) || fittedSpan <= 0.0001f)
                return 1f;
            return Mathf.Clamp(
                representativeRadius / fittedSpan,
                1f,
                MaximumInteractiveLocalSpatialScale);
        }

        static void FillUnmappedGlyphs(
            Vector3[] sourcePositions,
            TextStudioGlyphColorQuad[] sourceColors,
            bool[] sourceColorValidity,
            bool[] mapped)
        {
            Vector3 previousPosition = Vector3.zero;
            TextStudioGlyphColorQuad previousColor = TextStudioGlyphColorQuad.White;
            bool hasPrevious = false;
            for (int i = 0; i < mapped.Length; i++)
            {
                if (mapped[i])
                {
                    previousPosition = sourcePositions[i];
                    previousColor = sourceColors[i];
                    hasPrevious = true;
                    continue;
                }

                sourcePositions[i] = hasPrevious
                    ? previousPosition
                    : new Vector3(i, 0f, 0f);
                sourceColors[i] = hasPrevious
                    ? previousColor
                    : TextStudioGlyphColorQuad.White;
                sourceColorValidity[i] = hasPrevious;
            }
        }

        static bool TryGetCharacterQuad(
            TMP_TextInfo textInfo,
            in TMP_CharacterInfo character,
            out Vector3[] vertices,
            out Color32[] colors,
            out int vertexIndex)
        {
            vertices = null;
            colors = null;
            vertexIndex = character.vertexIndex;
            int materialIndex = character.materialReferenceIndex;
            if (!character.isVisible ||
                textInfo.meshInfo == null ||
                materialIndex < 0 ||
                materialIndex >= textInfo.meshInfo.Length)
            {
                return false;
            }

            vertices = textInfo.meshInfo[materialIndex].vertices;
            colors = textInfo.meshInfo[materialIndex].colors32;
            return vertices != null &&
                   vertexIndex >= 0 &&
                   vertexIndex + 3 < vertices.Length;
        }

        static Color32 ResolveSemanticColor(in TMP_CharacterInfo character)
        {
            if ((character.style & FontStyles.Underline) != 0)
                return character.underlineColor;
            if ((character.style & FontStyles.Strikethrough) != 0)
                return character.strikethroughColor;
            return character.color;
        }

        static bool IsFinite(Vector2 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y);
        }

        static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }
    }
}
