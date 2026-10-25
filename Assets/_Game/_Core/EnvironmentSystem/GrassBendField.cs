using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // One Stylized Grass bender for every crowd mover (enemies, animals): a sphere mesh particle per registered transform,
    // moved each frame, so they all bend grass in one draw with no per-mover component. The GrassBender sits on this object.
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class GrassBendField : MonoBehaviour
    {
        const float Lifetime = 1e6f;

        [SerializeField, Min(1), Tooltip("Most movers bending grass at once; the rest are skipped")] int _capacity = 512;

        readonly List<Transform> _movers = new();
        readonly List<float> _radii = new();
        ParticleSystem _particles;
        ParticleSystem.Particle[] _buffer;

        public int Count => _movers.Count;

        // Radius in metres around the mover's pivot (its feet). Movers remove themselves before they are destroyed.
        public void Add(Transform mover, float radius)
        {
            if (mover == null || radius <= 0f || _movers.Contains(mover))
                return;
            _movers.Add(mover);
            _radii.Add(radius);
        }

        public void Remove(Transform mover)
        {
            int index = _movers.IndexOf(mover);
            if (index < 0)
                return;
            int last = _movers.Count - 1;
            _movers[index] = _movers[last];
            _radii[index] = _radii[last];
            _movers.RemoveAt(last);
            _radii.RemoveAt(last);
        }

        void Awake()
        {
            _particles = GetComponent<ParticleSystem>();
            _buffer = new ParticleSystem.Particle[_capacity];
        }

        void LateUpdate()
        {
            int count = Mathf.Min(_movers.Count, _buffer.Length);
            for (int i = 0; i < count; i++)
            {
                ref ParticleSystem.Particle particle = ref _buffer[i];
                particle.position = _movers[i].position;
                particle.startSize = _radii[i] * 2f;
                particle.startColor = Color.white;
                particle.startLifetime = Lifetime;
                particle.remainingLifetime = Lifetime;
            }
            _particles.SetParticles(_buffer, count);
        }
    }
}
