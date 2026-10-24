using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

namespace Aipocalypse.Core.EnemySystem
{
    // Ticks the registered enemies' Manual-mode trees, time-sliced so each thinks about every ThinkInterval seconds whatever the
    // frame rate: each frame thinks its share of the crowd (count × deltaTime / interval), round-robin, at most MaxThinksPerFrame.
    // Scaled time, so a paused game doesn't think. A tree that removes its enemy mid-tick makes the swapped-in one wait a turn.
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
                for (int i = 0; i < thinks && _enemies.Count > 0; i++)
                {
                    if (_cursor >= _enemies.Count)
                        _cursor = 0;
                    _enemies[_cursor++].Tree.Tick();
                }
            }
        }
    }
}
