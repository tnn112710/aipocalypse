using Aipocalypse.Core.PlayerSystem;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

namespace Aipocalypse.Core.EnemySystem
{
    // Ticks the registered enemies' Manual trees time-sliced on scaled time: each is visited about every ThinkInterval seconds,
    // round-robin, at most MaxThinksPerFrame visits a frame; enemies past FarThinkDistance from the player think only every
    // FarThinkInterval of their visits. An enemy removed mid-tick makes the swapped-in one wait a turn.
    public sealed class EnemyManager : ITickable
    {
        static readonly ProfilerMarker Think = new("Aipocalypse.EnemyManager.Think");
        readonly EnemyRegistry _enemies;
        readonly EnemySystemSettings _settings;
        readonly PlayerRegistry _players;
        float _owed;
        float _clock;
        int _cursor;

        public EnemyManager(EnemyRegistry enemies, EnemySystemSettings settings, PlayerRegistry players)
        {
            _enemies = enemies;
            _settings = settings;
            _players = players;
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
            _clock += deltaTime;
            // Owed visits carry over (fractions at low counts, the rest when capped) but never more than one round.
            _owed = Mathf.Min(_owed + count * deltaTime / Mathf.Max(0.001f, _settings.ThinkInterval), count);
            int visits = Mathf.Min((int)_owed, Mathf.Max(1, _settings.MaxThinksPerFrame));
            _owed -= visits;
            IPlayer player = _players.Current;
            float far = player != null ? _settings.FarThinkDistance : 0f;
            Vector3 eye = player != null ? player.Transform.position : default;
            using (Think.Auto())
            {
                // Bounded by the live count too: a tree that removes others mustn't get the survivors ticked twice.
                for (int i = 0; i < visits && i < _enemies.Count; i++)
                {
                    if (_cursor >= _enemies.Count)
                        _cursor = 0;
                    EnemyBrain enemy = _enemies[_cursor++];
                    float next = enemy.NextThinkAt;
                    if (!ThinksNow((enemy.transform.position - eye).sqrMagnitude, far, _clock, ref next, _settings.FarThinkInterval))
                        continue;
                    enemy.NextThinkAt = next;
                    enemy.Think();
                }
            }
        }

        // Whether an enemy `sqrDistance` from the player thinks on this visit: always when it's within `far` metres (0 = no
        // limit), else only once `clock` reaches `next`, which then moves `interval` on.
        public static bool ThinksNow(float sqrDistance, float far, float clock, ref float next, float interval)
        {
            if (far <= 0f || sqrDistance <= far * far)
                return true;
            if (clock < next)
                return false;
            next = clock + interval;
            return true;
        }
    }
}
