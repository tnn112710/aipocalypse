using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Aipocalypse.Core.EnemySystem
{
    // Reuses enemies per prefab (made with the scene resolver, injected before Awake); back when disabled or despawned.
    // Prewarmed ones wait under an inactive parking root, never woken until taken (PrewarmPerFrame a frame).
    public sealed class EnemyPool : ITickable, IDisposable
    {
        static readonly ProfilerMarker PrewarmMarker = new("Aipocalypse.EnemyPool.Prewarm");

        readonly IObjectResolver _resolver;
        readonly EnemySystemSettings _settings;
        readonly Dictionary<EnemyBrain, Stack<EnemyBrain>> _free = new();
        readonly Dictionary<EnemyBrain, int> _made = new();
        readonly Dictionary<EnemyBrain, int> _wanted = new();
        readonly List<EnemyBrain> _warming = new();
        Transform _root;
        Transform _parking;
        bool _disposed;

        public EnemyPool(IObjectResolver resolver, EnemySystemSettings settings)
        {
            _resolver = resolver;
            _settings = settings;
        }

        public int Made(EnemyBrain prefab) => _made.TryGetValue(prefab, out int made) ? made : 0;
        public int Free(EnemyBrain prefab) => _free.TryGetValue(prefab, out Stack<EnemyBrain> free) ? free.Count : 0;

        // An enemy of `prefab` at `position`, active and (from its first Start on) thinking.
        public EnemyBrain Get(EnemyBrain prefab, Vector3 position, Quaternion rotation)
        {
            EnemyBrain enemy = TakeFree(prefab);
            if (enemy == null)
                return Create(prefab, position, rotation, Root);
            // Placed before it wakes (a parked one wakes on leaving the parking root), so the agent's entity and the brain's
            // Home start there; the teleport clears old paths.
            enemy.transform.SetPositionAndRotation(position, rotation);
            enemy.transform.SetParent(Root, true);
            enemy.gameObject.SetActive(true);
            enemy.GetComponent<EnemyMover>().Teleport(position);
            return enemy;
        }

        // Makes sure `count` instances of `prefab` exist, created a few per frame.
        public void Prewarm(EnemyBrain prefab, int count)
        {
            if (prefab == null || count <= Made(prefab) || (_wanted.TryGetValue(prefab, out int wanted) && wanted >= count))
                return;
            _wanted[prefab] = count;
            if (!_warming.Contains(prefab))
                _warming.Add(prefab);
        }

        public void Tick()
        {
            if (_warming.Count == 0)
                return;
            using var _ = PrewarmMarker.Auto();
            int budget = _settings.PrewarmPerFrame;
            for (int i = _warming.Count - 1; i >= 0 && budget > 0; i--)
            {
                EnemyBrain prefab = _warming[i];
                while (budget > 0 && Made(prefab) < _wanted[prefab])
                {
                    Return(Create(prefab, Vector3.zero, Quaternion.identity, Parking));
                    budget--;
                }
                if (Made(prefab) >= _wanted[prefab])
                    _warming.RemoveAt(i);
            }
        }

        // From EnemyBrain.OnDisable.
        internal void Return(EnemyBrain enemy)
        {
            if (_disposed || enemy == null || enemy.InPool)
                return;
            enemy.InPool = true;
            if (!_free.TryGetValue(enemy.Prefab, out Stack<EnemyBrain> free))
                _free[enemy.Prefab] = free = new Stack<EnemyBrain>();
            free.Push(enemy);
        }

        public void Dispose()
        {
            _disposed = true;
            _free.Clear();
            _warming.Clear();
        }

        EnemyBrain TakeFree(EnemyBrain prefab)
        {
            if (!_free.TryGetValue(prefab, out Stack<EnemyBrain> free))
                return null;
            while (free.Count > 0)
            {
                EnemyBrain enemy = free.Pop();
                if (enemy == null)
                {
                    _made[prefab] = Made(prefab) - 1;
                    continue;
                }
                enemy.InPool = false;
                return enemy;
            }
            return null;
        }

        EnemyBrain Create(EnemyBrain prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            EnemyBrain enemy = _resolver.Instantiate(prefab, position, rotation, parent);
            enemy.Pool = this;
            enemy.Prefab = prefab;
            _made[prefab] = Made(prefab) + 1;
            return enemy;
        }

        Transform Root => _root != null ? _root : _root = new GameObject("Enemies").transform;

        Transform Parking
        {
            get
            {
                if (_parking != null)
                    return _parking;
                var parking = new GameObject("Enemies (parked)");
                parking.SetActive(false);
                return _parking = parking.transform;
            }
        }
    }
}
