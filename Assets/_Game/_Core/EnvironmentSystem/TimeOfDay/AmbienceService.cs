using System;
using Aipocalypse.Core.AudioSystem;
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

        readonly TimeOfDayAmbience _ambience;
        readonly WorldClock _clock;
        readonly AudioService _audio;
        IAudioPlayer[] _beds = Array.Empty<IAudioPlayer>();
        float[] _volumes = Array.Empty<float>();
        float[] _callTimers = Array.Empty<float>();
        float _sinceUpdate;

        public AmbienceService(TimeOfDayAmbience ambience, WorldClock clock, AudioService audio)
        {
            _ambience = ambience;
            _clock = clock;
            _audio = audio;
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
            if (_clock.IsSkipping || Time.timeScale <= 0f)
                return;
            for (int i = 0; i < _callTimers.Length; i++)
            {
                TimeOfDayAmbience.Calls calls = _ambience.OneShots[i];
                if (!calls.Hours.Contains(hour))
                    continue;
                _callTimers[i] -= deltaTime;
                if (_callTimers[i] > 0f)
                    continue;
                _callTimers[i] = NextInterval(calls);
                _audio.Play(calls.Sound, calls.Volume * calls.Hours.Weight(hour));
            }
        }

        public void Dispose()
        {
            foreach (IAudioPlayer bed in _beds)
                _audio.Stop(bed, 1f);
        }

        static float BedVolume(in TimeOfDayAmbience.Bed bed, float hour) =>
            bed.Volume != null && bed.Volume.length > 0 ? Mathf.Clamp01(bed.Volume.Evaluate(hour)) : 1f;

        static float NextInterval(in TimeOfDayAmbience.Calls calls) => Random.Range(calls.Interval.x, Mathf.Max(calls.Interval.x, calls.Interval.y));
    }
}
