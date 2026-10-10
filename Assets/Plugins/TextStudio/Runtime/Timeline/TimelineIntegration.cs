#if USE_TIMELINE
using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace TextStudio.Timeline
{
    /// <summary>
    /// Timeline track that controls text reveal on a TextStudioText hub.
    /// </summary>
    [TrackColor(0.2f, 0.8f, 0.4f)]
    [TrackClipType(typeof(TextRevealClip))]
    [TrackBindingType(typeof(TextStudioText))]
    public class TextRevealTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject owner, int inputCount)
        {
            ScriptPlayable<TextRevealMixerBehaviour> mixer = ScriptPlayable<TextRevealMixerBehaviour>.Create(graph, inputCount);
            mixer.GetBehaviour().ConfigureClockOwner(graph.GetResolver() as PlayableDirector, this);
            return mixer;
        }
    }

    /// <summary>
    /// Data carried by a TextRevealClip. The track mixer owns hub mutations so overlapping clips
    /// can resolve text and reveal progress exactly once per Timeline frame.
    /// </summary>
    [Serializable]
    public class TextRevealBehaviour : PlayableBehaviour
    {
        [NonSerialized] public string text;
        [NonSerialized] public AnimationCurve revealCurve;
        [NonSerialized] public bool autoSkipOnEnd;
    }

    /// <summary>
    /// Deterministically blends reveal clips and distinguishes playback completion from scrubbing.
    /// </summary>
    internal sealed class TextRevealMixerBehaviour : PlayableBehaviour, ITextStudioRevealClockOwner
    {
        const float MinimumWeight = 0.0001f;
        const double CompletionEpsilon = 0.000001d;

        double[] _previousTimes = Array.Empty<double>();
        bool[] _wasActive = Array.Empty<bool>();
        bool[] _completionSent = Array.Empty<bool>();
        PlayableDirector _director;
        TextRevealTrack _track;
        TextStudioText _lastHub;

        internal void ConfigureClockOwner(PlayableDirector director, TextRevealTrack track)
        {
            _director = director;
            _track = track;
        }

        public bool ControlsRevealClock(TextStudioText target)
        {
            // Unbound outputs can stop evaluating without a final ProcessFrame callback.
            return _director != null && _director.isActiveAndEnabled && _track != null &&
                   _director.GetGenericBinding(_track) == target;
        }
        int _lastTextOwner = -1;
        string _lastAppliedText = string.Empty;

        public override void OnPlayableCreate(Playable playable)
        {
            EnsureCapacity(playable.GetInputCount());
        }

        public override void OnGraphStart(Playable playable)
        {
            ResetState(playable.GetInputCount());
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            TextStudioText hub = playerData as TextStudioText;
            if (hub == null)
            {
                ReleaseRevealClock();
                return;
            }

            int inputCount = playable.GetInputCount();
            EnsureCapacity(inputCount);
            if (_lastHub != hub)
            {
                ReleaseRevealClock();
                _lastHub = hub;
                _lastTextOwner = -1;
                _lastAppliedText = string.Empty;
            }

            float totalWeight = 0f;
            float weightedProgress = 0f;
            float textOwnerWeight = -1f;
            int textOwner = -1;
            string ownerText = string.Empty;
            bool autoSkip = false;
            int activeInputCount = 0;
            int activeCompletingInputCount = 0;

            for (int i = 0; i < inputCount; i++)
            {
                Playable input = playable.GetInput(i);
                if (!input.IsValid())
                    continue;

                ScriptPlayable<TextRevealBehaviour> revealPlayable =
                    (ScriptPlayable<TextRevealBehaviour>)input;
                TextRevealBehaviour behaviour = revealPlayable.GetBehaviour();
                float weight = Mathf.Clamp01(playable.GetInputWeight(i) * info.weight);
                double time = input.GetTime();
                double duration = input.GetDuration();
                bool active = weight > MinimumWeight;

                if (active)
                {
                    activeInputCount++;
                    float progress = EvaluateProgress(behaviour.revealCurve, time, duration);
                    totalWeight += weight;
                    weightedProgress += progress * weight;

                    if (!string.IsNullOrEmpty(behaviour.text) &&
                        IsPreferredTextOwner(textOwner, textOwnerWeight, i, weight))
                    {
                        textOwner = i;
                        textOwnerWeight = weight;
                        ownerText = behaviour.text;
                    }
                }

                if (time < duration - CompletionEpsilon)
                    _completionSent[i] = false;

                if (!_completionSent[i] &&
                    behaviour.autoSkipOnEnd &&
                    ShouldAutoSkip(
                        _previousTimes[i],
                        time,
                        duration,
                        _wasActive[i],
                        info.evaluationType == FrameData.EvaluationType.Playback,
                        info.seekOccurred,
                        info.effectiveSpeed))
                {
                    autoSkip = true;
                    _completionSent[i] = true;
                    if (active)
                        activeCompletingInputCount++;
                }

                _previousTimes[i] = time;
                _wasActive[i] = active;
            }

            if (textOwner >= 0 &&
                (textOwner != _lastTextOwner ||
                 !string.Equals(ownerText, _lastAppliedText, StringComparison.Ordinal)))
            {
                hub.SetText(ownerText);
                _lastTextOwner = textOwner;
                _lastAppliedText = ownerText;
            }
            else if (textOwner < 0)
            {
                _lastTextOwner = -1;
                _lastAppliedText = string.Empty;
            }

            if (totalWeight > MinimumWeight)
                hub.SampleExternalRevealProgress(this, Mathf.Clamp01(weightedProgress / totalWeight));
            else
                hub.ReleaseExternalRevealClock(this);

            if (ShouldApplyAutoSkip(
                    autoSkip,
                    activeInputCount,
                    activeCompletingInputCount))
            {
                hub.SampleExternalRevealProgress(this, 1f);
            }
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            ReleaseRevealClock();
        }

        void ReleaseRevealClock()
        {
            if (_lastHub != null) _lastHub.ReleaseExternalRevealClock(this);
            _lastHub = null;
        }

        internal static float EvaluateProgress(AnimationCurve curve, double time, double duration)
        {
            float normalized = ResolveNormalizedTime(time, duration);
            return Mathf.Clamp01(curve != null ? curve.Evaluate(normalized) : normalized);
        }

        internal static float ResolveNormalizedTime(double time, double duration)
        {
            if (double.IsNaN(time) || double.IsInfinity(time))
                return 0f;
            if (duration <= CompletionEpsilon ||
                double.IsNaN(duration) ||
                double.IsInfinity(duration))
            {
                return time >= 0d ? 1f : 0f;
            }

            return Mathf.Clamp01((float)(time / duration));
        }

        internal static bool IsPreferredTextOwner(
            int currentOwner,
            float currentWeight,
            int candidateOwner,
            float candidateWeight)
        {
            return currentOwner < 0 ||
                   candidateWeight > currentWeight + MinimumWeight ||
                   (Mathf.Abs(candidateWeight - currentWeight) <= MinimumWeight &&
                    candidateOwner > currentOwner);
        }

        internal static bool ShouldAutoSkip(
            double previousTime,
            double currentTime,
            double duration,
            bool wasActive,
            bool isPlaybackEvaluation,
            bool seekOccurred,
            float effectiveSpeed)
        {
            if (!wasActive ||
                !isPlaybackEvaluation ||
                seekOccurred ||
                effectiveSpeed <= 0f ||
                double.IsNaN(previousTime) ||
                double.IsNaN(currentTime) ||
                double.IsNaN(duration) ||
                double.IsInfinity(duration) ||
                duration <= CompletionEpsilon)
            {
                return false;
            }

            return previousTime < duration - CompletionEpsilon &&
                   currentTime >= duration - CompletionEpsilon &&
                   currentTime + CompletionEpsilon >= previousTime;
        }

        internal static bool ShouldApplyAutoSkip(
            bool completionRequested,
            int activeInputCount,
            int activeCompletingInputCount)
        {
            return completionRequested &&
                   (activeInputCount == 0 ||
                    activeInputCount == activeCompletingInputCount);
        }

        void EnsureCapacity(int inputCount)
        {
            if (_previousTimes.Length == inputCount)
                return;

            _previousTimes = new double[inputCount];
            _wasActive = new bool[inputCount];
            _completionSent = new bool[inputCount];
            for (int i = 0; i < inputCount; i++)
                _previousTimes[i] = double.NaN;
        }

        void ResetState(int inputCount)
        {
            EnsureCapacity(inputCount);
            for (int i = 0; i < _previousTimes.Length; i++)
            {
                _previousTimes[i] = double.NaN;
                _wasActive[i] = false;
                _completionSent[i] = false;
            }

            ReleaseRevealClock();
            _lastTextOwner = -1;
            _lastAppliedText = string.Empty;
        }
    }

    /// <summary>
    /// Data carried by a TextEffectClip. The track mixer performs all hub mutations.
    /// </summary>
    [Serializable]
    public class TextEffectBehaviour : PlayableBehaviour
    {
        [NonSerialized] public TextStudioEffectDefinitionAsset effect;
        [NonSerialized] public AnimationCurve intensityCurve;
        [NonSerialized] public TextStudioTextRange range;
    }

    /// <summary>
    /// Applies each active effect clip once using Timeline's effective clip and track weight.
    /// </summary>
    internal sealed class TextEffectMixerBehaviour : PlayableBehaviour, ITextStudioExternalEffectSample
    {
        const float MinimumWeight = 0.0001f;
        PlayableDirector _director;
        TextEffectTrack _track;
        TextStudioText _target;
        EffectSample[] _samples = Array.Empty<EffectSample>();
        int _sampleCount;

        struct EffectSample
        {
            public TextStudioEffectDefinitionAsset Effect;
            public float Progress;
            public float Intensity;
            public TextStudioTextRange Range;
        }

        internal void ConfigureSampleOwner(PlayableDirector director, TextEffectTrack track)
        {
            _director = director;
            _track = track;
        }

        public bool ApplySample(TextStudioText target)
        {
            if (_sampleCount == 0 || _director == null || !_director.isActiveAndEnabled ||
                _track == null || _director.GetGenericBinding(_track) != target) return false;
            for (int i = 0; i < _sampleCount; i++)
            {
                EffectSample sample = _samples[i];
                target.ApplyExternalEffect(sample.Effect, sample.Progress, sample.Intensity, sample.Range);
            }
            return true;
        }

        public override void OnPlayableDestroy(Playable playable) => ReleaseSample();

        void ReleaseSample()
        {
            if (_target != null) _target.ReleaseExternalEffectSample(this);
            _target = null;
            _sampleCount = 0;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            TextStudioText hub = playerData as TextStudioText;
            if (hub != _target) ReleaseSample();
            if (hub == null) return;
            _target = hub;
            _sampleCount = 0;

            int inputCount = playable.GetInputCount();
            if (_samples.Length < inputCount) _samples = new EffectSample[inputCount];
            for (int i = 0; i < inputCount; i++)
            {
                float weight = Mathf.Clamp01(playable.GetInputWeight(i) * info.weight);
                if (weight <= MinimumWeight)
                    continue;

                Playable input = playable.GetInput(i);
                if (!input.IsValid())
                    continue;

                ScriptPlayable<TextEffectBehaviour> effectPlayable =
                    (ScriptPlayable<TextEffectBehaviour>)input;
                TextEffectBehaviour behaviour = effectPlayable.GetBehaviour();
                if (behaviour.effect == null)
                    continue;

                float normalizedTime = TextRevealMixerBehaviour.ResolveNormalizedTime(
                    input.GetTime(),
                    input.GetDuration());
                float authoredIntensity = behaviour.intensityCurve != null
                    ? behaviour.intensityCurve.Evaluate(normalizedTime)
                    : 1f;
                float intensity = ResolveEffectiveIntensity(authoredIntensity, weight);
                if (intensity <= MinimumWeight)
                    continue;

                _samples[_sampleCount++] = new EffectSample
                {
                    Effect = behaviour.effect,
                    Progress = normalizedTime,
                    Intensity = intensity,
                    Range = behaviour.range
                };
            }
            if (_sampleCount > 0) hub.RegisterExternalEffectSample(this);
            else hub.ReleaseExternalEffectSample(this);
        }

        internal static float ResolveEffectiveIntensity(float authoredIntensity, float effectiveWeight)
        {
            if (float.IsNaN(authoredIntensity) ||
                float.IsInfinity(authoredIntensity) ||
                float.IsNaN(effectiveWeight) ||
                float.IsInfinity(effectiveWeight))
            {
                return 0f;
            }

            return Mathf.Clamp01(authoredIntensity) * Mathf.Clamp01(effectiveWeight);
        }
    }
}
#endif
