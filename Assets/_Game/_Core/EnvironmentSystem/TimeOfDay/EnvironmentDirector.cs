using System.Collections.Generic;
using Aipocalypse.Core.GraphicsSystem;
using Aipocalypse.Core.WorldTimeSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using VContainer;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Sole writer of sun, ambient, fog, reflections, sky globals and grade volumes: profile look at the clock's hour → apply
    // → reactors. Without a clock it uses Preview Hour (in edit mode only while Preview is on).
    public enum LookTransition
    {
        ByPhases,
        ByHours,
        Continuous,
    }

    [ExecuteAlways]
    public sealed class EnvironmentDirector : MonoBehaviour
    {
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

        // Moon phase drift smaller than this doesn't re-apply an unchanged look (the phase creeps every frame).
        const float MoonPhaseStep = 0.0001f;

        // Bumped when a look, profile or director setting is edited, so a held look re-applies while tuning in Play mode.
        static int _editRevision;

        public static void MarkLooksEdited() => _editRevision++;

        [SerializeField, InlineEditor] TimeOfDayProfile _profile;
        [SerializeField, Tooltip("When the look changes. By Phases: at each phase start, to the profile's Phase Looks hour. By Hours: at each whole hour. Continuous: every frame. The sun and moon move with the look, so they hold still between transitions")]
        LookTransition _transition = LookTransition.ByPhases;
        [SerializeField, Min(0f), Tooltip("Real seconds a look change takes. 0 = cut")]
        float _transitionDuration = 5f;
        [SerializeField, Tooltip("The one shadowed directional light: the sun by day, the moon by night")]
        Light _light;
        [SerializeField, Tooltip("Global volume: the grade of the key before the current hour. The scene's only grade outside a blend")]
        Volume _gradeFrom;
        [SerializeField, Tooltip("Global volume above Grade From: the next key's grade, weighted by the blend")]
        Volume _gradeTo;
        [SerializeField, Min(0f), Tooltip("Continuous only (the other transitions hold the light still): the light holds still until the sun has moved this many degrees, then glides there. A light turning every frame makes shadow edges crawl. 0 = follow the sun every frame")]
        float _shadowStep = 1f;
        [SerializeField, Min(0f), Tooltip("Seconds each step glides over. 0 = snap")]
        float _shadowGlide = 0.3f;
        [SerializeField, Min(0f), Tooltip("Play mode: URP shadow distance (m) while the moon lights the scene. Swaps at twilight with the light. 0 = keep the asset's")]
        float _nightShadowDistance = 60f;
        [SerializeField, Range(0f, 15f), Tooltip("Skybox Plus sky only: sun elevation (degrees) below which its sun sinks faster than the sun path. " +
            "Its sky stays day-blue until the sun is well below the horizon, so this starts its twilight before sunset")]
        float _skyboxPlusDuskElevation = 6f;
        [SerializeField, Range(1f, 6f), FormerlySerializedAs("_skyboxPlusNightSink"), Tooltip("Skybox Plus sky only, before noon: how many times faster its sun sinks below Dusk Elevation. " +
            "Its sky is dark with stars only once its sun is ~27° down; 3.5 = a twilight-blue sky at sunrise, matching the dawn grade's lit ground")]
        float _skyboxPlusMorningSink = 3.5f;
        [SerializeField, Range(1f, 6f), Tooltip("Skybox Plus sky only, after noon: as Morning Sink. " +
            "Higher than the morning because the night grade already darkens the ground at sunset; 5 = deep indigo with the first stars at sunset")]
        float _skyboxPlusEveningSink = 5f;
        [SerializeField, Range(-90f, 90f), Tooltip("Star map sky: the world's latitude. The celestial pole stands this many degrees above the north horizon and the stars turn around it")]
        float _starLatitude = 30f;
        [SerializeField, Range(0f, 360f), Tooltip("Star map sky: turns the whole star sky around the vertical by this bearing. 180 puts the Milky Way's core in the north (over WorldMap's northern mountains) instead of the real south")]
        float _starAzimuth = 180f;
        [SerializeField, Range(0f, 24f), Tooltip("Star map sky: hour the Milky Way's core crosses due south, at its highest. The sky turns 15° per hour like the real one")]
        float _galacticCoreTransit = 23f;
        [SerializeField, Tooltip("Reactors placed in this scene. Collected automatically when the scene is saved or Play starts")]
        List<EnvironmentReactor> _sceneReactors = new();

        [BoxGroup("Preview"), SerializeField, Tooltip("Edit mode: apply the look at Preview Hour. Writes the scene's lighting, so turn it off before saving a final look")]
        bool _preview;
        [BoxGroup("Preview"), SerializeField, Range(0f, 24f)] float _previewHour = 9f;
        [BoxGroup("Preview"), SerializeField, Min(0f), Tooltip("Edit mode: game hours per real second while previewing. 0 = hold")]
        float _previewSpeed;
        [BoxGroup("Preview"), SerializeField, Range(0f, 1f), Tooltip("0 new moon, 0.5 full")] float _previewMoonPhase = 0.5f;

        readonly List<EnvironmentReactor> _reactors = new();
        WorldClock _clock;
        AtmosphereState _state;
        VolumeProfile _fromProfile;
        VolumeProfile _toProfile;
        float _lookFrom;
        float _lookSpan;
        float _lookTarget = -1f;
        float _lookGlide = 1f;
        Quaternion _lightFrom;
        Quaternion _lightTo;
        float _glide = 1f;
        bool _lightPlaced;
        bool _sunLit;
        GraphicsService _graphics;
        // Play mode: runtime copy of a Skybox Plus sky, whose moon phase is a material property.
        Material _skySource;
        Material _skyCopy;
        // What was last applied; Apply skips an unchanged look.
        float _appliedLookHour = -1f;
        float _appliedMoonPhase = -1f;
        int _appliedRevision = -1;

        // By reference: reactors read a few fields of it each frame.
        public ref readonly AtmosphereState State => ref _state;
        public float Hour { get; private set; }
        // The hour whose look is shown: trails Hour unless the transition is Continuous.
        public float LookHour { get; private set; }
        public float MoonPhase { get; private set; }
        public Vector3 SunDirection { get; private set; }
        public Vector3 MoonDirection { get; private set; }
        public float SunElevation { get; private set; }
        // 0 at night to 1 by day, over the profile's SunPath daylight elevations.
        public float Daylight { get; private set; }

        [Inject]
        void Construct(IObjectResolver resolver)
        {
            resolver.TryResolve(out _clock);
            resolver.TryResolve(out _graphics);
        }

        void Awake()
        {
            if (!Application.isPlaying)
                return;
            foreach (EnvironmentReactor reactor in _sceneReactors)
                Register(reactor);
        }

        public void Register(EnvironmentReactor reactor)
        {
            if (reactor != null && !_reactors.Contains(reactor))
                _reactors.Add(reactor);
        }

        public void Unregister(EnvironmentReactor reactor) => _reactors.Remove(reactor);

        // Edit mode with Preview off still feeds the sky shader (globals only, nothing saved) so the sky isn't left unset.
        void OnEnable()
        {
            // OnDisable released the night shadow distance: the next LateUpdate applies in full and asks again.
            _appliedRevision = -1;
            if (!Application.isPlaying && !_preview)
                ApplySkyOnly();
        }

        void OnDisable() => SetShadowDistance(false);

        void OnDestroy()
        {
            if (_skyCopy == null)
                return;
            if (RenderSettings.skybox == _skyCopy)
                RenderSettings.skybox = _skySource;
            Destroy(_skyCopy);
        }

        // A request to GraphicsService, which owns the shared pipeline asset's distance.
        void SetShadowDistance(bool night)
        {
            if (_graphics == null)
                return;
            if (night && _nightShadowDistance > 0f && Application.isPlaying)
                _graphics.RequestShadowDistance(this, _nightShadowDistance);
            else
                _graphics.ReleaseShadowDistance(this);
        }

        void OnValidate()
        {
            MarkLooksEdited();
            if (!Application.isPlaying && !_preview)
                ApplySkyOnly();
        }

        void ApplySkyOnly()
        {
            if (_profile == null || !_profile.Evaluate(_previewHour, out AtmosphereState state, out _))
                return;
            Vector3 sun = _profile.Path.Sun(_previewHour, out _);
            Vector3 moon = _profile.Path.Moon(_previewHour, out _);
            ApplySky(state, _previewHour, sun, moon, _previewMoonPhase);
        }

        void LateUpdate()
        {
            bool playing = Application.isPlaying;
            if (!playing && !_preview)
                return;

            float hour;
            float moonPhase;
            if (_clock != null)
            {
                hour = _clock.Now.Hour;
                moonPhase = _clock.MoonPhase;
            }
            else
            {
                if (!playing && _previewSpeed > 0f)
                    _previewHour = Mathf.Repeat(_previewHour + _previewSpeed * Time.unscaledDeltaTime, 24f);
                hour = _previewHour;
                moonPhase = _previewMoonPhase;
            }

            Apply(hour, playing && _clock != null ? StepLook(hour) : hour, moonPhase);

            if (playing)
            {
                float deltaTime = Time.unscaledDeltaTime;
                for (int i = _reactors.Count - 1; i >= 0; i--)
                {
                    EnvironmentReactor reactor = _reactors[i];
                    if (reactor == null)
                        _reactors.RemoveAt(i);
                    else if (reactor.isActiveAndEnabled)
                        reactor.React(this, deltaTime);
                }
            }
#if UNITY_EDITOR
            else if (_previewSpeed > 0f)
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        // Glides the look hour (not a snapshot of the look) so the grades still blend key to key on the way.
        float StepLook(float hour)
        {
            float target = _transition switch
            {
                LookTransition.ByPhases when _profile != null => _profile.PhaseLookHour(_clock.Phase),
                LookTransition.ByHours => _clock.Now.WholeHour,
                _ => hour,
            };

            if (_transition == LookTransition.Continuous || _lookTarget < 0f || _transitionDuration <= 0f)
            {
                _lookTarget = _lookFrom = target;
                _lookSpan = 0f;
                _lookGlide = 1f;
                return target;
            }

            if (target != _lookTarget)
            {
                _lookFrom = LookHour;
                _lookSpan = Mathf.Repeat(target - _lookFrom, 24f);
                // The short way round, so switching modes mid-day doesn't spin through a whole day.
                if (_lookSpan > 12f)
                    _lookSpan -= 24f;
                _lookTarget = target;
                _lookGlide = 0f;
            }
            if (_lookGlide < 1f)
                _lookGlide = Mathf.Min(1f, _lookGlide + Time.unscaledDeltaTime / _transitionDuration);
            return Mathf.Repeat(_lookFrom + _lookSpan * Mathf.SmoothStep(0f, 1f, _lookGlide), 24f);
        }

        void Apply(float hour, float lookHour, float moonPhase)
        {
            // By Phases / By Hours hold the look for minutes: with no light glide running, nothing to redo.
            if (Application.isPlaying && _glide >= 1f && lookHour == _appliedLookHour &&
                Mathf.Abs(moonPhase - _appliedMoonPhase) < MoonPhaseStep && _appliedRevision == _editRevision)
            {
                Hour = hour;
                return;
            }

            if (_profile == null || !_profile.Evaluate(lookHour, out AtmosphereState state, out TimeOfDayProfile.Segment segment))
                return;

            _state = state;
            Hour = hour;
            LookHour = lookHour;
            MoonPhase = moonPhase;
            _appliedLookHour = lookHour;
            _appliedMoonPhase = moonPhase;
            _appliedRevision = _editRevision;

            // The sun and moon belong to the look: By Phases / By Hours hold them still between transitions.
            SunPath path = _profile.Path;
            Vector3 sun = path.Sun(lookHour, out float sunElevation);
            Vector3 moon = path.Moon(lookHour, out float moonElevation);
            SunDirection = sun;
            MoonDirection = moon;
            SunElevation = sunElevation;
            Daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(path.DaylightStartElevation, path.DaylightFullElevation, sunElevation));

            ApplyLight(path, state, sun, sunElevation, moon, moonElevation, moonPhase);
            ApplyRenderSettings(state);
            ApplySky(state, lookHour, sun, moon, moonPhase);
            ApplyGrades(segment);
        }

        void ApplyLight(SunPath path, in AtmosphereState state, Vector3 sun, float sunElevation, Vector3 moon, float moonElevation, float moonPhase)
        {
            if (_light == null)
                return;

            float fullness = 1f - Mathf.Abs(moonPhase - 0.5f) * 2f;
            float sunIntensity = state.SunIntensity * Mathf.SmoothStep(0f, 1f, sunElevation / path.HorizonFade);
            float moonIntensity = state.MoonIntensity * Mathf.SmoothStep(0f, 1f, moonElevation / path.HorizonFade) * Mathf.Lerp(path.NewMoonLight, 1f, fullness);
            bool sunLights = sunIntensity >= moonIntensity;

            // The swap happens while both are near zero at twilight, so it can't be seen.
            Vector3 toSource = sunLights ? sun : moon;
            float bearing = Mathf.Atan2(toSource.x, toSource.z) * Mathf.Rad2Deg;
            float elevation = Mathf.Max(sunLights ? sunElevation : moonElevation, path.MinLightElevation);

            // Only changed values are written (here, RenderSettings and the volumes): every write dirties the object,
            // and a selected one repaints its Inspector each frame, which halves the editor's frame rate.
            Quaternion rotation = StepLight(Quaternion.LookRotation(-SunPath.Direction(bearing, elevation)), sunLights);
            Transform lightTransform = _light.transform;
            // Exact compare: Quaternion's == ignores turns under ~0.16°, which would freeze a slow continuous sun.
            if (!lightTransform.rotation.Equals(rotation))
                lightTransform.rotation = rotation;
            if (_light.useColorTemperature)
                _light.useColorTemperature = false;
            Color color = sunLights ? state.SunColor : state.MoonColor;
            if (_light.color != color)
                _light.color = color;
            float intensity = sunLights ? sunIntensity : moonIntensity;
            if (_light.intensity != intensity)
                _light.intensity = intensity;
            float shadowStrength = sunLights ? state.SunShadows : state.MoonShadows;
            if (_light.shadowStrength != shadowStrength)
                _light.shadowStrength = shadowStrength;
            SetShadowDistance(!sunLights);
        }

        // Shadow maps shimmer while their light turns, so it turns in short glides between still holds.
        // Jumps, skips and the sun/moon swap snap: nobody sees the shadows move then.
        Quaternion StepLight(Quaternion target, bool sunLights)
        {
            bool snap = _shadowStep <= 0f || _transition != LookTransition.Continuous || !Application.isPlaying || !_lightPlaced || sunLights != _sunLit
                || (_clock != null && _clock.IsSkipping) || Quaternion.Angle(_lightTo, target) > _shadowStep * 4f + 2f;
            if (snap)
            {
                _lightFrom = _lightTo = target;
                _glide = 1f;
                _lightPlaced = true;
                _sunLit = sunLights;
                return target;
            }

            if (_glide >= 1f && Quaternion.Angle(_lightTo, target) >= _shadowStep)
            {
                _lightFrom = _lightTo;
                _lightTo = target;
                _glide = 0f;
            }
            if (_glide < 1f)
                _glide = _shadowGlide > 0f ? Mathf.Min(1f, _glide + Time.unscaledDeltaTime / _shadowGlide) : 1f;
            return Quaternion.Slerp(_lightFrom, _lightTo, Mathf.SmoothStep(0f, 1f, _glide));
        }

        static void ApplyRenderSettings(in AtmosphereState state)
        {
            if (RenderSettings.ambientMode != AmbientMode.Trilight)
                RenderSettings.ambientMode = AmbientMode.Trilight;
            if (RenderSettings.ambientSkyColor != state.AmbientSky)
                RenderSettings.ambientSkyColor = state.AmbientSky;
            if (RenderSettings.ambientEquatorColor != state.AmbientEquator)
                RenderSettings.ambientEquatorColor = state.AmbientEquator;
            if (RenderSettings.ambientGroundColor != state.AmbientGround)
                RenderSettings.ambientGroundColor = state.AmbientGround;
            if (RenderSettings.reflectionIntensity != state.ReflectionIntensity)
                RenderSettings.reflectionIntensity = state.ReflectionIntensity;
            if (RenderSettings.fogColor != state.FogColor)
                RenderSettings.fogColor = state.FogColor;
            if (RenderSettings.fogDensity != state.FogDensity)
                RenderSettings.fogDensity = state.FogDensity;
        }

        // Feeds both skies, so switching the scene's skybox material (Time Of Day or Skybox Plus) switches the sky.
        void ApplySky(in AtmosphereState state, float lookHour, Vector3 sun, Vector3 moon, float moonPhase)
        {
            ApplyTimeOfDaySky(state, sun, moon, moonPhase);
            ApplySkyboxPlus(sun, moon, moonPhase, SkyboxPlusSink(lookHour, _skyboxPlusMorningSink, _skyboxPlusEveningSink));
            float siderealTime = GalacticCoreRightAscension + 15f * (lookHour - _galacticCoreTransit);
            Shader.SetGlobalMatrix(StarMapRotationId, StarMapRotation(_starLatitude, siderealTime, _starAzimuth));
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

        void ApplySkyboxPlus(Vector3 sun, Vector3 moon, float moonPhase, float sink)
        {
            sun = SkyboxPlusSun(sun, _skyboxPlusDuskElevation, sink);
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
                    Destroy(_skyCopy);
                _skySource = sky;
                _skyCopy = new Material(sky) { name = sky.name + " (runtime)" };
                RenderSettings.skybox = _skyCopy;
            }
            // Ours: 0 new, 0.5 full. Skybox Plus: -1 new, 1 full; angle 0 lights the right side (waxing), 180 the left.
            float fullness = 1f - Mathf.Abs(moonPhase - 0.5f) * 2f;
            _skyCopy.SetFloat(SbpMoonPhaseId, fullness * 2f - 1f);
            _skyCopy.SetFloat(SbpMoonPhaseAngleId, moonPhase < 0.5f ? 0f : 180f);
        }

        // From = the key before the hour at full weight, To = the next key at the blend weight: URP's volume stack lerps them.
        // Each grade is complete (no base volume under them), so outside a blend between two grades only From is on.
        void ApplyGrades(TimeOfDayProfile.Segment segment)
        {
            VolumeProfile from = _profile.Keys[segment.From].Grade;
            VolumeProfile to = _profile.Keys[segment.To].Grade;
            SetGrade(_gradeFrom, ref _fromProfile, from, from != null ? 1f : 0f);
            SetGrade(_gradeTo, ref _toProfile, to, to != null && to != from ? segment.T : 0f);
        }

        static void SetGrade(Volume volume, ref VolumeProfile applied, VolumeProfile profile, float weight)
        {
            if (volume == null)
                return;
            if (applied != profile || volume.sharedProfile != profile)
                volume.sharedProfile = applied = profile;
            if (volume.weight != weight)
                volume.weight = weight;
            bool on = weight > 0f;
            if (volume.enabled != on)
                volume.enabled = on;
        }

        // Inspector: Play mode jumps the clock, edit mode moves the preview.
        [Button, PropertyOrder(-1)]
        void JumpToHour(float hour)
        {
            if (_clock != null)
                _clock.JumpTo(hour);
            else
                _previewHour = Mathf.Repeat(hour, 24f);
        }

#if UNITY_EDITOR
        [Button("Collect scene reactors"), PropertyOrder(-1)]
        public void CollectSceneReactors() => SceneCollection.Collect(this, _sceneReactors);

        // Keeps the scene list current without anyone pressing the button: on save and when entering Play mode.
        [UnityEditor.InitializeOnLoadMethod]
        static void HookCollection() => SceneCollection.Hook<EnvironmentDirector>(director => director.CollectSceneReactors());
#endif
    }
}
