using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // Advances the enemy bodies that are being knocked back or lying dead, on scaled time (pause freezes them). Bodies join
    // when hit and leave when they come to rest or disable themselves; idle enemies cost nothing here. Bodies holding loot
    // stay until emptied, at most MaxLootBodies of them (the oldest sinks first), and all sink at dawn.
    public sealed class EnemyBodyManager : ITickable, IDisposable
    {
        static readonly ProfilerMarker React = new("Aipocalypse.EnemyBodyManager.React");
        readonly List<EnemyBody> _active = new(64);
        readonly List<EnemyBody> _lootBodies = new(32);
        readonly EnemySystemSettings _settings;
        readonly IDisposable _dawn;

        public EnemyBodyManager(EnemySystemSettings settings, ISubscriber<DayPhaseChanged> phases)
        {
            _settings = settings;
            _dawn = phases.Subscribe(_ => ExpireLoot(), e => e.Current == DayPhase.Dawn);
        }

        public int Count => _active.Count;
        // Bodies lying with loot, oldest first.
        public IReadOnlyList<EnemyBody> LootBodies => _lootBodies;

        public void Track(EnemyBody body)
        {
            if (body.Tracked)
                return;
            body.Tracked = true;
            _active.Add(body);
        }

        public void Tick() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            using (React.Auto())
            {
                for (int i = _active.Count - 1; i >= 0; i--)
                {
                    EnemyBody body = _active[i];
                    if (body != null && body.Advance(deltaTime))
                        continue;
                    if (body != null)
                        body.Tracked = false;
                    int last = _active.Count - 1;
                    _active[i] = _active[last];
                    _active.RemoveAt(last);
                }
            }
        }

        // Every body lying with loot loses it and sinks (dawn, a cheat).
        public void ExpireLoot()
        {
            for (int i = _lootBodies.Count - 1; i >= 0; i--)
            {
                EnemyBody body = _lootBodies[i];
                _lootBodies.RemoveAt(i);
                if (body != null)
                    body.ExpireLoot();
            }
        }

        public void Dispose() => _dawn.Dispose();

        internal void AddLootBody(EnemyBody body)
        {
            if (_lootBodies.Contains(body))
                return;
            _lootBodies.Add(body);
            while (_lootBodies.Count > Mathf.Max(0, _settings.MaxLootBodies))
            {
                EnemyBody oldest = _lootBodies[0];
                _lootBodies.RemoveAt(0);
                if (oldest != null)
                    oldest.ExpireLoot();
            }
        }

        internal void RemoveLootBody(EnemyBody body) => _lootBodies.Remove(body);
    }
}
