using UnityEngine;

namespace MB13.Aipocalypse.Core.EnvironmentSystem
{
    // EnvironmentDirector's own light and sky, used when COZY doesn't draw: the one shadowed sun / moon light, turned in
    // glides so shadows don't crawl, and the sky shader globals (Time Of Day sky, Skybox Plus and its Play-mode material copy,
    // star map). The settings stay serialized on the director and come in with each call.
    public sealed class OwnSky
    {
        // The director's sky settings, read on every apply so Inspector edits show at once.
        public struct SkySettings
        {
            public float SkyboxPlusDuskElevation;
            public float SkyboxPlusMorningSink;
            public float SkyboxPlusEveningSink;
            public float StarLatitude;
            public float StarAzimuth;
            public float GalacticCoreTransit;
        }

        static readonly int SkyTintId = Shader.PropertyToID("_TOD_SkyTint");
        static readonly int SkyExposureId = Shader.PropertyToID("_TOD_SkyExposure");
        static readonly int NightTopId = Shader.PropertyToID("_TOD_NightTop");
        static readonly int NightHorizonId = Shader.PropertyToID("_TOD_NightHorizon");
        static readonly int NightBlendId = Shader.PropertyToID("_TOD_NightBlend");
        static readonly int HorizonGlowId = Shader.PropertyToID("_TOD_HorizonGlow");
        static readonly int SunDirId = Shader.PropertyToID("_TOD_SunDir");
        static readonly int SunColorId = Shader.PropertyToID("_TOD_SunColor");
        static readonly int SunDiscId = Shader.PropertyToID("_TOD_SunDisc");
        static readonly int MoonDirId = Shader.PropertyToID("_TOD_MoonDir");
        static readonly int MoonColorId = Shader.PropertyToID("_TOD_MoonColor");
        static readonly int MoonPhaseId = Shader.PropertyToID("_TOD_MoonPhase");
        static readonly int MoonGlowId = Shader.PropertyToID("_TOD_MoonGlow");
        static readonly int StarsId = Shader.PropertyToID("_TOD_Stars");
        // Skybox Plus (Kronnect) custom sun/moon positions, normally set by its own SkyboxPlus component (not used here).
        static readonly int SbpCustomPositionsId = Shader.PropertyToID("_SBPCustomPositions");
        static readonly int SbpSunDirId = Shader.PropertyToID("_WorldSpaceSunDir");
        static readonly int SbpSunMatrixId = Shader.PropertyToID("_LightWL");
        static readonly int SbpMoonDirId = Shader.PropertyToID("_WorldSpaceMoonDir");
        static readonly int SbpMoonMatrixId = Shader.PropertyToID("_MoonLightWL");
        static readonly int SbpMoonPhaseId = Shader.PropertyToID("_MoonPhase");
        static readonly int SbpMoonPhaseAngleId = Shader.PropertyToID("_MoonPhaseAngle");
        static readonly int StarMapRotationId = Shader.PropertyToID("_StarMapRotation");
        // Right ascension of the Milky Way's core (Sagittarius A*), degrees.
        const float GalacticCoreRightAscension = 266.405f;

        Quaternion _lightFrom;
        Quaternion _lightTo;
        float _glide = 1f;
        bool _lightPlaced;
        bool _sunLit;
        // Play mode: runtime copy of a Skybox Plus sky, whose moon phase is a material property.
        Material _skySource;
        Material _skyCopy;

        // No light glide running.
        public bool LightSettled => _glide >= 1f;

        // False without a light. Hold: the look holds the light still between transitions, or a skip runs (snap, don't glide).
        public bool ApplyLight(Light light, SunPath path, in AtmosphereState state, Vector3 sun, float sunElevation, Vector3 moon,
            float moonElevation, float moonPhase, float shadowStep, float shadowGlide, bool hold, out bool sunLights)
        {
            sunLights = false;
            if (light == null)
                return false;

            float fullness = 1f - Mathf.Abs(moonPhase - 0.5f) * 2f;
            float sunIntensity = state.SunIntensity * Mathf.SmoothStep(0f, 1f, sunElevation / path.HorizonFade);
            float moonIntensity = state.MoonIntensity * Mathf.SmoothStep(0f, 1f, moonElevation / path.HorizonFade) * Mathf.Lerp(path.NewMoonLight, 1f, fullness);
            sunLights = sunIntensity >= moonIntensity;

            // The swap happens while both are near zero at twilight, so it can't be seen.
            Vector3 toSource = sunLights ? sun : moon;
            float bearing = Mathf.Atan2(toSource.x, toSource.z) * Mathf.Rad2Deg;
            float elevation = Mathf.Max(sunLights ? sunElevation : moonElevation, path.MinLightElevation);

            // Only changed values are written (here, RenderSettings and the volumes): every write dirties the object,
            // and a selected one repaints its Inspector each frame, which halves the editor's frame rate.
            Quaternion rotation = StepLight(Quaternion.LookRotation(-SunPath.Direction(bearing, elevation)), sunLights, shadowStep,
                shadowGlide, hold);
            if (!light.enabled)
                light.enabled = true;
            Transform lightTransform = light.transform;
            // Exact compare: Quaternion's == ignores turns under ~0.16°, which would freeze a slow continuous sun.
            if (!lightTransform.rotation.Equals(rotation))
                lightTransform.rotation = rotation;
            if (light.useColorTemperature)
                light.useColorTemperature = false;
            Color color = sunLights ? state.SunColor : state.MoonColor;
            if (light.color != color)
                light.color = color;
            float intensity = sunLights ? sunIntensity : moonIntensity;
            if (light.intensity != intensity)
                light.intensity = intensity;
            float shadowStrength = sunLights ? state.SunShadows : state.MoonShadows;
            if (light.shadowStrength != shadowStrength)
                light.shadowStrength = shadowStrength;
            return true;
        }

