using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    public enum TextStudioSignalSource { NormalizedValue, AudioAmplitude, Beat }
    public enum TextStudioSignalDestination { EffectProgress, EffectIntensity }

    /// <summary>Applies one normalized signal through the shared external-effect contract each runtime frame.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextStudioText))]
    [AddComponentMenu("Text Studio/Animation/Signal Driver")]
    public sealed class TextStudioSignalDriver : MonoBehaviour, ITextStudioCapabilityReportProvider, ITextStudioExternalEffectSource
    {
        [SerializeField] TextStudioEffectDefinitionAsset _effect;
        [SerializeField] TextStudioSignalSource _source;
        [SerializeField] TextStudioSignalDestination _destination;
        [SerializeField, Range(0f, 1f)] float _value;
        [SerializeField, Tooltip("Source sampled into a reusable 256-sample RMS window.")] AudioSource _audioSource;
        [SerializeField, Min(0f)] float _audioGain = 4f;
        [SerializeField, Min(0.01f)] float _beatsPerMinute = 120f;
        [SerializeField, Min(0.01f)] float _beatsPerCycle = 1f;
        [SerializeField, Tooltip("DSP clock origin in seconds. Share the same origin to synchronize several text signals.")]
        double _beatOriginSeconds;
        [SerializeField, Min(0f), Tooltip("Exponential attack time in seconds. Zero follows immediately.")]
        float _attackSeconds;
        [SerializeField, Min(0f), Tooltip("Exponential release time in seconds. Zero follows immediately.")]
        float _releaseSeconds;
        [SerializeField] AnimationCurve _response = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField, Range(0f, 1f), Tooltip("Effect progress used when the signal controls intensity.")]
        float _fixedProgress = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("Maximum contribution weight. The host also applies its motion preference.")]
        float _intensity = 1f;
        TextStudioText _target;
        float[] _samples;
        float _smoothed;
        int _sampleFrame = -1;

        public TextStudioEffectDefinitionAsset Effect { get => _effect; set => _effect = value; }
        public TextStudioSignalSource Source { get => _source; set => _source = value; }
        public TextStudioSignalDestination Destination { get => _destination; set => _destination = value; }
        public float Value { get => _value; set => _value = TextStudioSignal.Normalize(value); }
        public AudioSource AudioSource { get => _audioSource; set => _audioSource = value; }
        public float BeatsPerMinute { get => _beatsPerMinute; set => _beatsPerMinute = value; }
        public double BeatOriginSeconds { get => _beatOriginSeconds; set => _beatOriginSeconds = value; }
        public float CurrentSignal => _smoothed;
        public bool IsActive => Application.isPlaying && isActiveAndEnabled && _effect != null && _intensity > 0f;

        void OnEnable()
        {
            _target = GetComponent<TextStudioText>();
            _smoothed = 0f;
            _sampleFrame = -1;
            if (_target != null) _target.RegisterExternalEffectSource(this);
        }

        void OnDisable()
        {
            if (_target != null)
            {
                _target.UnregisterExternalEffectSource(this);
                _target.ScheduleVisualStateUpdate();
            }
            _target = null;
            _samples = null;
        }

        /// <summary>Samples the active signal once per frame and submits it as effect progress or intensity.</summary>
        public void Submit(in TextStudioExternalEffectSubmissionContext context)
        {
            if (!IsActive) return;
            if (_sampleFrame != Time.frameCount)
            {
                _sampleFrame = Time.frameCount;
                SampleSignal();
            }
            context.Submit(_effect,
                _destination == TextStudioSignalDestination.EffectProgress ? _smoothed : TextStudioSignal.Normalize(_fixedProgress),
                TextStudioSignal.Normalize(_intensity) * (_destination == TextStudioSignalDestination.EffectIntensity ? _smoothed : 1f));
        }

        void SampleSignal()
        {
            float value;
            switch (_source)
            {
                case TextStudioSignalSource.AudioAmplitude:
                    if (_audioSource == null || !_audioSource.isPlaying) value = 0f;
                    else
                    {
                        _samples ??= new float[256];
                        _audioSource.GetOutputData(_samples, 0);
                        value = TextStudioSignal.AudioRms(_samples, _audioGain);
                    }
                    break;
                case TextStudioSignalSource.Beat:
                    value = TextStudioSignal.BeatPhase(AudioSettings.dspTime, _beatsPerMinute, _beatOriginSeconds, _beatsPerCycle);
                    break;
                default: value = TextStudioSignal.Normalize(_value); break;
            }
            float response = TextStudioSignal.Normalize(_response != null ? _response.Evaluate(value) : value);
            _smoothed = TextStudioSignal.Smooth(_smoothed, response, Time.unscaledDeltaTime, _attackSeconds, _releaseSeconds);
        }

        /// <summary>Appends warnings for a missing effect or a missing audio source required by the selected signal.</summary>
        public void CollectCapabilityDiagnostics(List<TextStudioPreviewDiagnostic> diagnostics)
        {
            if (_effect == null)
                diagnostics.Add(new TextStudioPreviewDiagnostic(TextStudioPreviewDiagnosticSeverity.Warning,
                    "Signal Driver has no effect.", "Assign an Effect or Composed Effect asset."));
            if (_source == TextStudioSignalSource.AudioAmplitude && _audioSource == null)
                diagnostics.Add(new TextStudioPreviewDiagnostic(TextStudioPreviewDiagnosticSeverity.Warning,
                    "Signal Driver has no audio source.", "Assign the AudioSource whose amplitude should drive the text."));
        }
    }
}
