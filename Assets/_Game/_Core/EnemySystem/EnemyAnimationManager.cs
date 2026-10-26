using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

namespace Aipocalypse.Core.EnemySystem
{
    // Advances every registered enemy's EnemyAnimator once a frame on scaled time (paused = frozen, like the Animators).
    public sealed class EnemyAnimationManager : ITickable
    {
        static readonly ProfilerMarker Animate = new("Aipocalypse.EnemyAnimationManager.Animate");
        readonly EnemyRegistry _enemies;

        public EnemyAnimationManager(EnemyRegistry enemies) => _enemies = enemies;

        public void Tick() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            using (Animate.Auto())
            {
                for (int i = 0; i < _enemies.Count; i++)
                {
                    EnemyAnimator animation = _enemies[i].Animation;
                    if (animation != null)
                        animation.Advance(deltaTime);
                }
            }
        }
    }
}
