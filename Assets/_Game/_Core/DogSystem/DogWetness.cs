using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // Rain soaks the dog; out of it after a soaking (under a roof, indoors, or the rain stops) it shakes itself dry once.
    // A short shower dries off by itself.
    public sealed class DogWetness
    {
        readonly DogSettings _settings;
        float _wet;

        public DogWetness(DogSettings settings) => _settings = settings;

        // True once, when it should shake.
        public bool Tick(float dt, bool rainedOn)
        {
            if (rainedOn)
            {
                _wet = Mathf.Min(_wet + dt, _settings.WetToShake);
                return false;
            }
            if (_settings.WetToShake > 0f && _wet >= _settings.WetToShake)
            {
                _wet = 0f;
                return true;
            }
            _wet = Mathf.Max(0f, _wet - dt);
            return false;
        }
    }
}
