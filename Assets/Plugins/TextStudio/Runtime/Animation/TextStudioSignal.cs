using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Normalized signal math shared by component and UI Toolkit integrations.</summary>
    public static class TextStudioSignal
    {
        /// <summary>Maps a finite source range into [0,1]. Reversed ranges invert the signal.</summary>
        public static float Normalize(float value, float minimum = 0f, float maximum = 1f)
        {
            if (!float.IsFinite(value) || !float.IsFinite(minimum) || !float.IsFinite(maximum) || minimum == maximum)
                return 0f;
            return (float)Math.Clamp(((double)value - minimum) / ((double)maximum - minimum), 0d, 1d);
        }

        /// <summary>Allocation-free RMS of a caller-owned sample window; nonfinite samples contribute silence.</summary>
        public static float AudioRms(ReadOnlySpan<float> samples, float gain = 1f)
        {
            if (samples.IsEmpty || !float.IsFinite(gain) || gain <= 0f) return 0f;
            double sum = 0d;
            for (int i = 0; i < samples.Length; i++)
            {
                double sample = samples[i];
                if (double.IsFinite(sample)) sum += sample * sample;
            }
            return (float)Math.Min(1d, Math.Sqrt(sum / samples.Length) * gain);
        }

        /// <summary>Clock-based beat phase in [0,1), with no accumulated delta-time drift.</summary>
        public static float BeatPhase(double timeSeconds, float beatsPerMinute, double originSeconds = 0d, float beatsPerCycle = 1f)
        {
            if (!double.IsFinite(timeSeconds) || !double.IsFinite(originSeconds) || !float.IsFinite(beatsPerMinute) ||
                beatsPerMinute <= 0f || !float.IsFinite(beatsPerCycle) || beatsPerCycle <= 0f) return 0f;
            double cycles = (timeSeconds - originSeconds) * (beatsPerMinute / (60d * beatsPerCycle));
            if (!double.IsFinite(cycles)) return 0f;
            return Mathf.Min((float)(cycles - Math.Floor(cycles)), 0.99999994f);
        }

        /// <summary>Frame-rate-independent attack/release smoothing. Seconds are exponential time constants.</summary>
        public static float Smooth(float current, float target, float deltaTime, float attackSeconds, float releaseSeconds)
        {
            current = Normalize(current);
            target = Normalize(target);
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f) return current;
            float seconds = target > current ? attackSeconds : releaseSeconds;
            if (!float.IsFinite(seconds) || seconds <= 0f) return target;
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-deltaTime / seconds));
        }
    }
}
