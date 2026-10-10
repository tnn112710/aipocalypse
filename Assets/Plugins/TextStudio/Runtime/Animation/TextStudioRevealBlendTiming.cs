using UnityEngine;

namespace TextStudio
{
    internal static class TextStudioRevealBlendTiming
    {
        internal const float Epsilon = 0.0001f;

        internal static bool TryResolveAppearanceWindow(
            TextStudioPhaseMask phaseMask,
            float phaseShift,
            float appearanceDuration,
            float blendDuration,
            out float start,
            out float end)
        {
            start = 0f;
            end = 0f;
            bool includesReveal = (phaseMask & TextStudioPhaseMask.Reveal) != 0;
            bool includesActive = (phaseMask & TextStudioPhaseMask.Active) != 0;
            if (!includesReveal && !includesActive)
                return false;

            float safeAppearanceDuration = Mathf.Max(Epsilon, appearanceDuration);
            float requestedBlendDuration = Mathf.Max(0f, blendDuration);
            if (requestedBlendDuration <= Epsilon)
                return false;

            if (includesReveal)
            {
                float safeBlendDuration = Mathf.Min(
                    requestedBlendDuration,
                    safeAppearanceDuration);
                end = safeAppearanceDuration;
                start = end - safeBlendDuration;
                return true;
            }

            if (phaseShift < -Epsilon)
                return false;

            start = safeAppearanceDuration - Mathf.Max(0f, phaseShift);
            end = start + requestedBlendDuration;
            return end > start + Epsilon;
        }

        internal static float SmoothWeight(float elapsed, float start, float end)
        {
            float duration = Mathf.Max(Epsilon, end - start);
            float linear = Mathf.Clamp01((elapsed - start) / duration);
            return linear * linear * (3f - 2f * linear);
        }
    }
}
