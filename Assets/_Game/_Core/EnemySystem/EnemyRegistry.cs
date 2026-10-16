using System.Collections.Generic;

namespace Aipocalypse.Core.EnemySystem
{
    // The enemies in play as a dense list for EnemyManager to walk. Brains add themselves when they start or are re-enabled
    // and leave when disabled, so spawned and pooled enemies are ticked the same as scene-placed ones. Swap-remove: O(1).
    public sealed class EnemyRegistry
    {
        readonly List<EnemyBrain> _enemies = new(256);

        public int Count => _enemies.Count;
        public EnemyBrain this[int index] => _enemies[index];

        public void Add(EnemyBrain enemy)
        {
            if (enemy.RegistryIndex >= 0)
                return;
            enemy.RegistryIndex = _enemies.Count;
            _enemies.Add(enemy);
        }

        public void Remove(EnemyBrain enemy)
        {
            int index = enemy.RegistryIndex;
            if (index < 0)
                return;
            int last = _enemies.Count - 1;
            EnemyBrain moved = _enemies[last];
            _enemies[index] = moved;
            moved.RegistryIndex = index;
            _enemies.RemoveAt(last);
            enemy.RegistryIndex = -1;
        }
    }
}
