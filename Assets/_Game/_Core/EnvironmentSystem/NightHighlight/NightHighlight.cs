using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MB13.Aipocalypse.Core.EnvironmentSystem
{
    // Keeps enemies readable at night (NightHighlightSettings): drives a directional light that only the highlight layer
    // receives, turned with the view, and sets the enemies' eye glow global (EnemyEyeGlow shader). Use the Darkness source
    // so both fade in at dusk.
    public sealed class NightHighlight : TimeOfDayReactor
    {
        static readonly int EyeGlowId = Shader.PropertyToID("_EnemyEyeGlow");

        [SerializeField, InlineEditor] NightHighlightSettings _settings;
        [SerializeField, Tooltip("A directional light under this object; its rendering layers are set from the settings")] Light _light;

        UniversalAdditionalLightData _lightData;
        float _eyeGlow = -1f;

        public NightHighlightSettings Settings => _settings;
        // Console switches (dev builds): null follows the settings.
        public bool? LightOverride { get; set; }
        public bool? EyesOverride { get; set; }
        public bool LightOn => _settings != null && (LightOverride ?? _settings.LightEnabled);
        public bool EyesOn => _settings != null && (EyesOverride ?? _settings.EyesEnabled);

        void Awake()
        {
            if (_light != null)
                _light.TryGetComponent(out _lightData);
        }

        void OnDisable()
        {
            if (_light != null)
                _light.enabled = false;
            SetEyeGlow(0f);
        }

        public override void React(EnvironmentDirector director, float deltaTime)
        {
            base.React(director, deltaTime);
            float darkness = Mathf.Max(0f, Weight);
            if (_settings != null && _light != null)
                ApplyLight(darkness);
            SetEyeGlow(EyesOn ? _settings.EyeBrightness * darkness : 0f);
        }

        void SetEyeGlow(float value)
        {
            if (Mathf.Abs(value - _eyeGlow) < 0.001f)
                return;
            _eyeGlow = value;
            Shader.SetGlobalFloat(EyeGlowId, value);
        }

        protected override void Apply(float weight) { }

        void ApplyLight(float darkness)
        {
            float intensity = LightOn ? _settings.LightIntensity * darkness : 0f;
            bool on = intensity > 0.001f;
            if (_light.enabled != on)
                _light.enabled = on;
            Camera view = Camera.main;
            if (!on || view == null)
                return;
            _light.intensity = intensity;
            _light.color = _settings.LightColor;
            if (_lightData != null)
                _lightData.renderingLayers = _settings.Layer;
            _light.transform.rotation = LightRotation(view.transform.forward, _settings.LightPitch, _settings.LightYaw);
        }

        // Shines the way the player looks (level, whatever the camera's pitch), tilted down and turned to the side.
        public static Quaternion LightRotation(Vector3 viewForward, float pitch, float yaw)
        {
            Vector3 flat = Vector3.ProjectOnPlane(viewForward, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f)
                flat = Vector3.forward;
            return Quaternion.LookRotation(flat) * Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
