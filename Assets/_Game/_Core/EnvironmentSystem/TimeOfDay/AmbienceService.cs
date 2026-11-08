using System;
using Aipocalypse.Core.AudioSystem;
using Aipocalypse.Core.TimeSystem;
using Aipocalypse.Core.WorldTimeSystem;
using Ami.BroAudio;
using UnityEngine;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Plays TimeOfDayAmbience against the world clock. Beds start once and follow their curves; calls wait a random interval.
    public sealed class AmbienceService : IStartable, ITickable, IDisposable
    {
        const float UpdateInterval = 0.25f;
        const float MinCallInterval = 1f;

        readonly TimeOfDayAmbience _ambience;
        readonly WorldClock _clock;
        readonly AudioService _audio;
        readonly TimeService _time;
        IAudioPlayer[] _beds = Array.Empty<IAudioPlayer>();
        float[] _volumes = Array.Empty<float>();
        float[] _callTimers = Array.Empty<float>();
        float _sinceUpdate;

        // Set by the weather (birds go quiet in rain): multiplies every bed and call. 1 without weather.
        public float NatureVolume { get; set; } = 1f;

        public AmbienceService(TimeOfDayAmbience ambience, WorldClock clock, AudioService audio, TimeService time)
        {
            _ambience = ambience;
            _clock = clock;
            _audio = audio;
            _time = time;
        }

        public void Start()
        {
            float hour = _clock.Now.Hour;
            int count = _ambience.Beds.Length;
            _beds = new IAudioPlayer[count];
            _volumes = new float[count];
            for (int i = 0; i < count; i++)
            {
                TimeOfDayAmbience.Bed bed = _ambience.Beds[i];
                _volumes[i] = BedVolume(bed, hour);
                _beds[i] = _audio.Start(bed.Sound, _volumes[i], _ambience.VolumeFade);
            }

            _callTimers = new float[_ambience.OneShots.Length];
            for (int i = 0; i < _callTimers.Length; i++)
                _callTimers[i] = NextInterval(_ambience.OneShots[i]);
        }

        public void Tick()
        {
            float deltaTime = Time.unscaledDeltaTime;
            float hour = _clock.Now.Hour;

            _sinceUpdate += deltaTime;
            if (_sinceUpdate >= UpdateInterval)
            {
                _sinceUpdate = 0f;
                for (int i = 0; i < _beds.Length; i++)
                {
                    float volume = BedVolume(_ambience.Beds[i], hour);
                    if (Mathf.Abs(volume - _volumes[i]) < 0.01f)
                        continue;
                    _volumes[i] = volume;
                    _audio.SetVolume(_beds[i], volume, _ambience.VolumeFade);
                }
            }

            // A fast-forward would fire every call at once; a paused game shouldn't call at all.
            if (_clock.IsSkipping || _time.IsPaused)
                return;
            for (int i = 0; i < _callTimers.Length; i++)
            {
                TimeOfDayAmbience.Calls calls = _ambience.OneShots[i];
                float weight = calls.Hours.Weight(hour);
                if (weight <= 0f)
                    continue;
                _callTimers[i] -= deltaTime;
                if (_callTimers[i] > 0f)
                    continue;
                _callTimers[i] = NextInterval(calls);
                _audio.Play(calls.Sound, calls.Volume * weight * NatureVolume);
            }
        }

        public void Dispose()
        {
            foreach (IAudioPlayer bed in _beds)
                _audio.Stop(bed, _ambience.StopFade);
        }

        float BedVolume(in TimeOfDayAmbience.Bed bed, float hour) =>
            (bed.Volume != null && bed.Volume.length > 0 ? Mathf.Clamp01(bed.Volume.Evaluate(hour)) : 1f) * NatureVolume;

        // At least the slider's 1 s: a new entry's (0, 0) would otherwise play the call every frame.
        static float NextInterval(in TimeOfDayAmbience.Calls calls)
        {
            float min = Mathf.Max(MinCallInterval, calls.Interval.x);
            return Random.Range(min, Mathf.Max(min, calls.Interval.y));
        }
    }
}
