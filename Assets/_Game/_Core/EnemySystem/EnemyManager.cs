using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

namespace Aipocalypse.Core.EnemySystem
{
    // Ticks every registered enemy's Manual-mode tree, time-sliced: each tree thinks every ThinkStride frames, staggered.
    public sealed class EnemyManager : ITickable
    {
        static readonly ProfilerMarker Think = new("Aipocalypse.EnemyManager.Think");
        readonly EnemyRegistry _enemies;
        readonly WanderSettings _settings;
        int _frame;

        public EnemyManager(EnemyRegistry enemies, WanderSettings settings)
        {
            _enemies = enemies;
            _settings = settings;
        }

        public void Tick()
        {
            int stride = Mathf.Max(1, _settings.ThinkStride);
            int phase = _frame++ % stride;
            using (Think.Auto())
            {
                for (int i = phase; i < _enemies.Count; i += stride)
                    _enemies[i].Tree.Tick();
            }
        }
    }
}
