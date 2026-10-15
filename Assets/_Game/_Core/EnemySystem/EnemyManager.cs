using Pathfinding;
using Unity.Profiling;
using VContainer.Unity;

namespace Aipocalypse.Core.EnemySystem
{
    public sealed class EnemyManager : IStartable, ITickable
    {
        static readonly ProfilerMarker Think = new("Aipocalypse.EnemyManager.Think");
        readonly EnemyBrain[] _enemies;
        readonly AstarPath _navigation;
        readonly WanderSettings _settings;
        int _frame;

        public EnemyManager(EnemyBrain[] enemies, AstarPath navigation, WanderSettings settings)
        {
            _enemies = enemies;
            _navigation = navigation;
            _settings = settings;
        }

        public void Start()
        {
            foreach (var enemy in _enemies)
                if (enemy != null && enemy.isActiveAndEnabled) enemy.Initialize(_navigation);
        }

        public void Tick()
        {
            int stride = UnityEngine.Mathf.Max(1, _settings.ThinkStride);
            int phase = _frame++ % stride;
            using (Think.Auto())
                for (int i = phase; i < _enemies.Length; i += stride)
                {
                    var enemy = _enemies[i];
                    if (enemy != null && enemy.isActiveAndEnabled && enemy.Tree != null) enemy.Tree.Tick();
                }
        }
    }
}
