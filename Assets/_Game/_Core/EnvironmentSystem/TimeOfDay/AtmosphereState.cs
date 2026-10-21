using UnityEngine;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Every value the environment director applies in a frame: the time-of-day look, then each atmosphere layer's changes.
    public struct AtmosphereState
    {
        public Color SunColor;
        public float SunIntensity;
        public float SunShadows;
        public Color MoonColor;
        public float MoonIntensity;
        public float MoonShadows;

        public Color AmbientSky;
        public Color AmbientEquator;
        public Color AmbientGround;
        public float ReflectionIntensity;

        public Color FogColor;
        public float FogDensity;

        public Color SkyTint;
        public float SkyExposure;
        public Color NightSkyTop;
        public Color NightSkyHorizon;
        public float NightSkyBlend;
        public Color HorizonGlow;
        public float SunDisc;
        public float Stars;
        public float MoonGlow;

        // Far mountains' own height haze (their shaders skip scene fog).
        public Color HazeColor;
        // Multiplies unlit effects (mist, haze banks, clouds, god rays) so they don't glow at night.
        public Color UnlitTint;

        public static AtmosphereState Lerp(in AtmosphereState a, in AtmosphereState b, float t) => new()
        {
            SunColor = Color.Lerp(a.SunColor, b.SunColor, t),
            SunIntensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, t),
            SunShadows = Mathf.Lerp(a.SunShadows, b.SunShadows, t),
            MoonColor = Color.Lerp(a.MoonColor, b.MoonColor, t),
            MoonIntensity = Mathf.Lerp(a.MoonIntensity, b.MoonIntensity, t),
            MoonShadows = Mathf.Lerp(a.MoonShadows, b.MoonShadows, t),
            AmbientSky = Color.Lerp(a.AmbientSky, b.AmbientSky, t),
            AmbientEquator = Color.Lerp(a.AmbientEquator, b.AmbientEquator, t),
            AmbientGround = Color.Lerp(a.AmbientGround, b.AmbientGround, t),
            ReflectionIntensity = Mathf.Lerp(a.ReflectionIntensity, b.ReflectionIntensity, t),
            FogColor = Color.Lerp(a.FogColor, b.FogColor, t),
            FogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, t),
            SkyTint = Color.Lerp(a.SkyTint, b.SkyTint, t),
            SkyExposure = Mathf.Lerp(a.SkyExposure, b.SkyExposure, t),
            NightSkyTop = Color.Lerp(a.NightSkyTop, b.NightSkyTop, t),
            NightSkyHorizon = Color.Lerp(a.NightSkyHorizon, b.NightSkyHorizon, t),
            NightSkyBlend = Mathf.Lerp(a.NightSkyBlend, b.NightSkyBlend, t),
            HorizonGlow = Color.Lerp(a.HorizonGlow, b.HorizonGlow, t),
            SunDisc = Mathf.Lerp(a.SunDisc, b.SunDisc, t),
            Stars = Mathf.Lerp(a.Stars, b.Stars, t),
            MoonGlow = Mathf.Lerp(a.MoonGlow, b.MoonGlow, t),
            HazeColor = Color.Lerp(a.HazeColor, b.HazeColor, t),
            UnlitTint = Color.Lerp(a.UnlitTint, b.UnlitTint, t),
        };
    }
}
