using UnityEngine;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // The scene's one wind writer: the vendor foliage wind values (Raygeas trees and grass, HIGHLANDS / shared Raygeas plants,
    // BK flutter through BKFoliageGlobals) and the wind zone Stylized Grass's WindController reads, all as the tuned values
    // times the weather's strength and speed (Breezy = 1). Blows the way this object faces. Replaces the Raygeas wind drivers.
    [ExecuteAlways]
    public sealed class WeatherWind : MonoBehaviour
    {
        static readonly int TreeForceId = Shader.PropertyToID("RaygeasTreeWindForce");
        static readonly int TreeSpeedId = Shader.PropertyToID("RaygeasTreeWindSpeed");
        static readonly int TreeWavesScaleId = Shader.PropertyToID("RaygeasTreeWindWavesScale");
        static readonly int TreeFlowDensityId = Shader.PropertyToID("RaygeasTreeWindFlowDensity");
        static readonly int GrassForceId = Shader.PropertyToID("RaygeasGrassWindForce");
        static readonly int GrassSpeedId = Shader.PropertyToID("RaygeasGrassWindSpeed");
        static readonly int DirectionId = Shader.PropertyToID("RaygeasWindDirection");
        static readonly int GlobalForceId = Shader.PropertyToID("RAYGlobalWindForce");
        static readonly int GlobalSpeedId = Shader.PropertyToID("RAYGlobalWindSpeed");
        static readonly int GlobalWavesScaleId = Shader.PropertyToID("RAYGlobalWavesScale");
        static readonly int GlobalFlowDensityId = Shader.PropertyToID("RAYGlobalFlowDensity");
        static readonly int GlobalDirectionId = Shader.PropertyToID("RAYGlobalDirection");

        [SerializeField, Tooltip("BK plants' flutter, scaled with the wind")] BKFoliageGlobals _bk;
        [SerializeField, Tooltip("Read by Stylized Grass's WindController on the same object: Main = strength, Turbulence = gusts")]
        WindZone _zone;
        [Header("Raygeas trees and grass (tuned values at Breezy)")]
        [SerializeField] float _treeForce = 1.5f;
        [SerializeField] float _treeSpeed = 15f;
        [SerializeField] float _treeWavesScale = 0.5f;
        [SerializeField] float _treeFlowDensity = 0.5f;
        [SerializeField] float _grassForce = 1f;
        [SerializeField] float _grassSpeed = 2f;
        [Header("HIGHLANDS and shared Raygeas plants (tuned values at Breezy)")]
        [SerializeField] float _globalForce = 1f;
        [SerializeField] float _globalSpeed = 1f;
        [SerializeField] float _globalWavesScale = 1f;
        [SerializeField] float _globalFlowDensity = 1f;

        float _strength = 1f;
        float _speed = 1f;
        float _gust = 1f;

        public Vector3 Direction => transform.forward;

        // Multipliers on the tuned sway: strength = amplitude, speed = sway speed, gust = Stylized Grass gusts.
        public void Set(float strength, float speed, float gust)
        {
            if (strength == _strength && speed == _speed && gust == _gust)
                return;
            _strength = strength;
            _speed = speed;
            _gust = gust;
            Apply();
        }

        void OnEnable() => Apply();

        void OnValidate() => Apply();

        void Update()
        {
            if (!transform.hasChanged)
                return;
            transform.hasChanged = false;
            Apply();
        }

        void Apply()
        {
            Vector3 direction = transform.forward;
            Shader.SetGlobalFloat(TreeForceId, _treeForce * _strength);
            Shader.SetGlobalFloat(TreeSpeedId, _treeSpeed * _speed);
            Shader.SetGlobalFloat(TreeWavesScaleId, _treeWavesScale);
            Shader.SetGlobalFloat(TreeFlowDensityId, _treeFlowDensity);
            Shader.SetGlobalFloat(GrassForceId, _grassForce * _strength);
            Shader.SetGlobalFloat(GrassSpeedId, _grassSpeed * _speed);
            Shader.SetGlobalVector(DirectionId, direction);
            Shader.SetGlobalFloat(GlobalForceId, _globalForce * _strength);
            Shader.SetGlobalFloat(GlobalSpeedId, _globalSpeed * _speed);
            Shader.SetGlobalFloat(GlobalWavesScaleId, _globalWavesScale);
            Shader.SetGlobalFloat(GlobalFlowDensityId, _globalFlowDensity);
            Shader.SetGlobalVector(GlobalDirectionId, direction);
            if (_bk != null)
                _bk.SetWindScale(_strength, _speed);
            if (_zone != null)
            {
                _zone.windMain = _strength;
                _zone.windTurbulence = _gust;
            }
        }
    }
}
