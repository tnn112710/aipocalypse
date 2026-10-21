using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // One look of the day (sunrise, noon, midnight…). A TimeOfDayProfile places presets at hours and blends between them.
    // Sun direction is not here: the profile's sun path moves the sun; this only says how it looks.
    [CreateAssetMenu(menuName = "Aipocalypse/Environment/Lighting Preset", fileName = "TOD_")]
    public sealed class LightingPreset : ScriptableObject
    {
        [BoxGroup("Sun")] public Color SunColor = new(1f, 0.87f, 0.74f);
        [BoxGroup("Sun"), Min(0f)] public float SunIntensity = 2.8f;
        [BoxGroup("Sun"), Range(0f, 1f)] public float SunShadows = 1f;

        [BoxGroup("Moon"), Tooltip("Moonlight takes over the same light while the sun is down")]
        public Color MoonColor = new(0.55f, 0.65f, 0.95f);
        [BoxGroup("Moon"), Min(0f), Tooltip("At full moon; a new moon gives a third of it")]
        public float MoonIntensity = 0.4f;
        [BoxGroup("Moon"), Range(0f, 1f)] public float MoonShadows = 0.6f;

        [BoxGroup("Ambient"), ColorUsage(false, true)] public Color AmbientSky = new(0.64f, 0.73f, 0.86f);
        [BoxGroup("Ambient"), ColorUsage(false, true)] public Color AmbientEquator = new(0.56f, 0.57f, 0.50f);
        [BoxGroup("Ambient"), ColorUsage(false, true)] public Color AmbientGround = new(0.42f, 0.39f, 0.31f);
        [BoxGroup("Ambient"), Range(0f, 1f), Tooltip("Sky reflections on shiny surfaces; lower at night so they don't reflect a daytime sky")]
        public float ReflectionIntensity = 1f;

        [BoxGroup("Fog")] public Color FogColor = new(0.58f, 0.76f, 0.94f);
        [BoxGroup("Fog"), Min(0f)] public float FogDensity = 0.0011f;

        [BoxGroup("Sky"), Tooltip("Tint of the painted sky texture")] public Color SkyTint = new(0.462f, 0.481f, 0.502f);
        [BoxGroup("Sky"), Min(0f)] public float SkyExposure = 0.31f;
        [BoxGroup("Sky"), ColorUsage(false, true)] public Color NightSkyTop = new(0.01f, 0.015f, 0.04f);
        [BoxGroup("Sky"), ColorUsage(false, true)] public Color NightSkyHorizon = new(0.04f, 0.06f, 0.12f);
        [BoxGroup("Sky"), Range(0f, 1f), Tooltip("0 = painted sky only, 1 = the night gradient replaces it")]
        public float NightSkyBlend;
        [BoxGroup("Sky"), ColorUsage(false, true), Tooltip("Warm band on the horizon toward the sun (sunrise, sunset)")]
        public Color HorizonGlow = Color.black;
        [BoxGroup("Sky"), Min(0f)] public float SunDisc = 1f;
        [BoxGroup("Sky"), Min(0f)] public float Stars;
        [BoxGroup("Sky"), Min(0f)] public float MoonGlow;

        [BoxGroup("Scene"), Tooltip("Height haze of the far mountains (their shaders skip scene fog)")]
        public Color HazeColor = new(0.30f, 0.42f, 0.55f);
        [BoxGroup("Scene"), Tooltip("Multiplies unlit effects: mist, haze banks, clouds, god rays")]
        public Color UnlitTint = Color.white;

        public AtmosphereState ToState() => new()
        {
            SunColor = SunColor,
            SunIntensity = SunIntensity,
            SunShadows = SunShadows,
            MoonColor = MoonColor,
            MoonIntensity = MoonIntensity,
            MoonShadows = MoonShadows,
            AmbientSky = AmbientSky,
            AmbientEquator = AmbientEquator,
            AmbientGround = AmbientGround,
            ReflectionIntensity = ReflectionIntensity,
            FogColor = FogColor,
            FogDensity = FogDensity,
            SkyTint = SkyTint,
            SkyExposure = SkyExposure,
            NightSkyTop = NightSkyTop,
            NightSkyHorizon = NightSkyHorizon,
            NightSkyBlend = NightSkyBlend,
            HorizonGlow = HorizonGlow,
            SunDisc = SunDisc,
            Stars = Stars,
            MoonGlow = MoonGlow,
            HazeColor = HazeColor,
            UnlitTint = UnlitTint,
        };
    }
}
