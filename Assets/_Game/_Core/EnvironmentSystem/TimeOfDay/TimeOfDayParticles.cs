using UnityEngine;

namespace MB13.Aipocalypse.Core.EnvironmentSystem
{
    // Emitters that only run part of the day (butterflies by day, fireflies at night) or run more at some hours (morning mist).
    // Scales the emission rate; at a rate of 0 the emitter stops (bursts too) and its live particles finish naturally.
    public sealed class TimeOfDayParticles : TimeOfDayReactor
    {
        [SerializeField, Tooltip("Empty: every ParticleSystem under this object")]
        ParticleSystem[] _systems = System.Array.Empty<ParticleSystem>();
        [SerializeField, Min(0f), Tooltip("Emission rate multiplier at weight 0. 0 = stop emitting")]
        float _offRate;
        [SerializeField, Min(0f), Tooltip("Emission rate multiplier at weight 1")]
        float _onRate = 1f;
        [SerializeField, Tooltip("Point each emitter's +Y at the sun (god rays)")]
        bool _alignToSun;

        float[] _rates;
        Renderer[] _renderers;
        Vector3 _alignedTo;

        void Awake()
        {
            if (_systems.Length == 0)
                _systems = GetComponentsInChildren<ParticleSystem>(true);
            _rates = new float[_systems.Length];
            _renderers = new Renderer[_systems.Length];
            for (int i = 0; i < _systems.Length; i++)
            {
                if (_systems[i] == null)
                    continue;
                _rates[i] = _systems[i].emission.rateOverTimeMultiplier;
                _renderers[i] = _systems[i].GetComponent<ParticleSystemRenderer>();
            }
        }

        public override void React(EnvironmentDirector director, float deltaTime)
        {
            // Vector3.Angle is 0 against a zero vector, so the first alignment has to be forced.
            if (_alignToSun && (_alignedTo == Vector3.zero || Vector3.Angle(_alignedTo, director.SunDirection) > 0.1f))
            {
                _alignedTo = director.SunDirection;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, _alignedTo);
                foreach (ParticleSystem system in _systems)
                {
                    if (system != null)
                        system.transform.rotation = rotation;
                }
            }
            base.React(director, deltaTime);
        }

        protected override void Apply(float weight)
        {
            float multiplier = Mathf.Lerp(_offRate, _onRate, weight);
            bool emit = multiplier > 0.001f;
            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem system = _systems[i];
                if (system == null)
                    continue;
                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTimeMultiplier = _rates[i] * multiplier;
                // Culled emitters (Pause culling) don't simulate, so their particles would wait off screen and show up
                // later, e.g. god rays at night after a clock jump. Unseen ones are cleared instead.
                if (!emit && system.isEmitting)
                    system.Stop(false, _renderers[i] != null && !_renderers[i].isVisible
                        ? ParticleSystemStopBehavior.StopEmittingAndClear
                        : ParticleSystemStopBehavior.StopEmitting);
                else if (emit && !system.isEmitting)
                    system.Play(false);
            }
        }
    }
}
