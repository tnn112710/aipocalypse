using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace MB13.Aipocalypse.Core.SpawnSystem
{
    // Reuses spawnables per prefab, made with the scene resolver (injected before Awake). Prewarmed ones wake once off-stage
    // (Awake and Start paid while loading: a pooled goblin then wakes in ~1 ms instead of ~3.5) and wait under an inactive
    // parking root. Taken ones live at the scene root: a shared parent serialises animation write-back.
    // Owners reserve how many of a prefab they keep pooled (a raid, an area); free copies past every reservation are destroyed
    // once the prefab has gone unused for IdleTrimDelay, or soon after their owner releases them, TrimPerFrame a frame.
    public sealed class PrefabPool : ITickable, IDisposable
    {
        static readonly ProfilerMarker PrewarmMarker = new("Aipocalypse.PrefabPool.Prewarm");
        static readonly ProfilerMarker TrimMarker = new("Aipocalypse.PrefabPool.Trim");

        readonly IObjectResolver _resolver;
        readonly SpawnSystemSettings _settings;
        readonly Dictionary<Spawnable, Stack<Spawnable>> _free = new();
        readonly Dictionary<Spawnable, int> _made = new();
        readonly Dictionary<Spawnable, int> _wanted = new();
        readonly Dictionary<(object Owner, Spawnable Prefab), int> _reserved = new();
        readonly Dictionary<Spawnable, float> _lastUsed = new();
        readonly HashSet<Spawnable> _released = new();
        readonly List<Spawnable> _prefabs = new();
        readonly List<(object Owner, Spawnable Prefab)> _releasing = new();
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

        // Copies of `prefab` kept pooled: every owner's reservation added up (owners can need theirs at the same time).
        public int Kept(Spawnable prefab)
        {
            int kept = 0;
            foreach (KeyValuePair<(object Owner, Spawnable Prefab), int> reservation in _reserved)
                if (reservation.Key.Prefab == prefab)
                    kept += reservation.Value;
            return kept;
        }

        // How many free copies to destroy now: those past the reservations, once the prefab sat unused `idleFor` seconds past
        // `delay` (0 = never) or its owner let them go (`released`), at most `budget`.
        public static int ToTrim(int made, int free, int kept, float idleFor, float delay, bool released, int budget)
        {
            int surplus = Mathf.Min(free, made - kept);
            if (surplus <= 0 || budget <= 0)
                return 0;
            bool due = released || (delay > 0f && idleFor >= delay);
            return due ? Mathf.Min(surplus, budget) : 0;
        }

        // An instance of `prefab` at `position`, active at the scene root; SpawnService then hands it its SpawnInfo.
        public Spawnable Get(Spawnable prefab, Vector3 position, Quaternion rotation)
        {
            _lastUsed[prefab] = Time.time;
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

        // Makes sure `count` instances of `prefab` exist, PrewarmPerFrame created and woken a frame. With an `owner` it reserves
        // them (replacing its earlier reservation of the prefab) and they're kept pooled until it calls Release; without one
        // they're trimmed like any copy once unused for IdleTrimDelay.
        public void Prewarm(Spawnable prefab, int count, object owner = null)
        {
            if (prefab == null)
                return;
            if (owner != null)
            {
                _reserved[(owner, prefab)] = Mathf.Max(0, count);
                _released.Remove(prefab);
            }
            Known(prefab);
            _lastUsed[prefab] = Time.time;
            if (owner != null)
                count = Kept(prefab);
            if (count <= Made(prefab) || (_wanted.TryGetValue(prefab, out int wanted) && wanted >= count))
                return;
            _wanted[prefab] = count;
            if (!_warming.Contains(prefab))
                _warming.Add(prefab);
        }

        // Drops every reservation `owner` made: copies no one else reserves are trimmed over the next frames (busy ones once
        // they're back).
        public void Release(object owner)
        {
            if (owner == null)
                return;
            _releasing.Clear();
            foreach (KeyValuePair<(object Owner, Spawnable Prefab), int> reservation in _reserved)
                if (reservation.Key.Owner == owner)
                    _releasing.Add(reservation.Key);
            foreach ((object Owner, Spawnable Prefab) key in _releasing)
            {
                _reserved.Remove(key);
                _released.Add(key.Prefab);
            }
        }

        public void Tick()
        {
            ParkStaged();
            Trim();
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

        // Destroys free copies nobody keeps, TrimPerFrame a frame; prefabs still prewarming wait.
        void Trim()
        {
            int budget = _settings.TrimPerFrame;
            if (budget <= 0 || _staged.Count > 0)
                return;
            float now = Time.time;
            for (int i = 0; i < _prefabs.Count && budget > 0; i++)
            {
                Spawnable prefab = _prefabs[i];
                int free = Free(prefab);
                bool released = _released.Contains(prefab);
                if ((free == 0 && !released) || _warming.Contains(prefab))
                    continue;
                int made = Made(prefab);
                int kept = Kept(prefab);
                int trim = ToTrim(made, free, kept, now - _lastUsed[prefab], _settings.IdleTrimDelay, released, budget);
                if (trim == 0)
                {
                    if (released && made <= kept)
                        _released.Remove(prefab);
                    continue;
                }
                using var _ = TrimMarker.Auto();
                Stack<Spawnable> stack = _free[prefab];
                for (int k = 0; k < trim && stack.Count > 0; k++)
                {
                    Spawnable instance = stack.Pop();
                    _made[prefab] = Made(prefab) - 1;
                    if (instance != null)
                        Object.Destroy(instance.gameObject);
                }
                // A later prewarm builds them again.
                _wanted[prefab] = Mathf.Min(_wanted.TryGetValue(prefab, out int wanted) ? wanted : 0, Made(prefab));
                budget -= trim;
            }
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
            Known(prefab);
            _made[prefab] = Made(prefab) + 1;
            return instance;
        }

        void Known(Spawnable prefab)
        {
            if (_lastUsed.ContainsKey(prefab))
                return;
            _lastUsed[prefab] = Time.time;
            _prefabs.Add(prefab);
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
