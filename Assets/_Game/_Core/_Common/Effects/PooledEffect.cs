using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Aipocalypse.Core.Effects
{
    // Root of a reusable visual (muzzle flash, tracer, impact, bullet hole, blood). EffectPool spawns and returns it.
    [DisallowMultipleComponent]
    public sealed class PooledEffect : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Returned to the pool after this many seconds; 0 = only when the code that spawned it returns it")]
        float _lifetime = 2f;

        [SerializeField, Min(0), Tooltip("At most this many alive at once; spawning more recycles the oldest. 0 = no limit")]
        int _maxActive;

        ParticleSystem[] _particles;
        TrailRenderer[] _trails;
        DecalProjector _decal;
        Vector3 _decalSize;
        bool _decalSearched;

        public float Lifetime => _lifetime;
        public int MaxActive => _maxActive;

        // Set by EffectPool.
        internal PooledEffect Prefab { get; set; }
        internal int SpawnId { get; set; }
        internal bool IsSpawned { get; set; }

        // After it's been moved into place: no trail streak from where it was last used, particles start over.
        // Found on first use rather than in Awake, which hasn't run yet when it spawns under an inactive parent.
        public void Restart()
        {
            _particles ??= GetComponentsInChildren<ParticleSystem>(true);
            _trails ??= GetComponentsInChildren<TrailRenderer>(true);
            foreach (TrailRenderer trail in _trails)
                trail.Clear();
            foreach (ParticleSystem particles in _particles)
            {
                particles.Clear(false);
                particles.Play(false);
            }
        }

        // Decals are scale-invariant so a scaled parent can't stretch them; their width and height scale here instead.
        public void ScaleDecal(float scale)
        {
            if (!_decalSearched)
            {
                _decal = GetComponentInChildren<DecalProjector>(true);
                if (_decal != null)
                    _decalSize = _decal.size;
                _decalSearched = true;
            }
            if (_decal != null)
                _decal.size = new Vector3(_decalSize.x * scale, _decalSize.y * scale, _decalSize.z);
        }
    }
}
