using System.Collections.Generic;
using Aipocalypse.Core.WorldTimeSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Sole writer of the sun light, ambient, fog, reflections, sky globals and the time-of-day grade volumes.
    // Each frame: profile look at the clock's hour → atmosphere layers (weather later) → apply → reactors.
    // Without a clock (no installer, edit mode) it uses Preview Hour; in edit mode only while Preview is on.
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

        [SerializeField, InlineEditor] TimeOfDayProfile _profile;
        [SerializeField, Tooltip("When the look changes. By Phases: at each phase start, to the profile's Phase Looks hour. By Hours: at each whole hour. Continuous: every frame. The sun and moon move with the look, so they hold still between transitions")]
        LookTransition _transition = LookTransition.ByPhases;
        [SerializeField, Min(0f), Tooltip("Real seconds a look change takes. 0 = cut")]
        float _transitionDuration = 5f;
        [SerializeField, Tooltip("The one shadowed directional light: the sun by day, the moon by night")]
        Light _light;
        [SerializeField, Tooltip("Global volume above the base grade: the grade of the key before the current hour")]
        Volume _gradeFrom;
        [SerializeField, Tooltip("Global volume above Grade From: the next key's grade, weighted by the blend")]
        Volume _gradeTo;
        [SerializeField, Min(0f), Tooltip("Continuous only (the other transitions hold the light still): the light holds still until the sun has moved this many degrees, then glides there. A light turning every frame makes shadow edges crawl. 0 = follow the sun every frame")]
        float _shadowStep = 1f;
        [SerializeField, Min(0f), Tooltip("Seconds each step glides over. 0 = snap")]
        float _shadowGlide = 0.3f;
        [SerializeField, Min(0f), Tooltip("Play mode: URP shadow distance (m) while the moon lights the scene. Swaps at twilight with the light. 0 = keep the asset's")]
        float _nightShadowDistance = 60f;
        [SerializeField, Tooltip("Reactors placed in this scene. Collected automatically when the scene is saved or Play starts")]
        List<EnvironmentReactor> _sceneReactors = new();

        [BoxGroup("Preview"), SerializeField, Tooltip("Edit mode: apply the look at Preview Hour. Writes the scene's lighting, so turn it off before saving a final look")]
        bool _preview;
        [BoxGroup("Preview"), SerializeField, Range(0f, 24f)] float _previewHour = 9f;
        [BoxGroup("Preview"), SerializeField, Min(0f), Tooltip("Edit mode: game hours per real second while previewing. 0 = hold")]
        float _previewSpeed;
        [BoxGroup("Preview"), SerializeField, Range(0f, 1f), Tooltip("0 new moon, 0.5 full")] float _previewMoonPhase = 0.5f;

        readonly List<EnvironmentReactor> _reactors = new();
        readonly List<IAtmosphereLayer> _layers = new();
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
        UniversalRenderPipelineAsset _pipeline;
        float _dayShadowDistance;

        public TimeOfDayProfile Profile => _profile;
        public WorldClock Clock => _clock;
        public AtmosphereState State => _state;
        public float Hour { get; private set; }
        // The hour whose look is shown: trails Hour unless the transition is Continuous.
        public float LookHour { get; private set; }
        public float MoonPhase { get; private set; }
        public Vector3 SunDirection { get; private set; }
        public Vector3 MoonDirection { get; private set; }
        public float SunElevation { get; private set; }
        // 0 at night (sun 6° below the horizon) to 1 by day (10° above).
        public float Daylight { get; private set; }

        [Inject]
        void Construct(IObjectResolver resolver) => resolver.TryResolve(out _clock);

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

        public void AddLayer(IAtmosphereLayer layer)
        {
            if (_layers.Contains(layer))
                return;
            int index = 0;
            while (index < _layers.Count && _layers[index].Order <= layer.Order)
                index++;
            _layers.Insert(index, layer);
        }

        public void RemoveLayer(IAtmosphereLayer layer) => _layers.Remove(layer);

        // Edit mode with Preview off still feeds the sky shader (globals only, nothing saved) so the sky isn't left unset.
        void OnEnable()
        {
            if (!Application.isPlaying && !_preview)
                ApplySkyOnly();
        }

        // The pipeline asset is shared and saved with the project, so its own distance always comes back.
        void OnDisable() => SetShadowDistance(false);

        // _pipeline is set only while the night distance is in place.
        void SetShadowDistance(bool night)
        {
            night &= _nightShadowDistance > 0f && Application.isPlaying;
            if (night && _pipeline == null)
            {
                _pipeline = UniversalRenderPipeline.asset;
                if (_pipeline == null)
                    return;
                _dayShadowDistance = _pipeline.shadowDistance;
            }
            if (_pipeline == null)
                return;

            float distance = night ? _nightShadowDistance : _dayShadowDistance;
            if (_pipeline.shadowDistance != distance)
                _pipeline.shadowDistance = distance;
            if (!night)
                _pipeline = null;
        }

        void OnValidate()
        {
            if (!Application.isPlaying && !_preview)
                ApplySkyOnly();
        }

        void ApplySkyOnly()
        {
            if (_profile == null || !_profile.Evaluate(_previewHour, out AtmosphereState state, out _))
                return;
            Vector3 sun = _profile.Path.Sun(_previewHour, out _);
            Vector3 moon = _profile.Path.Moon(_previewHour, out _);
            ApplySky(state, sun, moon, _previewMoonPhase);
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
            return Mathf.Repeat(_lookFrom + _lookSpan * Smooth01(_lookGlide), 24f);
        }

        void Apply(float hour, float lookHour, float moonPhase)
        {
            if (_profile == null || !_profile.Evaluate(lookHour, out AtmosphereState state, out TimeOfDayProfile.Segment segment))
                return;

            for (int i = 0; i < _layers.Count; i++)
                _layers[i].Modify(ref state, hour);

            _state = state;
            Hour = hour;
            LookHour = lookHour;
            MoonPhase = moonPhase;

            // The sun and moon belong to the look: By Phases / By Hours hold them still between transitions.
            SunPath path = _profile.Path;
            Vector3 sun = path.Sun(lookHour, out float sunElevation);
            Vector3 moon = path.Moon(lookHour, out float moonElevation);
            SunDirection = sun;
            MoonDirection = moon;
            SunElevation = sunElevation;
            Daylight = Smooth01((sunElevation + 6f) / 16f);

            ApplyLight(path, state, sun, sunElevation, moon, moonElevation, moonPhase);
            ApplyRenderSettings(state);
            ApplySky(state, sun, moon, moonPhase);
            ApplyGrades(segment);
        }

        void ApplyLight(SunPath path, in AtmosphereState state, Vector3 sun, float sunElevation, Vector3 moon, float moonElevation, float moonPhase)
        {
            if (_light == null)
                return;

            float fullness = 1f - Mathf.Abs(moonPhase - 0.5f) * 2f;
            float sunIntensity = state.SunIntensity * Smooth01(sunElevation / path.HorizonFade);
            float moonIntensity = state.MoonIntensity * Smooth01(moonElevation / path.HorizonFade) * Mathf.Lerp(1f / 3f, 1f, fullness);
            bool sunLights = sunIntensity >= moonIntensity;

            // The swap happens while both are near zero at twilight, so it can't be seen.
            Vector3 toSource = sunLights ? sun : moon;
            float bearing = Mathf.Atan2(toSource.x, toSource.z) * Mathf.Rad2Deg;
            float elevation = Mathf.Max(sunLights ? sunElevation : moonElevation, path.MinLightElevation);

            _light.transform.rotation = StepLight(Quaternion.LookRotation(-SunPath.Direction(bearing, elevation)), sunLights);
            _light.useColorTemperature = false;
            _light.color = sunLights ? state.SunColor : state.MoonColor;
            _light.intensity = sunLights ? sunIntensity : moonIntensity;
            _light.shadowStrength = sunLights ? state.SunShadows : state.MoonShadows;
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
            return Quaternion.Slerp(_lightFrom, _lightTo, Smooth01(_glide));
        }

        static void ApplyRenderSettings(in AtmosphereState state)
        {
            if (RenderSettings.ambientMode != AmbientMode.Trilight)
                RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = state.AmbientSky;
            RenderSettings.ambientEquatorColor = state.AmbientEquator;
            RenderSettings.ambientGroundColor = state.AmbientGround;
            RenderSettings.reflectionIntensity = state.ReflectionIntensity;
            RenderSettings.fogColor = state.FogColor;
            RenderSettings.fogDensity = state.FogDensity;
        }

        static void ApplySky(in AtmosphereState state, Vector3 sun, Vector3 moon, float moonPhase)
        {
            Shader.SetGlobalColor(SkyTintId, state.SkyTint);
            Shader.SetGlobalFloat(SkyExposureId, state.SkyExposure);
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

        // From = the key before the hour at full weight, To = the next key at the blend weight: URP's volume stack lerps them.
        void ApplyGrades(TimeOfDayProfile.Segment segment)
        {
            VolumeProfile from = _profile.Keys[segment.From].Grade;
            VolumeProfile to = _profile.Keys[segment.To].Grade;
            if (_gradeFrom != null)
            {
                if (_fromProfile != from || _gradeFrom.sharedProfile != from)
                    _gradeFrom.sharedProfile = _fromProfile = from;
                _gradeFrom.weight = from != null ? 1f : 0f;
            }
            if (_gradeTo != null)
            {
                if (_toProfile != to || _gradeTo.sharedProfile != to)
                    _gradeTo.sharedProfile = _toProfile = to;
                _gradeTo.weight = to != null && to != from ? segment.T : 0f;
            }
        }

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
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
        public void CollectSceneReactors()
        {
            var found = new List<EnvironmentReactor>();
            foreach (EnvironmentReactor reactor in FindObjectsByType<EnvironmentReactor>(FindObjectsInactive.Include))
                if (reactor.gameObject.scene == gameObject.scene)
                    found.Add(reactor);

            // Compared as a set: re-sorting the same reactors must not dirty the scene on every Play.
            if (found.Count == _sceneReactors.Count && new HashSet<EnvironmentReactor>(found).SetEquals(_sceneReactors))
                return;

            found.Sort((a, b) =>
            {
                int byName = string.CompareOrdinal(a.name, b.name);
                return byName != 0 ? byName : string.CompareOrdinal(a.GetType().Name, b.GetType().Name);
            });

            _sceneReactors = found;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        // Keeps the scene list current without anyone pressing the button: on save and when entering Play mode.
        [UnityEditor.InitializeOnLoadMethod]
        static void HookCollection()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += (scene, _) => CollectIn(scene);
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state != UnityEditor.PlayModeStateChange.ExitingEditMode)
                    return;
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                    CollectIn(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i));
            };
        }

        static void CollectIn(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (EnvironmentDirector director in FindObjectsByType<EnvironmentDirector>(FindObjectsInactive.Include))
                if (director.gameObject.scene == scene)
                    director.CollectSceneReactors();
        }
#endif
    }
}
