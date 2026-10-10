using System.Collections.Generic;
#if UNITY_6000_2_OR_NEWER
using Unity.Collections;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal static class TextStudioEffectPreviewUtility
    {
        internal const float PixelsPerUnit = 22f;
        internal const float DefaultCycleDuration = 1f;
        internal const string SampleText = "Text Studio Preview\nEffect Area Demo";

        static readonly Color32 s_BaseColor = new Color32(230, 235, 240, 255);
        static readonly string[] s_SpeedLabels = { "0.25x", "0.5x", "1x", "2x", "4x" };
        static readonly float[] s_Speeds = { 0.25f, 0.5f, 1f, 2f, 4f };

        internal static Color32 BaseColor => s_BaseColor;
        internal static int SpeedPresetCount => s_Speeds.Length;

#if UNITY_6000_2_OR_NEWER
        internal sealed class PreviewGlyphLabel : Label
        {
            TextStudioCornerOffsets _vertexCornerOffsets;

            internal TextStudioCornerOffsets VertexCornerOffsets => _vertexCornerOffsets;

            internal PreviewGlyphLabel(string text)
                : base(text)
            {
                PostProcessTextVertices += ApplyCornerDeformation;
            }

            internal void SetCornerOffsets(in TextStudioCornerOffsets cornerOffsets)
            {
                _vertexCornerOffsets = cornerOffsets.FlipVerticalAxis();
                MarkDirtyRepaint();
            }

            void ApplyCornerDeformation(TextElement.GlyphsEnumerable glyphs)
            {
                foreach (TextElement.Glyph glyph in glyphs)
                {
                    NativeSlice<Vertex> vertices = glyph.vertices;
                    if (vertices.Length < 4)
                        continue;

                    OffsetVertex(vertices, 0, _vertexCornerOffsets.BottomLeft);
                    OffsetVertex(vertices, 1, _vertexCornerOffsets.TopLeft);
                    OffsetVertex(vertices, 2, _vertexCornerOffsets.TopRight);
                    OffsetVertex(vertices, 3, _vertexCornerOffsets.BottomRight);
                    break;
                }
            }

            static void OffsetVertex(NativeSlice<Vertex> vertices, int index, Vector2 cornerOffset)
            {
                Vertex vertex = vertices[index];
                vertex.position += new Vector3(
                    cornerOffset.x * PixelsPerUnit,
                    cornerOffset.y * PixelsPerUnit,
                    0f);
                vertices[index] = vertex;
            }
        }
#endif

        internal static string GetSpeedLabel(int index)
        {
            return s_SpeedLabels[Mathf.Clamp(index, 0, s_SpeedLabels.Length - 1)];
        }

        internal static float GetSpeed(int index)
        {
            return s_Speeds[Mathf.Clamp(index, 0, s_Speeds.Length - 1)];
        }

        internal static void BuildGlyphIndexMap(string normalized, List<int> glyphIndices)
        {
            if (glyphIndices == null)
                return;

            glyphIndices.Clear();
            if (string.IsNullOrEmpty(normalized))
                return;

            for (int i = 0; i < normalized.Length; i++)
            {
                if (normalized[i] != '\n')
                    glyphIndices.Add(i);
            }
        }

        internal static float GetCycleDuration(
            TextStudioPreviewSegment segments,
            float revealDuration,
            float activeDuration,
            float hideDuration)
        {
            return GetCycleDuration(segments, revealDuration, activeDuration, hideDuration, 1);
        }

        internal static float GetCycleDuration(
            TextStudioPreviewSegment segments,
            float revealDuration,
            float activeDuration,
            float hideDuration,
            int activeDurationMultiplier)
        {
            TextStudioPreviewSegment normalized = NormalizeSegments(segments);
            float duration = 0f;
            if ((normalized & TextStudioPreviewSegment.Reveal) != 0)
                duration += Mathf.Max(0.0001f, revealDuration);
            if ((normalized & TextStudioPreviewSegment.Active) != 0)
            {
                duration += Mathf.Max(0.0001f, activeDuration) *
                    Mathf.Max(1, activeDurationMultiplier);
            }
            if ((normalized & TextStudioPreviewSegment.Hide) != 0)
                duration += Mathf.Max(0.0001f, hideDuration);
            return Mathf.Max(0.0001f, duration);
        }

        internal static void ResolveFrame(
            float lifecycleTime,
            TextStudioPreviewSegment segments,
            float revealDuration,
            float activeDuration,
            float hideDuration,
            out AnimationPhase phase,
            out float phaseProgress,
            out float phaseTime,
            out float cycleDuration)
        {
            ResolveFrame(
                lifecycleTime,
                segments,
                revealDuration,
                activeDuration,
                hideDuration,
                1,
                out phase,
                out phaseProgress,
                out phaseTime,
                out cycleDuration);
        }

        internal static void ResolveFrame(
            float lifecycleTime,
            TextStudioPreviewSegment segments,
            float revealDuration,
            float activeDuration,
            float hideDuration,
            int activeDurationMultiplier,
            out AnimationPhase phase,
            out float phaseProgress,
            out float phaseTime,
            out float cycleDuration)
        {
            TextStudioPreviewSegment normalized = NormalizeSegments(segments);
            float reveal = Mathf.Max(0.0001f, revealDuration);
            float active = Mathf.Max(0.0001f, activeDuration);
            float activeWindow = active * Mathf.Max(1, activeDurationMultiplier);
            float hide = Mathf.Max(0.0001f, hideDuration);
            cycleDuration = GetCycleDuration(
                normalized,
                reveal,
                active,
                hide,
                activeDurationMultiplier);
            float remaining = Mathf.Clamp(lifecycleTime, 0f, cycleDuration);

            if ((normalized & TextStudioPreviewSegment.Reveal) != 0)
            {
                if (remaining < reveal || !HasLaterSegment(normalized, TextStudioPreviewSegment.Reveal))
                {
                    phase = AnimationPhase.Appearance;
                    phaseTime = Mathf.Min(remaining, reveal);
                    phaseProgress = Mathf.Clamp01(phaseTime / reveal);
                    return;
                }

                remaining -= reveal;
            }

            if ((normalized & TextStudioPreviewSegment.Active) != 0)
            {
                if (remaining < activeWindow || !HasLaterSegment(normalized, TextStudioPreviewSegment.Active))
                {
                    phase = AnimationPhase.Persistent;
                    phaseTime = Mathf.Min(remaining, activeWindow);
                    phaseProgress = Mathf.Clamp01(phaseTime / active);
                    return;
                }

                remaining -= activeWindow;
            }

            phase = AnimationPhase.Disappearance;
            phaseTime = Mathf.Min(remaining, hide);
            phaseProgress = Mathf.Clamp01(phaseTime / hide);
        }

        internal static TextStudioPreviewSegment SetSegmentEnabled(
            TextStudioPreviewSegment segments,
            TextStudioPreviewSegment segment,
            bool enabled)
        {
            TextStudioPreviewSegment normalized = NormalizeSegments(segments);
            TextStudioPreviewSegment knownSegment = segment & TextStudioPreviewSegment.All;
            if (knownSegment == TextStudioPreviewSegment.None)
                return normalized;

            TextStudioPreviewSegment candidate = enabled
                ? normalized | knownSegment
                : normalized & ~knownSegment;
            return candidate == TextStudioPreviewSegment.None ? normalized : candidate;
        }

        internal static float AdvanceProgress(
            float currentProgress,
            float deltaTime,
            float speed,
            bool repeat,
            out bool keepAutoplay)
        {
            float progress = Mathf.Max(0f, currentProgress);
            float step = Mathf.Max(0f, deltaTime) * Mathf.Max(0f, speed);
            if (step <= 0f)
            {
                keepAutoplay = true;
                return progress;
            }

            float next = progress + step;
            if (repeat)
            {
                keepAutoplay = true;
                return next;
            }

            if (next < 1f)
            {
                keepAutoplay = true;
                return next;
            }

            keepAutoplay = false;
            return 1f;
        }

        internal static float GetScrubProgress(float previewTime)
        {
            return GetScrubProgress(previewTime, DefaultCycleDuration);
        }

        internal static float GetScrubProgress(float previewTime, float cycleDuration)
        {
            if (previewTime <= 0f)
                return 0f;

            float duration = Mathf.Max(0.0001f, cycleDuration);
            return Mathf.Repeat(previewTime, duration) / duration;
        }

        internal static float GetTimeFromScrub(float currentTime, float scrubProgress)
        {
            return GetTimeFromScrub(currentTime, scrubProgress, DefaultCycleDuration);
        }

        internal static float GetTimeFromScrub(
            float currentTime,
            float scrubProgress,
            float cycleDuration)
        {
            float duration = Mathf.Max(0.0001f, cycleDuration);
            float cycle = Mathf.Floor(Mathf.Max(0f, currentTime) / duration);
            return (cycle + Mathf.Clamp01(scrubProgress)) * duration;
        }

        internal static void ApplyState(Label glyph, EvaluatedGlyphState state)
        {
            ApplyState(glyph, state, s_BaseColor);
        }

        internal static void ApplyState(
            Label glyph,
            EvaluatedGlyphState state,
            Color32 sourceColor)
        {
#if UNITY_6000_2_OR_NEWER
            if (glyph is PreviewGlyphLabel previewGlyph)
                previewGlyph.SetCornerOffsets(in state.cornerOffsets);
#endif
            Vector3 offset = state.positionOffset * PixelsPerUnit;
            glyph.style.translate = new StyleTranslate(new Translate(
                new Length(offset.x, LengthUnit.Pixel),
                new Length(-offset.y, LengthUnit.Pixel),
                0f));

            float angle = state.rotation.eulerAngles.z;
            if (angle > 180f)
                angle -= 360f;
            glyph.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));
            glyph.style.transformOrigin = new StyleTransformOrigin(new TransformOrigin(
                new Length(state.Pivot.x * 100f, LengthUnit.Percent),
                new Length(state.Pivot.y * 100f, LengthUnit.Percent),
                0f));

            Vector3 scale = state.scale;
            glyph.style.scale = new StyleScale(new Scale(new Vector3(
                Mathf.Max(0.01f, scale.x),
                Mathf.Max(0.01f, scale.y),
                1f)));
            glyph.style.opacity = Mathf.Clamp01(state.alpha);
            glyph.style.color = new StyleColor((Color)state.ResolveColor(sourceColor));
        }

        internal static Label CreateGlyph(string text)
        {
#if UNITY_6000_2_OR_NEWER
            return new PreviewGlyphLabel(text);
#else
            return new Label(text);
#endif
        }

        static TextStudioPreviewSegment NormalizeSegments(TextStudioPreviewSegment segments)
        {
            TextStudioPreviewSegment normalized = segments & TextStudioPreviewSegment.All;
            return normalized == TextStudioPreviewSegment.None
                ? TextStudioPreviewSegment.Reveal | TextStudioPreviewSegment.Active
                : normalized;
        }

        static bool HasLaterSegment(
            TextStudioPreviewSegment segments,
            TextStudioPreviewSegment segment)
        {
            if (segment == TextStudioPreviewSegment.Reveal)
            {
                return (segments & (TextStudioPreviewSegment.Active | TextStudioPreviewSegment.Hide)) != 0;
            }

            return segment == TextStudioPreviewSegment.Active &&
                   (segments & TextStudioPreviewSegment.Hide) != 0;
        }
    }
}
