using UnityEngine;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Lamps, lanterns, window lights: fades lights in over their hours. Author each light at its "on" intensity.
    public sealed class TimeOfDayLight : TimeOfDayReactor
    {
        [SerializeField, Tooltip("Empty: every Light under this object")]
        Light[] _lights = System.Array.Empty<Light>();

        float[] _intensities;

        void Awake()
        {
            if (_lights.Length == 0)
                _lights = GetComponentsInChildren<Light>(true);
            _intensities = new float[_lights.Length];
            for (int i = 0; i < _lights.Length; i++)
                _intensities[i] = _lights[i].intensity;
        }

        protected override void Apply(float weight)
        {
            for (int i = 0; i < _lights.Length; i++)
            {
                Light light = _lights[i];
                if (light == null)
                    continue;
                light.intensity = _intensities[i] * weight;
                light.enabled = weight > 0.001f;
            }
        }
    }
}
