using UnityEngine;

namespace TextStudio
{
    internal static class TextStudioWritingPlaybackPolicy
    {
        internal static bool Resolve(
            TextStudioMotionPolicy policy,
            TextStudioReducedMotionBehavior behavior,
            ref float strokeGap)
        {
            TextStudioMotionPolicy resolved = TextStudioMotionSettings.Resolve(policy);
            if (behavior == TextStudioReducedMotionBehavior.Preserve ||
                resolved == TextStudioMotionPolicy.Full)
            {
                return false;
            }
            if (resolved == TextStudioMotionPolicy.None ||
                behavior == TextStudioReducedMotionBehavior.Disable)
            {
                strokeGap = 0f;
                return true;
            }

            strokeGap *= 0.25f;
            return false;
        }
    }

    internal static class TextStudioWritingScheduleMath
    {
        internal static float ResolveStrokeDistance(
            in TextStudioPreparedWritingStroke stroke,
            int strokeIndex,
            int strokeCount,
            float normalizedDistance,
            float strokeGap,
            bool reverse)
        {
            int gapCount = Mathf.Max(0, strokeCount - 1);
            float gap = Mathf.Min(
                Mathf.Clamp(strokeGap, 0f, 0.25f),
                gapCount > 0 ? 0.95f / gapCount : 0f);
            float usable = Mathf.Max(0.05f, 1f - gap * gapCount);
            float distance = stroke.SequenceStart * usable +
                             strokeIndex * gap +
                             Mathf.Clamp01(normalizedDistance) *
                             stroke.SequenceDuration * usable;
            return reverse ? 1f - distance : distance;
        }

        internal static float ResolveSourceAlpha(
            in TextStudioWritingConfiguration configuration,
            TextStudioWritingSourceKind sourceKind,
            float localProgress)
        {
            float progress = Mathf.Clamp01(localProgress);
            if (sourceKind == TextStudioWritingSourceKind.None)
                return 1f;
            if (sourceKind == TextStudioWritingSourceKind.Typewriter)
            {
                return SmoothStep(
                    0f,
                    Mathf.Max(0.02f, configuration.EdgeSoftness),
                    progress);
            }
            if (sourceKind == TextStudioWritingSourceKind.GlyphCoverage)
            {
                return 1f;
            }
            switch (configuration.Mode)
            {
                case TextStudioWritingMode.None:
                    return 1f;
                case TextStudioWritingMode.InkReveal:
                    return 1f;
                default:
                    return SmoothStep(
                        configuration.FillStart,
                        Mathf.Min(1f, configuration.FillStart + configuration.FillDuration),
                        progress);
            }
        }

        internal static bool UsesDirectionalSourceMask(TextStudioWritingMode mode)
        {
            return mode == TextStudioWritingMode.InkReveal;
        }

        internal static bool UsesDirectionalSourceMask(
            TextStudioWritingSourceKind sourceKind)
        {
            return sourceKind == TextStudioWritingSourceKind.GlyphCoverage;
        }

        internal static TextStudioWritingMode ResolveGpuMode(
            TextStudioWritingMode requestedMode,
            TextStudioWritingSourceKind sourceKind)
        {
            if (sourceKind != TextStudioWritingSourceKind.GlyphCoverage)
                return TextStudioWritingMode.None;
            return UsesDirectionalSourceMask(requestedMode)
                ? requestedMode
                : TextStudioWritingMode.InkReveal;
        }

        internal static bool UsesPreparedPath(TextStudioWritingMode mode)
        {
            return TextStudioWritingModeUtility.RequestedDataLevel(mode) >=
                   TextStudioWritingDataLevel.PreparedOutline;
        }

        static float SmoothStep(float edge0, float edge1, float value)
        {
            if (edge1 <= edge0 + 0.000001f)
                return value >= edge1 ? 1f : 0f;
            float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
