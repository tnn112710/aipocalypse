using System;
using Aipocalypse.Core.AudioSystem;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Holds the weather (a sky weather + a wind level) and shows it: COZY's sky through the bridge, the scene wind, Unity fog,
    // the ambience's nature volume, rain / wind beds and lightning. Changes blend over game seconds, so pausing holds them.
    // Nothing decides the weather on its own yet: the console and later rules call Set.
    public sealed class WeatherService : IStartable, ITickable, IDisposable
    {
        const float AudioInterval = 0.25f;

        readonly WeatherSettings _settings;
        readonly AudioService _audio;
        readonly IPublisher<WeatherChanged> _changed;
        readonly WeatherBlend _blend;
        readonly WeatherAudio _beds;
        readonly float[] _bedTargets;
        EnvironmentDirector _director;
        WeatherWind _wind;
        LightningFlash _flash;
        AmbienceService _ambience;
        WorldClock _clock;
        float _sinceAudio;
        float _nextStrike;
        // Counts down to the thunder of the last flash; negative = none pending.
        float _thunderIn = -1f;

        public WeatherService(WeatherSettings settings, AudioService audio, IPublisher<WeatherChanged> changed, IObjectResolver resolver)
        {
            _settings = settings;
            _audio = audio;
            _changed = changed;
            _beds = new WeatherAudio(settings, audio);
            _blend = new WeatherBlend(_beds.Count);
            _bedTargets = new float[_beds.Count];
            resolver.TryResolve(out _director);
            resolver.TryResolve(out _wind);
            resolver.TryResolve(out _flash);
            resolver.TryResolve(out _ambience);
            resolver.TryResolve(out _clock);
        }

        public SkyWeather Sky { get; private set; }
        // Set by Set / SetWind; null = the sky weather's default wind.
        public WindLevel? WindOverride { get; private set; }
        public WindLevel Wind => WindOverride ?? SkySettings(Sky)?.DefaultWind ?? WindLevel.Breezy;
        public WeatherState State => new(Sky, WindOverride);
        // 0 → 1 over the current change; 1 when settled.
        public float Progress => _blend.Progress;
        public ref readonly WeatherBlend.Mix Mix => ref _blend.Now;

        public void Start() => Restore(new WeatherState(_settings.StartSky));

        // wind null = the sky weather's default; seconds null = the settings' transition, 0 = cut.
        public void Set(SkyWeather sky, WindLevel? wind = null, float? seconds = null)
        {
            bool changed = sky != Sky || wind != WindOverride;
            Sky = sky;
            WindOverride = wind;
            Show(seconds ?? _settings.TransitionSeconds, changed);
        }

        public void SetWind(WindLevel? wind) => Set(Sky, wind);

        // Cuts to a saved weather without a transition.
        public void Restore(WeatherState state)
        {
            Sky = state.Sky;
            WindOverride = state.WindOverride;
            Show(0f, true);
        }

        WeatherSettings.Sky SkySettings(SkyWeather sky) => _settings.Skies.TryGetValue(sky, out WeatherSettings.Sky value) ? value : null;

        WeatherSettings.Wind WindSettings(WindLevel level) => _settings.Winds.TryGetValue(level, out WeatherSettings.Wind value) ? value : null;

        void Show(float seconds, bool changed)
        {
            WeatherSettings.Sky sky = SkySettings(Sky);
            WeatherSettings.Wind wind = WindSettings(Wind);
            var target = new WeatherBlend.Mix
            {
                Strength = wind?.Strength ?? 1f,
                Speed = wind?.Speed ?? 1f,
                Gust = wind?.Gust ?? 1f,
                FogScale = sky?.FogDensityScale ?? 1f,
                Nature = sky?.NatureVolume ?? 1f,
                Lightning = sky != null && sky.Lightning ? 1f : 0f,
            };
            _beds.Targets(sky, wind, _bedTargets);
            _blend.Start(target, _bedTargets, seconds);

            CozyBridge cozy = _director != null ? _director.Cozy : null;
            if (cozy != null && sky != null)
                cozy.SetWeather(sky.Cozy, seconds);
            Push();
            _beds.Show(_blend.Beds, seconds > 0f ? AudioInterval : 0f);
            _sinceAudio = 0f;
            if (changed)
                _changed.Publish(new WeatherChanged());
        }

        public void Tick() => Advance(Time.deltaTime);

        // Game seconds: zero while paused.
        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;
            if (_blend.Progress < 1f)
            {
                _blend.Advance(deltaTime);
                Push();
                _sinceAudio += deltaTime;
                if (_sinceAudio >= AudioInterval || _blend.Progress >= 1f)
                {
                    _sinceAudio = 0f;
                    _beds.Show(_blend.Beds, AudioInterval);
                }
            }
            Lightning(deltaTime);
        }

        void Push()
        {
            ref readonly WeatherBlend.Mix mix = ref _blend.Now;
            if (_wind != null)
                _wind.Set(mix.Strength, mix.Speed, mix.Gust);
            if (_director != null)
            {
                _director.WeatherFogScale = mix.FogScale;
                if (_director.Cozy != null)
                    _director.Cozy.SetRainWind(WindDirection * (mix.Strength * _settings.RainSlant));
            }
            if (_ambience != null)
                _ambience.NatureVolume = mix.Nature;
        }

        Vector3 WindDirection => _wind != null ? _wind.Direction : Vector3.forward;

        // While the storm is at least half in: a flash now and then, its thunder a moment later. None while skipping time.
        void Lightning(float deltaTime)
        {
            if (_thunderIn >= 0f)
            {
                _thunderIn -= deltaTime;
                if (_thunderIn < 0f)
                    _audio.Play(_settings.Thunder, _settings.ThunderVolume);
            }

            if (_blend.Now.Lightning < 0.5f || (_clock != null && _clock.IsSkipping))
            {
                _nextStrike = 0f;
                return;
            }
            if (_nextStrike <= 0f)
            {
                _nextStrike = Random.Range(_settings.StrikeInterval.x, _settings.StrikeInterval.y);
                return;
            }
            _nextStrike -= deltaTime;
            if (_nextStrike > 0f)
                return;
            _nextStrike = Random.Range(_settings.StrikeInterval.x, _settings.StrikeInterval.y);
            Strike();
        }

        public void Strike()
        {
            if (_flash != null)
                _flash.Strike();
            _thunderIn = Random.Range(_settings.ThunderDelay.x, _settings.ThunderDelay.y);
        }

        public void Dispose()
        {
            _beds.Dispose();
            if (_ambience != null)
                _ambience.NatureVolume = 1f;
        }
    }
}
