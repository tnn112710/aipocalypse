using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Aipocalypse.Core.SpawnSystem
{
    // Reuses spawnables per prefab, made with the scene resolver (injected before Awake). Prewarmed ones wake once off-stage
    // (Awake and Start paid while loading: a pooled goblin then wakes in ~1 ms instead of ~3.5) and wait under an inactive
    // parking root. Taken ones live at the scene root: a shared parent serialises animation write-back.
    public sealed class PrefabPool : ITickable, IDisposable
    {
        static readonly ProfilerMarker PrewarmMarker = new("Aipocalypse.PrefabPool.Prewarm");

        readonly IObjectResolver _resolver;
        readonly SpawnSystemSettings _settings;
        readonly Dictionary<Spawnable, Stack<Spawnable>> _free = new();
        readonly Dictionary<Spawnable, int> _made = new();
        readonly Dictionary<Spawnable, int> _wanted = new();
        readonly List<Spawnable> _warming = new();
        readonly List<Spawnable> _staged = new();
        int _stagedFrame;
        Transform _parking;
        bool _disposed;

        public PrefabPool(IObjectResolver resolver, SpawnSystemSettings settings)
        {
            _resolver = resolver;
            _settings = settings;
        }

        public int Made(Spawnable prefab) => prefab != null && _made.TryGetValue(prefab, out int made) ? made : 0;
        public int Free(Spawnable prefab) => prefab != null && _free.TryGetValue(prefab, out Stack<Spawnable> free) ? free.Count : 0;
        // Prefabs still being prewarmed.
        public bool Warming => _warming.Count > 0 || _staged.Count > 0;

        // An instance of `prefab` at `position`, active at the scene root; SpawnService then hands it its SpawnInfo.
        public Spawnable Get(Spawnable prefab, Vector3 position, Quaternion rotation)
        {
            Spawnable instance = TakeFree(prefab);
            if (instance == null)
                return Create(prefab, position, rotation);
            // Placed before it wakes, so whatever reads its position on enable (an agent's entity, a home point) starts there.
            Transform body = instance.transform;
            body.SetPositionAndRotation(position, rotation);
            if (body.parent != null)
                body.SetParent(null, true);
            instance.gameObject.SetActive(true);
            return instance;
        }

        // Makes sure `count` instances of `prefab` exist, PrewarmPerFrame created and woken a frame.
        public void Prewarm(Spawnable prefab, int count)
        {
            if (prefab == null || count <= Made(prefab) || (_wanted.TryGetValue(prefab, out int wanted) && wanted >= count))
                return;
            _wanted[prefab] = count;
            if (!_warming.Contains(prefab))
                _warming.Add(prefab);
        }

        public void Tick()
        {
            ParkStaged();
            if (_warming.Count == 0)
                return;
            using var _ = PrewarmMarker.Auto();
            int budget = Mathf.Max(1, _settings.PrewarmPerFrame);
            for (int i = _warming.Count - 1; i >= 0 && budget > 0; i--)
            {
                Spawnable prefab = _warming[i];
                while (budget > 0 && Made(prefab) < _wanted[prefab])
                {
                    Spawnable instance = Create(prefab, _settings.StagingPoint, Quaternion.identity);
                    instance.State = SpawnState.Warming;
                    _staged.Add(instance);
                    _stagedFrame = Time.frameCount;
                    budget--;
                }
                if (Made(prefab) >= _wanted[prefab])
                    _warming.RemoveAt(i);
            }
        }

        // From Spawnable.OnDisable.
        internal void Return(Spawnable instance)
        {
            if (_disposed || instance == null || instance.InPool)
                return;
            instance.InPool = true;
            instance.State = SpawnState.Pooled;
            if (!_free.TryGetValue(instance.Prefab, out Stack<Spawnable> free))
                _free[instance.Prefab] = free = new Stack<Spawnable>();
            free.Push(instance);
        }

        public void Dispose()
        {
            _disposed = true;
            _free.Clear();
            _warming.Clear();
            _staged.Clear();
        }

        // Instances woken last frame have run Start by now: off and parked.
        void ParkStaged()
        {
            if (_staged.Count == 0 || Time.frameCount <= _stagedFrame)
                return;
            foreach (Spawnable instance in _staged)
            {
                if (instance == null)
                    continue;
                instance.gameObject.SetActive(false);
                instance.transform.SetParent(Parking, false);
            }
            _staged.Clear();
        }

        Spawnable TakeFree(Spawnable prefab)
        {
            if (!_free.TryGetValue(prefab, out Stack<Spawnable> free))
                return null;
            while (free.Count > 0)
            {
                Spawnable instance = free.Pop();
                if (instance == null)
                {
                    _made[prefab] = Made(prefab) - 1;
                    continue;
                }
                instance.InPool = false;
                return instance;
            }
            return null;
        }

        Spawnable Create(Spawnable prefab, Vector3 position, Quaternion rotation)
        {
            Spawnable instance = _resolver.Instantiate(prefab, position, rotation);
            instance.name = prefab.name;
            instance.Prefab = prefab;
            instance.Pool = this;
            _made[prefab] = Made(prefab) + 1;
            return instance;
        }

        Transform Parking
        {
            get
            {
                if (_parking != null)
                    return _parking;
                var parking = new GameObject("Spawn pool (parked)");
                parking.SetActive(false);
                return _parking = parking.transform;
            }
        }
    }
}
