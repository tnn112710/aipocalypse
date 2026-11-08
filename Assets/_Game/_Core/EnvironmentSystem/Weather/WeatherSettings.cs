using System;
using System.Collections.Generic;
using Ami.BroAudio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Every sky weather and wind level: what COZY draws, how the scene's wind, fog and ambience change, and the sounds.
    // Read by WeatherService; one asset in Data/Environment/Weather.
    [CreateAssetMenu(menuName = "Aipocalypse/Environment/Weather Settings", fileName = "Weather")]
    public sealed class WeatherSettings : ScriptableObject
    {
        [Serializable]
        public sealed class Sky
        {
            [Tooltip("COZY weather profile: our copy in Data/Environment/Cozy/Weather (no COZY audio or thunder)")]
            public ScriptableObject Cozy;
            public WindLevel DefaultWind = WindLevel.Breezy;
            [Min(0f), Tooltip("Multiplies the look's fog density (Unity fog mode; COZY fog follows the profile)")]
            public float FogDensityScale = 1f;
            [Range(0f, 1f), Tooltip("Multiplies the time-of-day ambience (birds, meadow wind, owls)")]
            public float NatureVolume = 1f;
            [Range(0f, 1f), Tooltip("Rain drops, as a share of WeatherRain's full rate. 0 = no rain")] public float RainAmount;
            [Tooltip("Looping bed, e.g. rain. Unset = none")] public SoundID Bed;
            [Range(0f, 1f)] public float BedVolume = 1f;
            [Tooltip("Lightning flashes followed by thunder")] public bool Lightning;
        }

        [Serializable]
        public sealed class Wind
        {
            [Min(0f), Tooltip("Sway amplitude of trees, grass and plants, times the scene's tuned sway (Breezy = 1)")]
            public float Strength = 1f;
            [Min(0f), Tooltip("Sway speed, times the tuned speed")] public float Speed = 1f;
            [Min(0f), Tooltip("Stylized Grass gusts, times the materials' own")] public float Gust = 1f;
            [Tooltip("Looping wind bed. Unset = none (the time-of-day ambience has a light meadow wind)")] public SoundID Bed;
            [Range(0f, 1f)] public float BedVolume = 1f;
        }

        [Tooltip("Off: no weather service; the scene keeps its COZY profile and tuned wind")]
        public bool Enabled = true;
        public SkyWeather StartSky = SkyWeather.PartlyCloudy;
        [Tooltip("Optional: how the weather changes on its own (WeatherSchedule; needs the world clock). Unset or off = only the console and code change it")]
        public WeatherRules Rules;
        [Min(0f), Tooltip("Game seconds a change blends over (pausing holds it)")] public float TransitionSeconds = 20f;
        [Min(0f), Tooltip("Sideways drift of the rain drops (m/s) per unit of wind strength")] public float RainSlant = 0.6f;
        [MinMaxSlider(1f, 120f, true), Tooltip("Game seconds between lightning strikes")] public Vector2 StrikeInterval = new(8f, 25f);
        [MinMaxSlider(0f, 5f, true), Tooltip("Seconds from the flash to its thunder")] public Vector2 ThunderDelay = new(0.3f, 2.5f);
        public SoundID Thunder;
        [Range(0f, 1f)] public float ThunderVolume = 1f;
        // Unity serializes a dictionary only with [SerializeField], public or not.
        [SerializeField] public Dictionary<SkyWeather, Sky> Skies = new();
        [SerializeField] public Dictionary<WindLevel, Wind> Winds = new();
    }
}
