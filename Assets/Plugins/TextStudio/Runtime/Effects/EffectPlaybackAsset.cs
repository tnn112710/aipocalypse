using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Per-glyph stagger parameters for animation phasing.
    /// Controls how effects stagger across characters, words, and lines.
    /// </summary>
    [Serializable]
    public struct PhaseParams
    {
        [TextStudioRange(0f, 0.5f, 0.001f, suffix: "s", allowOverflow: true)]
        [Tooltip("Additional start delay in seconds for each successive character. 0.02 means 20 ms per character.")]
        public float charOffset;

        [TextStudioRange(0f, 2.5f, 0.005f, suffix: "s", allowOverflow: true)]
        [Tooltip("Additional start delay in seconds for each successive word.")]
        public float wordOffset;

        [TextStudioRange(0f, 5f, 0.01f, suffix: "s", allowOverflow: true)]
        [Tooltip("Additional start delay in seconds for each successive line.")]
        public float lineOffset;

        [Tooltip("Playback speed multiplier after delays. 1x is normal speed; 2x is twice as fast.")]
        [Min(0.01f)]
        public float speed;

        public static PhaseParams Default => new PhaseParams
        {
            charOffset = 0.015f,
            wordOffset = 0f,
            lineOffset = 0f,
            speed = 1f
        };

        public static PhaseParams NoOffset => new PhaseParams
        {
            charOffset = 0f,
            wordOffset = 0f,
            lineOffset = 0f,
            speed = 1f
        };

        /// <summary>
        /// Calculates the cumulative start delay, in seconds, for a glyph from its
        /// character, word, and line positions.
        /// </summary>
        public float ComputeDelay(int glyphIndex, int wordIndex, int lineIndex)
        {
            return glyphIndex * charOffset + wordIndex * wordOffset + lineIndex * lineOffset;
        }
    }

    /// <summary>
    /// Playback mode interface for effect timing/intensity.
    /// </summary>
    public interface IEffectPlayback
    {
        /// <summary>Gets how local time is mapped to normalized effect intensity.</summary>
        PlaybackMode Mode { get; }
        /// <summary>Evaluates normalized effect progress for an elapsed time and phase duration.</summary>
        /// <param name="time">Elapsed time in seconds.</param>
        /// <param name="duration">Fallback forward duration in seconds.</param>
        /// <returns>Normalized effect intensity.</returns>
        float Evaluate(float time, float duration);
    }

    /// <summary>Chooses whether completed finite effect playback holds its final intensity or resets to zero.</summary>
    public enum EffectPlaybackCompletion
    {
        HoldFinal,
        Reset
    }

    /// <summary>
    /// Reusable playback asset that maps elapsed time to normalized effect intensity with delay, looping, hold, and completion policy.
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Playback",
        menuName = "Text Studio/Animation/Playback",
        order = 103)]
    public class EffectPlaybackAsset : ScriptableObject, IEffectPlayback
    {
        [SerializeField] PlaybackMode _mode = PlaybackMode.Once;
        [SerializeField, Min(0f)] float _delayBeforeStart;
        [SerializeField, Min(0.0001f)] float _duration = 0.5f;
        [SerializeField, Min(0f)] float _holdDuration;
        [SerializeField, Min(0f)] float _returnDuration = 0.5f;
        [SerializeField, Min(0)] int _cycleCount;
        [SerializeField, Min(0f)] float _delayBetweenCycles;
        [SerializeField] EffectPlaybackCompletion _completion = EffectPlaybackCompletion.HoldFinal;
        [SerializeField, Range(0f, 1f)] float _externalPreviewValue = 1f;
        [SerializeField] AnimationCurve _weightCurve = AnimationCurve.Linear(0, 0, 1, 1);

        public PlaybackMode Mode => _mode;
        public float DelayBeforeStart => Mathf.Max(0f, _delayBeforeStart);
        public float Duration => Mathf.Max(0.0001f, _duration);
        public float HoldDuration => Mathf.Max(0f, _holdDuration);
        public float ReturnDuration => _returnDuration > 0f ? _returnDuration : Duration;
        public int CycleCount => Mathf.Max(0, _cycleCount);
        public float DelayBetweenCycles => Mathf.Max(0f, _delayBetweenCycles);
        public EffectPlaybackCompletion Completion => _completion;
        public float ExternalPreviewValue => Mathf.Clamp01(_externalPreviewValue);
        public bool IsInfinite => _mode == PlaybackMode.External ||
                                  ((_mode == PlaybackMode.Loop || _mode == PlaybackMode.PingPong) && CycleCount == 0);

        internal void Initialize(PlaybackMode mode)
        {
            _mode = mode;
        }

        internal void Configure(
            PlaybackMode mode,
            float delayBeforeStart,
            float forwardDuration,
            float holdDuration,
            float returnDuration,
            int cycleCount,
            float delayBetweenCycles,
            EffectPlaybackCompletion completion,
            float externalPreviewValue)
        {
            _mode = mode;
            _delayBeforeStart = Mathf.Max(0f, delayBeforeStart);
            _duration = Mathf.Max(0.0001f, forwardDuration);
            _holdDuration = Mathf.Max(0f, holdDuration);
            _returnDuration = Mathf.Max(0f, returnDuration);
            _cycleCount = Mathf.Max(0, cycleCount);
            _delayBetweenCycles = Mathf.Max(0f, delayBetweenCycles);
            _completion = completion;
            _externalPreviewValue = Mathf.Clamp01(externalPreviewValue);
        }

        /// <inheritdoc/>
        public float Evaluate(float time, float duration)
        {
            return Evaluate(time, duration, out _);
        }

        /// <summary>Evaluates normalized intensity and reports whether finite playback has completed.</summary>
        public float Evaluate(float time, float duration, out bool complete)
        {
            if (_mode == PlaybackMode.External)
            {
                complete = false;
                return Mathf.Clamp01(time);
            }

            float localTime = Mathf.Max(0f, time) - DelayBeforeStart;
            if (localTime < 0f)
            {
                complete = false;
                return 0f;
            }

            float forwardDuration = ResolveDuration(duration);
            switch (_mode)
            {
                case PlaybackMode.Loop:
                    return EvaluateRepeated(localTime, forwardDuration, false, out complete);
                case PlaybackMode.PingPong:
                    return EvaluateRepeated(localTime, forwardDuration, true, out complete);
                case PlaybackMode.Weighted:
                    return EvaluateWeighted(localTime, forwardDuration, out complete);
                case PlaybackMode.Once:
                default:
                    if (localTime >= forwardDuration + HoldDuration)
                    {
                        complete = true;
                        return GetCompletedValue(1f);
                    }

                    complete = false;
                    if (localTime >= forwardDuration)
                        return 1f;
                    return Mathf.Clamp01(localTime / forwardDuration);
            }
        }

        /// <summary>Calculates total playback time, including delays, holds, returns, and finite repeat cycles.</summary>
        /// <returns>Total seconds, or positive infinity for unbounded playback.</returns>
        public float GetTotalDuration(float duration = 0f)
        {
            if (IsInfinite)
                return float.PositiveInfinity;

            float forwardDuration = ResolveDuration(duration);
            if (_mode == PlaybackMode.Loop)
                return DelayBeforeStart + ComputeFiniteCycleDuration(forwardDuration);
            if (_mode == PlaybackMode.PingPong)
            {
                float coreDuration = forwardDuration + HoldDuration + ResolveReturnDuration(forwardDuration);
                return DelayBeforeStart + ComputeFiniteCycleDuration(coreDuration);
            }

            return DelayBeforeStart + forwardDuration + HoldDuration;
        }

        void OnValidate()
        {
            _delayBeforeStart = Mathf.Max(0f, _delayBeforeStart);
            _duration = Mathf.Max(0.0001f, _duration);
            _holdDuration = Mathf.Max(0f, _holdDuration);
            _returnDuration = Mathf.Max(0f, _returnDuration);
            _cycleCount = Mathf.Max(0, _cycleCount);
            _delayBetweenCycles = Mathf.Max(0f, _delayBetweenCycles);
            _externalPreviewValue = Mathf.Clamp01(_externalPreviewValue);
            _weightCurve ??= AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        float EvaluateRepeated(
            float localTime,
            float forwardDuration,
            bool pingPong,
            out bool complete)
        {
            float returnDuration = pingPong ? ResolveReturnDuration(forwardDuration) : 0f;
            float coreDuration = forwardDuration + (pingPong ? HoldDuration + returnDuration : 0f);
            int cycles = CycleCount;
            if (cycles > 0)
            {
                float totalDuration = coreDuration * cycles + DelayBetweenCycles * Mathf.Max(0, cycles - 1);
                if (localTime >= totalDuration)
                {
                    complete = true;
                    return pingPong ? 0f : GetCompletedValue(1f);
                }
            }

            complete = false;
            float stride = coreDuration + DelayBetweenCycles;
            float cycleTime = stride > 0f ? Mathf.Repeat(localTime, stride) : 0f;
            if (cycleTime >= coreDuration)
                return pingPong ? 0f : GetCompletedValue(1f);

            if (!pingPong)
                return Mathf.Clamp01(cycleTime / forwardDuration);
            if (cycleTime < forwardDuration)
                return Mathf.Clamp01(cycleTime / forwardDuration);
            if (cycleTime < forwardDuration + HoldDuration)
                return 1f;

            float returnTime = cycleTime - forwardDuration - HoldDuration;
            return 1f - Mathf.Clamp01(returnTime / returnDuration);
        }

        float EvaluateWeighted(float localTime, float forwardDuration, out bool complete)
        {
            AnimationCurve curve = _weightCurve;
            float normalizedTime = Mathf.Clamp01(localTime / forwardDuration);
            if (localTime >= forwardDuration + HoldDuration)
            {
                complete = true;
                return GetCompletedValue(curve != null ? curve.Evaluate(1f) : 1f);
            }

            complete = false;
            if (localTime >= forwardDuration)
                return curve != null ? curve.Evaluate(1f) : 1f;
            return curve != null ? curve.Evaluate(normalizedTime) : normalizedTime;
        }

        float ComputeFiniteCycleDuration(float coreDuration)
        {
            int cycles = Mathf.Max(1, CycleCount);
            return coreDuration * cycles + DelayBetweenCycles * Mathf.Max(0, cycles - 1);
        }

        float ResolveDuration(float duration)
        {
            return duration > 0f ? Mathf.Max(0.0001f, duration) : Duration;
        }

        float ResolveReturnDuration(float forwardDuration)
        {
            return _returnDuration > 0f ? _returnDuration : forwardDuration;
        }

        float GetCompletedValue(float finalValue)
        {
            return _completion == EffectPlaybackCompletion.Reset ? 0f : finalValue;
        }
    }
}
