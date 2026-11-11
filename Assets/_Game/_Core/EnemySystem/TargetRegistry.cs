using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.PlayerSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // Everything enemies may target: the active player (from PlayerRegistry) plus whatever registers itself (crops, animals
    // later). Barriers in the way (fence segments, gates) are kept apart: they're only searched when the hunt has no path,
    // and their middle is the Farm, what raiders go for before they notice the player.
    public sealed class TargetRegistry : IDisposable
    {
        readonly PlayerRegistry _players;
        readonly List<ITargetable> _others = new();
        readonly List<IBreachable> _barriers = new();
        FarmTarget _farm;

        // `players` may be null (tests, scenes without a player system).
        public TargetRegistry(PlayerRegistry players) => _players = players;

        // Targets enemies hunt (the player first), not counting barriers.
        public int Count => _others.Count + (Player != null ? 1 : 0);
        public ITargetable this[int index] => Player != null ? index == 0 ? Player : _others[index - 1] : _others[index];
        public IReadOnlyList<IBreachable> Barriers => _barriers;
        // Goes up whenever a barrier starts or stops blocking (broke, stood again, a gate opened or shut): hunters choose again.
        public int BarrierVersion { get; private set; }
        public ITargetable Player => _players?.Current;
        // The middle of the fence ring; null in a scene without barriers.
        public ITargetable Farm => _barriers.Count > 0 ? _farm : null;

        public void Add(ITargetable target)
        {
            if (target is IBreachable barrier)
            {
                if (_barriers.Contains(barrier))
                    return;
                _barriers.Add(barrier);
                (_farm ??= new FarmTarget()).Place(_barriers);
                return;
            }
            if (target != null && !_others.Contains(target))
                _others.Add(target);
        }

        public void BarriersChanged() => BarrierVersion++;

        public void Remove(ITargetable target)
        {
            if (target is IBreachable barrier)
            {
                if (_barriers.Remove(barrier))
                    _farm?.Place(_barriers);
            }
            else
                _others.Remove(target);
        }

        // Scene scope disposed (and tests): drops the Farm's object.
        public void Dispose()
        {
            _farm?.Dispose();
            _farm = null;
        }

        // Where the Farm stands, for probes and tests.
        public Vector3 FarmPosition => Farm != null ? Farm.Transform.position : Vector3.zero;
    }
}
