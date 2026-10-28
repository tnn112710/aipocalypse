using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VContainer.Unity;

namespace Aipocalypse.Core.WeaponSystem
{
    // One pool per effect prefab, and timed returns for effects with a lifetime. Central tick, no Update per effect.
    // Pooled objects live under a root in the active scene; when that scene unloads, the pools start over.
    public sealed class EffectPool : ITickable
    {
        sealed class Pool
        {
            public ObjectPool<PooledEffect> Objects;
            // Spawn order, oldest first; only kept for prefabs with a MaxActive.
            public readonly List<PooledEffect> Active = new();
        }

        struct Timed
        {
            public PooledEffect Effect;
            public int SpawnId;
            public float ReturnAt;
        }

        readonly Dictionary<PooledEffect, Pool> _pools = new();
        readonly List<Timed> _timed = new();
        Transform _root;
        int _nextSpawnId;

        public void Prewarm(PooledEffect prefab, int count)
        {
            if (prefab == null)
                return;
            Pool pool = GetPool(prefab);
            int missing = count - pool.Objects.CountAll;
            if (missing <= 0)
                return;

            var created = ListPool<PooledEffect>.Get();
            for (int i = 0; i < missing; i++)
                created.Add(pool.Objects.Get());
            foreach (PooledEffect effect in created)
                pool.Objects.Release(effect);
            ListPool<PooledEffect>.Release(created);
        }

        // `lifetime` < 0 uses the prefab's own; 0 = stays until Return.
        public PooledEffect Spawn(PooledEffect prefab, Vector3 position, Quaternion rotation, Transform parent = null, float lifetime = -1f)
        {
            if (prefab == null)
                return null;

            Pool pool = GetPool(prefab);
            bool capped = prefab.MaxActive > 0;
            if (capped && pool.Active.Count >= prefab.MaxActive)
            {
                // Destroyed with a parent (a dropped gun's muzzle flash) still counts until forgotten.
                for (int i = pool.Active.Count - 1; i >= 0; i--)
                {
                    if (pool.Active[i] == null)
                        Return(pool.Active[i]);
                }
                if (pool.Active.Count >= prefab.MaxActive)
                    Return(pool.Active[0]);
            }

            PooledEffect effect = pool.Objects.Get();
            effect.transform.SetParent(parent != null ? parent : _root, false);
            effect.transform.SetPositionAndRotation(position, rotation);
            effect.SpawnId = ++_nextSpawnId;
            effect.IsSpawned = true;
            if (capped)
                pool.Active.Add(effect);
            effect.gameObject.SetActive(true);
            effect.Restart();

            if (lifetime < 0f)
                lifetime = prefab.Lifetime;
            if (lifetime > 0f)
                _timed.Add(new Timed { Effect = effect, SpawnId = effect.SpawnId, ReturnAt = Time.time + lifetime });
            return effect;
        }

        public void Return(PooledEffect effect)
        {
            if (ReferenceEquals(effect, null) || !effect.IsSpawned || !_pools.TryGetValue(effect.Prefab, out Pool pool))
                return;

            effect.IsSpawned = false;
            if (effect.Prefab.MaxActive > 0)
                pool.Active.Remove(effect);
            // Destroyed with its parent (a dropped gun's muzzle flash): just forget it.
            if (effect != null)
                pool.Objects.Release(effect);
        }

        // Unless it's returned or recycled first; 0 returns it now.
        public void ReturnAfter(PooledEffect effect, float seconds)
        {
            if (seconds <= 0f)
            {
                Return(effect);
                return;
            }
            if (!ReferenceEquals(effect, null) && effect.IsSpawned)
                _timed.Add(new Timed { Effect = effect, SpawnId = effect.SpawnId, ReturnAt = Time.time + seconds });
        }

        public void Tick()
        {
            float now = Time.time;
            for (int i = _timed.Count - 1; i >= 0; i--)
            {
                Timed timed = _timed[i];
                // False once it was recycled early (MaxActive) or returned by code.
                bool current = !ReferenceEquals(timed.Effect, null) && timed.Effect.IsSpawned && timed.Effect.SpawnId == timed.SpawnId;
                if (current && timed.Effect != null && now < timed.ReturnAt)
                    continue;

                if (current)
                    Return(timed.Effect);
                // Order doesn't matter: swap-remove.
                int last = _timed.Count - 1;
                _timed[i] = _timed[last];
                _timed.RemoveAt(last);
            }
        }

        Pool GetPool(PooledEffect prefab)
        {
            EnsureRoot();
            if (_pools.TryGetValue(prefab, out Pool pool))
                return pool;

            pool = new Pool();
            pool.Objects = new ObjectPool<PooledEffect>(
                createFunc: () =>
                {
                    PooledEffect effect = Object.Instantiate(prefab, _root, false);
                    effect.name = prefab.name;
                    effect.Prefab = prefab;
                    effect.gameObject.SetActive(false);
                    return effect;
                },
                actionOnRelease: effect =>
                {
                    effect.gameObject.SetActive(false);
                    effect.transform.SetParent(_root, false);
                },
                actionOnDestroy: effect =>
                {
                    if (effect != null)
                        Object.Destroy(effect.gameObject);
                },
                collectionCheck: false);
            _pools.Add(prefab, pool);
            return pool;
        }

        void EnsureRoot()
        {
            if (_root != null)
                return;

            // The scene holding the old root unloaded and took every pooled object with it.
            _pools.Clear();
            _timed.Clear();
            _root = new GameObject("[Effects]").transform;
        }
    }
}