        // Shadow maps shimmer while their light turns, so it turns in short glides between still holds.
        // Jumps, skips and the sun/moon swap snap: nobody sees the shadows move then.
        Quaternion StepLight(Quaternion target, bool sunLights, float shadowStep, float shadowGlide, bool hold)
        {
            bool snap = shadowStep <= 0f || hold || !Application.isPlaying || !_lightPlaced || sunLights != _sunLit
                || Quaternion.Angle(_lightTo, target) > shadowStep * 4f + 2f;
            if (snap)
            {
                _lightFrom = _lightTo = target;
                _glide = 1f;
                _lightPlaced = true;
                _sunLit = sunLights;
                return target;
            }

            if (_glide >= 1f && Quaternion.Angle(_lightTo, target) >= shadowStep)
            {
                _lightFrom = _lightTo;
                _lightTo = target;
                _glide = 0f;
            }
            if (_glide < 1f)
                _glide = shadowGlide > 0f ? Mathf.Min(1f, _glide + Time.unscaledDeltaTime / shadowGlide) : 1f;
            return Quaternion.Slerp(_lightFrom, _lightTo, Mathf.SmoothStep(0f, 1f, _glide));
        }

        // Feeds both skies, so switching the scene's skybox material (Time Of Day or Skybox Plus) switches the sky.
        public void ApplySky(in AtmosphereState state, float lookHour, Vector3 sun, Vector3 moon, float moonPhase, in SkySettings settings)
        {
            ApplyTimeOfDaySky(state, sun, moon, moonPhase);
            ApplySkyboxPlus(sun, moon, moonPhase, settings.SkyboxPlusDuskElevation,
                SkyboxPlusSink(lookHour, settings.SkyboxPlusMorningSink, settings.SkyboxPlusEveningSink));
            float siderealTime = GalacticCoreRightAscension + 15f * (lookHour - settings.GalacticCoreTransit);
            Shader.SetGlobalMatrix(StarMapRotationId, StarMapRotation(settings.StarLatitude, siderealTime, settings.StarAzimuth));
        }

        // Puts the scene's own Skybox Plus material back and frees the runtime copy.
        public void ReleaseSkyCopy()
        {
            if (_skyCopy == null)
                return;
            if (RenderSettings.skybox == _skyCopy)
                RenderSettings.skybox = _skySource;
            UnityEngine.Object.Destroy(_skyCopy);
        }

        // World sky direction → star map direction. The map is equirectangular in celestial coordinates (right ascension 0h at
        // its left edge, growing rightward; +90° declination at the top) imported as a cubemap with Unity's latitude-longitude
        // layout, where u = 0.5 + atan2(x, z) / 2π (measured: +X holds u 0.75). North is +Z, east +X. Sidereal time in degrees (15° per hour).
        // Azimuth turns the whole sky clockwise seen from above (a star due south moves to bearing 180 + azimuth).
        public static Matrix4x4 StarMapRotation(float latitude, float siderealTime, float azimuth)
        {
            float lat = latitude * Mathf.Deg2Rad;
            float lst = siderealTime * Mathf.Deg2Rad;
            float cosLst = Mathf.Cos(lst);
            float sinLst = Mathf.Sin(lst);
            // Hour-angle frame in world space: where the equator crosses the meridian, west, and the celestial pole.
            var meridian = new Vector3(0f, Mathf.Cos(lat), -Mathf.Sin(lat));
            Vector3 west = Vector3.left;
            var pole = new Vector3(0f, Mathf.Sin(lat), Mathf.Cos(lat));
            // Right ascension = sidereal time − hour angle; map direction = (−cos δ sin α, sin δ, −cos δ cos α).
            Vector3 rowX = -sinLst * meridian + cosLst * west;
            Vector3 rowZ = -cosLst * meridian - sinLst * west;
            Matrix4x4 m = Matrix4x4.identity;
            m.SetRow(0, rowX);
            m.SetRow(1, pole);
            m.SetRow(2, rowZ);
            return m * Matrix4x4.Rotate(Quaternion.Euler(0f, -azimuth, 0f));
        }

