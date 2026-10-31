using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

namespace Aipocalypse.Core.EnemySystem
{
    // Ticks the registered enemies' Manual trees time-sliced on scaled time: each thinks about every ThinkInterval seconds,
    // round-robin, at most MaxThinksPerFrame; an enemy removed mid-tick makes the swapped-in one wait a turn.
    public sealed class EnemyManager : ITickable
    {
        static readonly ProfilerMarker Think = new("Aipocalypse.EnemyManager.Think");
        readonly EnemyRegistry _enemies;
        readonly EnemySystemSettings _settings;
        float _owed;
        int _cursor;

        public EnemyManager(EnemyRegistry enemies, EnemySystemSettings settings)
        {
            _enemies = enemies;
            _settings = settings;
        }

        public void Tick() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            int count = _enemies.Count;
            if (count == 0)
            {
                _owed = 0f;
                return;
            }
            // Owed thinks carry over (fractions at low counts, the rest when capped) but never more than one round.
            _owed = Mathf.Min(_owed + count * deltaTime / Mathf.Max(0.001f, _settings.ThinkInterval), count);
            int thinks = Mathf.Min((int)_owed, Mathf.Max(1, _settings.MaxThinksPerFrame));
            _owed -= thinks;
            using (Think.Auto())
            {
                // Bounded by the live count too: a tree that removes others mustn't get the survivors ticked twice.
                for (int i = 0; i < thinks && i < _enemies.Count; i++)
                {
                    if (_cursor >= _enemies.Count)
                        _cursor = 0;
                    _enemies[_cursor++].Think();
                }
            }
        }
    }
}
