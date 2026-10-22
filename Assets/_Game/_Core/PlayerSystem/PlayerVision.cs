using Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // The light around the player in the dark: a soft vision light, or a worn lantern's warmer, wider one. Off by day.
    // Radius is how far the ground reads clearly. URP light fades with distance squared, so the intensity is worked out
    // from the radius (bigger radius, brighter light) to keep the same brightness at the edge; the range reaches past it.
    public sealed class PlayerVision
    {
        readonly Light _light;
        readonly PlayerVisionTuning _tuning;

        bool _started;
        float _brightness;

        public PlayerVision(Light light, PlayerVisionTuning tuning)
        {
            _light = light;
            _tuning = tuning;
        }

        public float Radius { get; private set; }
        public float Intensity { get; private set; }
        public float Range { get; private set; }
        public Color LightColor { get; private set; }
        public Vector3 Position { get; private set; }

        // `darkness` 0 by day to 1 at night; `radius` the vision radius after stats; `lantern` the worn one, or null.
        public void Tick(float darkness, float radius, LanternDefinition lantern, float time, float deltaTime)
        {
            bool worn = lantern != null;
            float t = _started ? Damping.ByTime(_tuning.BlendTime, deltaTime) : 1f;
            _started = true;
            Radius = Mathf.Lerp(Radius, radius + (worn ? lantern.Radius : 0f), t);
            LightColor = Color.Lerp(LightColor, worn ? lantern.Color : _tuning.Color, t);
            Position = Vector3.Lerp(Position, worn ? _tuning.LanternPosition : _tuning.Position, t);
            _brightness = Mathf.Lerp(_brightness, worn ? lantern.Brightness : _tuning.Brightness, t);

            // From the light down to the ground at the radius' edge.
            float edgeSqr = Radius * Radius + Position.y * Position.y;
            Range = Mathf.Sqrt(edgeSqr) * _tuning.RangeScale;
            float flicker = worn ? 1f - lantern.Flicker * Mathf.PerlinNoise(time * lantern.FlickerSpeed, 0.5f) : 1f;
            Intensity = IntensityFor(_brightness, edgeSqr, _tuning.RangeScale, _tuning.MaxIntensity) * Mathf.Clamp01(darkness) * flicker;
            Apply(worn ? lantern.Shadows : LightShadows.None);
        }

        // URP: brightness at distance d = intensity / d² × (1 - (d / range)⁴)², with d = range / rangeScale at the edge.
        public static float IntensityFor(float edgeBrightness, float edgeDistanceSqr, float rangeScale, float max)
        {
            float fade = 1f - 1f / (rangeScale * rangeScale * rangeScale * rangeScale);
            return Mathf.Min(max, edgeBrightness * edgeDistanceSqr / Mathf.Max(0.01f, fade * fade));
        }

        void Apply(LightShadows shadows)
        {
            bool on = Intensity > 0.001f && Radius > 0.01f;
            if (_light.enabled != on)
                _light.enabled = on;
            if (!on)
                return;

            _light.intensity = Intensity;
            _light.range = Range;
            _light.color = LightColor;
            _light.transform.localPosition = Position;
            if (_light.shadows != shadows)
                _light.shadows = shadows;
        }
    }
}
