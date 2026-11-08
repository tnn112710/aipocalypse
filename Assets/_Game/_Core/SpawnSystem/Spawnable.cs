using UnityEngine;

namespace MB13.Aipocalypse.Core.SpawnSystem
{
    // Root of anything SpawnService brings into play (enemies, animals, flocks, items, props): which prefab and pool it belongs
    // to, its state, and the ISpawnable parts told about each life. Disabling it hands it back to its pool.
    [DisallowMultipleComponent]
    public sealed class Spawnable : MonoBehaviour
    {
        ISpawnable[] _parts;
        SpawnState _state = SpawnState.Active;

        // The prefab it was made from and the pool it returns to; null for objects placed in the scene.
        public Spawnable Prefab { get; internal set; }
        internal PrefabPool Pool { get; set; }
        internal SpawnService Service { get; set; }
        internal bool InPool { get; set; }

        public SpawnState State
        {
            get => _state;
            internal set => _state = value;
        }

        // In play and not withdrawn, dead or used up.
        public bool IsAlive => _state == SpawnState.Active && gameObject.activeInHierarchy;
        // Counts lives: a handle tells a reused instance from the one it spawned.
        public int Life { get; private set; }
        public SpawnInfo Info { get; private set; }
        public SpawnCard Card => Info.Card;

        // Dead or used up but still in the world (a body lying, a coin picked up mid-animation): stops counting as alive and
        // goes back to its pool when it disables itself.
        public void Retire()
        {
            if (_state != SpawnState.Active && _state != SpawnState.Leaving)
                return;
            SpawnState was = _state;
            _state = SpawnState.Retired;
            Info.Handle?.MemberSpent();
            if (was == SpawnState.Active)
                Service?.Ended(this);
        }

        // From SpawnService once it's placed and active.
        internal void Spawned(SpawnService service, in SpawnInfo info)
        {
            Service = service;
            Info = info;
            Life++;
            _state = SpawnState.Active;
            _parts ??= GetComponentsInChildren<ISpawnable>(true);
            foreach (ISpawnable part in _parts)
                part.OnSpawned(info);
        }

        // Withdrawn by SpawnService (DespawnMode.WhenUnseen).
        internal void Leave()
        {
            if (_state != SpawnState.Active)
                return;
            _state = SpawnState.Leaving;
            Service?.Ended(this);
        }

        void OnDisable()
        {
            if (_state == SpawnState.Active)
                Service?.Ended(this);
            if (Pool == null)
                return;
            if (_state != SpawnState.Warming && _state != SpawnState.Pooled)
            {
                _parts ??= GetComponentsInChildren<ISpawnable>(true);
                foreach (ISpawnable part in _parts)
                    part.OnDespawned();
            }
            _state = SpawnState.Pooled;
            Pool.Return(this);
        }
    }
}
