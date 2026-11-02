using System.Collections.Generic;
using Aipocalypse.Core.Entities;
using Aipocalypse.Core.PlayerSystem;

namespace Aipocalypse.Core.EnemySystem
{
    // Everything enemies may target: the active player (from PlayerRegistry) plus whatever registers itself (crops, animals
    // later). Barriers in the way (fence segments, gates) are kept apart: they're only searched when the hunt has no path.
    public sealed class TargetRegistry
    {
        readonly PlayerRegistry _players;
        readonly List<ITargetable> _others = new();
        readonly List<IBreachable> _barriers = new();

        // `players` may be null (tests, scenes without a player system).
        public TargetRegistry(PlayerRegistry players) => _players = players;

        // Targets enemies hunt (the player first), not counting barriers.
        public int Count => _others.Count + (Player != null ? 1 : 0);
        public ITargetable this[int index] => Player != null ? index == 0 ? Player : _others[index - 1] : _others[index];
        public IReadOnlyList<IBreachable> Barriers => _barriers;

        ITargetable Player => _players?.Current;

        public void Add(ITargetable target)
        {
            if (target is IBreachable barrier)
            {
                if (!_barriers.Contains(barrier))
                    _barriers.Add(barrier);
                return;
            }
            if (target != null && !_others.Contains(target))
                _others.Add(target);
        }

        public void Remove(ITargetable target)
        {
            if (target is IBreachable barrier)
                _barriers.Remove(barrier);
            else
                _others.Remove(target);
        }
    }
}
