using System.Collections.Generic;
using Aipocalypse.Core.Entities;
using Aipocalypse.Core.PlayerSystem;

namespace Aipocalypse.Core.EnemySystem
{
    // Everything enemies may target: the active player (from PlayerRegistry) plus whatever registers itself (fences, crops,
    // animals later).
    public sealed class TargetRegistry
    {
        readonly PlayerRegistry _players;
        readonly List<ITargetable> _others = new();

        // `players` may be null (tests, scenes without a player system).
        public TargetRegistry(PlayerRegistry players) => _players = players;

        public int Count => _others.Count + (Player != null ? 1 : 0);
        public ITargetable this[int index] => Player != null ? index == 0 ? Player : _others[index - 1] : _others[index];

        ITargetable Player => _players?.Current;

        public void Add(ITargetable target)
        {
            if (target != null && !_others.Contains(target))
                _others.Add(target);
        }

        public void Remove(ITargetable target) => _others.Remove(target);
    }
}