        static void ApplyTimeOfDaySky(in AtmosphereState state, Vector3 sun, Vector3 moon, float moonPhase)
        {
            // Tint and exposure are Skybox/Panoramic values (the looks copied them), which Unity converts from gamma to linear
            // for a material; globals are passed raw, so convert here or the sky renders ~10x too bright and clips.
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Shader.SetGlobalColor(SkyTintId, linear ? state.SkyTint.linear : state.SkyTint);
            Shader.SetGlobalFloat(SkyExposureId, linear ? Mathf.GammaToLinearSpace(state.SkyExposure) : state.SkyExposure);
            Shader.SetGlobalColor(NightTopId, state.NightSkyTop);
            Shader.SetGlobalColor(NightHorizonId, state.NightSkyHorizon);
            Shader.SetGlobalFloat(NightBlendId, state.NightSkyBlend);
            Shader.SetGlobalColor(HorizonGlowId, state.HorizonGlow);
            Shader.SetGlobalVector(SunDirId, sun);
            Shader.SetGlobalColor(SunColorId, state.SunColor);
            Shader.SetGlobalFloat(SunDiscId, state.SunDisc);
            Shader.SetGlobalVector(MoonDirId, moon);
            Shader.SetGlobalColor(MoonColorId, state.MoonColor);
            Shader.SetGlobalFloat(MoonPhaseId, moonPhase);
            Shader.SetGlobalFloat(MoonGlowId, state.MoonGlow);
            Shader.SetGlobalFloat(StarsId, state.Stars);
        }

        // Skybox Plus darkens its sky and shows stars from its sun's height alone, so near and below the horizon its sun sinks faster.
        public static Vector3 SkyboxPlusSun(Vector3 sun, float duskElevation, float sink)
        {
            float elevation = Mathf.Asin(Mathf.Clamp(sun.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (elevation >= duskElevation)
                return sun;
            elevation = duskElevation + (elevation - duskElevation) * sink;
            float bearing = Mathf.Atan2(sun.x, sun.z) * Mathf.Rad2Deg;
            return SunPath.Direction(bearing, Mathf.Max(elevation, -85f));
        }

        // Switches at noon and midnight, where the sun is high or deep enough that either sink gives the same sky.
        public static float SkyboxPlusSink(float hour, float morning, float evening) => hour >= 12f ? evening : morning;

        void ApplySkyboxPlus(Vector3 sun, Vector3 moon, float moonPhase, float duskElevation, float sink)
        {
            sun = SkyboxPlusSun(sun, duskElevation, sink);
            Shader.SetGlobalFloat(SbpCustomPositionsId, 1f);
            Shader.SetGlobalVector(SbpSunDirId, sun);
            Shader.SetGlobalMatrix(SbpSunMatrixId, Matrix4x4.Rotate(Quaternion.Inverse(Quaternion.LookRotation(-sun))));
            Shader.SetGlobalVector(SbpMoonDirId, new Vector4(moon.x, moon.y, moon.z, -1f));
            Shader.SetGlobalMatrix(SbpMoonMatrixId, Matrix4x4.Rotate(Quaternion.Inverse(Quaternion.LookRotation(-moon))));

            // The phase is a material property: Play mode writes it to a runtime copy, so the asset is never written.
            if (!Application.isPlaying)
                return;
            Material sky = RenderSettings.skybox;
            if (sky == null || !sky.HasProperty(SbpMoonPhaseAngleId))
                return;
            if (sky != _skyCopy)
            {
                if (_skyCopy != null)
                    UnityEngine.Object.Destroy(_skyCopy);
                _skySource = sky;
                _skyCopy = new Material(sky) { name = sky.name + " (runtime)" };
                RenderSettings.skybox = _skyCopy;
            }
            // Ours: 0 new, 0.5 full. Skybox Plus: -1 new, 1 full; angle 0 lights the right side (waxing), 180 the left.
            float fullness = 1f - Mathf.Abs(moonPhase - 0.5f) * 2f;
            _skyCopy.SetFloat(SbpMoonPhaseId, fullness * 2f - 1f);
            _skyCopy.SetFloat(SbpMoonPhaseAngleId, moonPhase < 0.5f ? 0f : 180f);
        }
    }
}
