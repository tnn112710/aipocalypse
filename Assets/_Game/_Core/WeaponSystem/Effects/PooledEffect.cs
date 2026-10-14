using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // Root of a reusable visual (muzzle flash, tracer, impact, bullet hole). EffectPool spawns and returns it.
    [DisallowMultipleComponent]
    public sealed class PooledEffect : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Returned to the pool after this many seconds; 0 = only when the code that spawned it returns it")]
        float _lifetime = 2f;

        [SerializeField, Min(0), Tooltip("At most this many alive at once; spawning more recycles the oldest. 0 = no limit")]
        int _maxActive;

        ParticleSystem[] _particles;
        TrailRenderer[] _trails;

        public float Lifetime => _lifetime;
        public int MaxActive => _maxActive;

        // Set by EffectPool.
        internal PooledEffect Prefab { get; set; }
        internal int SpawnId { get; set; }
        internal bool IsSpawned { get; set; }

        void Awake()
        {
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            _trails = GetComponentsInChildren<TrailRenderer>(true);
        }

        // After it's been moved into place: no trail streak from where it was last used, particles start over.
        public void Restart()
        {
            foreach (TrailRenderer trail in _trails)
                trail.Clear();
            foreach (ParticleSystem particles in _particles)
            {
                particles.Clear(false);
                particles.Play(false);
            }
        }
    }
}
