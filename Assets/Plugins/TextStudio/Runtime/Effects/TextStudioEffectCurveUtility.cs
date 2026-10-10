using UnityEngine;

namespace TextStudio
{
    internal static class TextStudioEffectCurveUtility
    {
        internal static float Evaluate(TextStudioEffectCurvePreset preset, float t)
        {
            t = Mathf.Clamp01(t);
            switch (preset)
            {
                case TextStudioEffectCurvePreset.Sine:
                    return Mathf.Sin(t * Mathf.PI * 2f);
                case TextStudioEffectCurvePreset.HalfSine:
                    return Mathf.Sin(t * Mathf.PI);
                case TextStudioEffectCurvePreset.Square:
                    return t < 0.5f ? 1f : -1f;
                case TextStudioEffectCurvePreset.Step:
                    return t < 0.5f ? 0f : 1f;
                case TextStudioEffectCurvePreset.Hold:
                    return 1f;
                case TextStudioEffectCurvePreset.Bounce:
                    return Mathf.Abs(Mathf.Sin(t * Mathf.PI));
                case TextStudioEffectCurvePreset.EaseIn:
                    return t * t;
                case TextStudioEffectCurvePreset.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                case TextStudioEffectCurvePreset.EaseInOut:
                    return t * t * (3f - 2f * t);
                case TextStudioEffectCurvePreset.BackOut:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    float u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                }
                case TextStudioEffectCurvePreset.ElasticOut:
                    if (t <= 0f || t >= 1f)
                        return t;
                    return Mathf.Pow(2f, -10f * t) *
                        Mathf.Sin((t * 10f - 0.75f) * (Mathf.PI * 2f / 3f)) + 1f;
                case TextStudioEffectCurvePreset.ReverseLinear:
                    return 1f - t;
                case TextStudioEffectCurvePreset.Custom:
                case TextStudioEffectCurvePreset.Linear:
                default:
                    return t;
            }
        }

        internal static bool ShouldMirrorLoopProgress(
            TextStudioEffectPlaybackPreset playback,
            TextStudioEffectCurvePreset curve)
        {
            if (playback != TextStudioEffectPlaybackPreset.Loop &&
                playback != TextStudioEffectPlaybackPreset.Cycle)
            {
                return false;
            }

            switch (curve)
            {
                case TextStudioEffectCurvePreset.EaseIn:
                case TextStudioEffectCurvePreset.EaseOut:
                case TextStudioEffectCurvePreset.EaseInOut:
                case TextStudioEffectCurvePreset.BackOut:
                case TextStudioEffectCurvePreset.ElasticOut:
                    return true;
                default:
                    return false;
            }
        }
    }
}
