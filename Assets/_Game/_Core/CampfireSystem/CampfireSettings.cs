using System.Collections.Generic;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.CampfireSystem
{
    // Tuning for campfires (docs/technicals/CAMPFIRE.md): fuel, rain, how far the fire lights and keeps the player sane, the
    // light's flicker and the yard wood pile. Read by code; runtime never writes it.
    [CreateAssetMenu(menuName = "Aipocalypse/Campfire Settings")]
    public sealed class CampfireSettings : ScriptableObject
    {
        [Header("Fuel")]
        [Min(0.1f), Tooltip("Game hours of fuel the pit holds (4 logs of 2 h; an upgrade raises it later)")]
        public float MaxFuel = 8f;
        [Range(0f, 1f), Tooltip("Share of the max below which the flame, light and safe radius shrink with the fuel")]
        public float ShrinkBelow = 0.25f;
        [Range(0f, 1f), Tooltip("Share of the max below which the HUD fuel bar shows while lit")]
        public float WarnBelow = 0.35f;
        [Min(0f), Tooltip("Metres (flat) from the fire within which the HUD fuel bar shows while the fire is out in the dark (far away there's nothing to do about it)")]
        public float FuelBarRange = 60f;
        [SerializeField, Tooltip("How much faster a lit fire burns in each sky weather (missing = × 1)")]
        public Dictionary<SkyWeather, float> RainBurn = new()
        {
            [SkyWeather.LightRain] = 1.25f,
            [SkyWeather.MediumRain] = 1.5f,
            [SkyWeather.HeavyRain] = 2f,
        };

        [Header("Light")]
        [Min(0f), Tooltip("Core light range at full strength: the hot pool around the pit")]
        public float CoreRange = 8f;
        [Min(0f), Tooltip("Metres (flat) inside which the player is safe from the dark at full strength (the SafeLight radius)")]
        public float SafeRadius = 30f;
        [Min(0f), Tooltip("Core light intensity at full strength")]
        public float CoreIntensity = 6f;
        [Min(0f), Tooltip("Fill light intensity at full strength (it hangs high above the pit, so it lights the yard evenly, ~20 m)")]
        public float FillIntensity = 50f;
        [Min(0f), Tooltip("Fill light range at full strength")]
        public float FillRange = 42f;
        [Range(0f, 1f), Tooltip("How far the flicker dips the lights (0.3 = down to 70 %)")]
        public float FlickerAmount = 0.3f;
        [Range(0f, 1f), Tooltip("Share of the flicker the fill light gets (the core gets all of it): the whole yard pulsing reads as a strobe")]
        public float FillFlicker = 0.25f;
        [Min(0f), Tooltip("Flicker speed (noise per second)")]
        public float FlickerSpeed = 6f;
        [Min(0f), Tooltip("How much brighter the lights get for a moment when a log goes on (0.6 = +60 %)")]
        public float FlareBoost = 0.6f;
        [Min(1f), Tooltip("The embers' rate at the top of that flare, × the flame's own (sparks fly up)")]
        public float FlareEmbers = 6f;
        [Min(0.01f), Tooltip("Real seconds the flare takes to settle")]
        public float FlareSeconds = 0.8f;
        [Min(0f), Tooltip("Real seconds the fire takes to flare up when lit or die down when put out")]
        public float FadeSeconds = 1.2f;
        [Range(0f, 1f), Tooltip("Flame size at the lowest fuel, as a share of full (embers, so a dying fire still shows)")]
        public float MinFlameScale = 0.35f;

        [Header("Sound")]
        [Min(0.5f), Tooltip("Seconds between sputters while a lit fire is below ShrinkBelow (random in this range): it's dying")]
        public float SputterEveryMin = 4f;
        [Min(0.5f)] public float SputterEveryMax = 9f;

        [Header("Wood pile")]
        [Min(0), Tooltip("Logs in the yard pile each morning (a stand-in for chopping)")]
        public int PileSize = 8;

        public float BurnScale(SkyWeather sky) => RainBurn != null && RainBurn.TryGetValue(sky, out float scale) ? Mathf.Max(0f, scale) : 1f;
    }
}
