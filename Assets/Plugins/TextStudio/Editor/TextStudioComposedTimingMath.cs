using UnityEngine;

namespace TextStudio.Editor
{
    internal static class TextStudioComposedTimingMath
    {
        internal const float MinimumDuration = 0.05f;
        internal const float MaximumDuration = 100f;
        internal const float MinimumPhaseShift = -10f;
        internal const float MaximumPhaseShift = 10f;

        internal static float VisualStartFromPhaseShift(float phaseShift)
        {
            return -SanitizePhaseShift(phaseShift);
        }

        internal static float PhaseShiftFromVisualStart(float visualStart)
        {
            return SanitizePhaseShift(-visualStart);
        }

        internal static float ResolveDuration(float baseDuration, float speed)
        {
            float safeBase = IsFinitePositive(baseDuration) ? baseDuration : 1f;
            float safeSpeed = IsFinitePositive(speed) ? speed : 1f;
            return Mathf.Clamp(safeBase / safeSpeed, MinimumDuration, MaximumDuration);
        }

        internal static float ResolveSpeed(float baseDuration, float duration)
        {
            float safeBase = IsFinitePositive(baseDuration) ? baseDuration : 1f;
            float safeDuration = IsFinitePositive(duration)
                ? Mathf.Clamp(duration, MinimumDuration, MaximumDuration)
                : safeBase;
            return Mathf.Max(0.01f, safeBase / safeDuration);
        }

        internal static float ResolveTimelineDuration(
            float revealDuration,
            float activeDuration,
            float hideDuration,
            int activeDurationMultiplier)
        {
            return ResolvePhaseDuration(
                       0,
                       revealDuration,
                       activeDuration,
                       hideDuration,
                       activeDurationMultiplier) +
                   ResolvePhaseDuration(
                       1,
                       revealDuration,
                       activeDuration,
                       hideDuration,
                       activeDurationMultiplier) +
                   ResolvePhaseDuration(
                       2,
                       revealDuration,
                       activeDuration,
                       hideDuration,
                       activeDurationMultiplier);
        }

        internal static float ResolvePhaseStart(
            int phaseIndex,
            float revealDuration,
            float activeDuration,
            float hideDuration,
            int activeDurationMultiplier)
        {
            if (phaseIndex <= 0)
                return 0f;

            float start = ResolvePhaseDuration(
                0,
                revealDuration,
                activeDuration,
                hideDuration,
                activeDurationMultiplier);
            if (phaseIndex == 1)
                return start;

            return start + ResolvePhaseDuration(
                1,
                revealDuration,
                activeDuration,
                hideDuration,
                activeDurationMultiplier);
        }

        internal static float ResolvePhaseDuration(
            int phaseIndex,
            float revealDuration,
            float activeDuration,
            float hideDuration,
            int activeDurationMultiplier)
        {
            float duration = phaseIndex switch
            {
                0 => revealDuration,
                2 => hideDuration,
                _ => activeDuration * Mathf.Max(1, activeDurationMultiplier)
            };
            return IsFinitePositive(duration) ? duration : 1f;
        }

        internal static bool TryResolveRevealBlendWindow(
            TextStudioPhaseMask phaseMask,
            float phaseShift,
            float revealDuration,
            float blendDuration,
            out float start,
            out float duration)
        {
            start = 0f;
            duration = 0f;
            if (!TextStudioRevealBlendTiming.TryResolveAppearanceWindow(
                    phaseMask,
                    phaseShift,
                    revealDuration,
                    blendDuration,
                    out float unresolvedStart,
                    out float unresolvedEnd))
            {
                return false;
            }

            float clippedStart = Mathf.Max(0f, unresolvedStart);
            float clippedEnd = Mathf.Max(0f, unresolvedEnd);
            if (clippedEnd <= clippedStart + TextStudioRevealBlendTiming.Epsilon)
                return false;

            start = clippedStart;
            duration = clippedEnd - clippedStart;
            return true;
        }

        internal static float SnapPhaseShift(float phaseShift, bool fine)
        {
            float step = fine ? 0.01f : 0.05f;
            return SanitizePhaseShift(Mathf.Round(phaseShift / step) * step);
        }

        internal static float SnapDuration(float duration, bool fine)
        {
            float step = fine ? 0.01f : 0.05f;
            float snapped = Mathf.Round(duration / step) * step;
            return Mathf.Clamp(snapped, MinimumDuration, MaximumDuration);
        }

        static float SanitizePhaseShift(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;
            return Mathf.Clamp(value, MinimumPhaseShift, MaximumPhaseShift);
        }

        static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
