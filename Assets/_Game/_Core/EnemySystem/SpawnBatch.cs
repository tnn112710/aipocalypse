using System.Collections.Generic;

namespace Aipocalypse.Core.EnemySystem
{
    // A group EnemySpawner brings in over a few frames (EnemySystemSettings.SpawnsPerFrame): how many it holds and how many
    // are still to come. Arrivals are added to the caller's list; Cancel drops the ones not spawned yet.
    public sealed class SpawnBatch
    {
        readonly List<EnemyBrain> _into;

        internal SpawnBatch(List<EnemyBrain> into) => _into = into;

        // Members queued (each on walkable ground).
        public int Count { get; internal set; }
        // Members not spawned yet.
        public int Pending { get; private set; }
        public bool Cancelled { get; private set; }

        public void Cancel()
        {
            Cancelled = true;
            Pending = 0;
        }

        internal void Queued()
        {
            Count++;
            Pending++;
        }

        // A pooled enemy may come back while still listed from an earlier life, so it's listed once.
        internal void Arrived(EnemyBrain enemy)
        {
            Pending--;
            if (enemy != null && _into != null && !_into.Contains(enemy))
                _into.Add(enemy);
        }
    }
}
