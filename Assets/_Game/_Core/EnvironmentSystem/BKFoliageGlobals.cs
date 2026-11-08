using UnityEngine;

namespace MB13.Aipocalypse.Core.EnvironmentSystem
{
    // Sets the scene-wide values BK Pure Nature plants read (BK/Grass shader). Unset, they are 0: no micro wind, and
    // plants fade out from about 50 m. BK_EnvironmentManager would set them too but also draws its own clouds.
    [ExecuteAlways]
    public sealed class BKFoliageGlobals : MonoBehaviour
    {
        static readonly int RenderDistanceId = Shader.PropertyToID("GrassRenderDist");
        static readonly int MicroPowerId = Shader.PropertyToID("MicroPower");
        static readonly int MicroSpeedId = Shader.PropertyToID("MicroSpeed");
        static readonly int MicroFrequencyId = Shader.PropertyToID("MicroFrequency");

        [SerializeField, Min(0f), Tooltip("Plants start fading at half this distance; keep it at 2x the terrain detail distance")] float _renderDistance = 240f;
        [SerializeField, Range(0f, 1f)] float _microPower = 0.1f;
        [SerializeField, Min(0f)] float _microSpeed = 0.5f;
        [SerializeField, Min(0f)] float _microFrequency = 5f;

        // Set by the scene wind (WeatherWind); the tuned values are the Breezy wind.
        float _powerScale = 1f;
        float _speedScale = 1f;

        void OnEnable() => Apply();

        void OnValidate() => Apply();

        public void SetWindScale(float power, float speed)
        {
            if (power == _powerScale && speed == _speedScale)
                return;
            _powerScale = power;
            _speedScale = speed;
            Apply();
        }

        void Apply()
        {
            Shader.SetGlobalFloat(RenderDistanceId, _renderDistance);
            Shader.SetGlobalFloat(MicroPowerId, Mathf.Clamp01(_microPower * _powerScale));
            Shader.SetGlobalFloat(MicroSpeedId, _microSpeed * _speedScale);
            Shader.SetGlobalFloat(MicroFrequencyId, _microFrequency);
        }
    }
}
