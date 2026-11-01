using System.Collections.Generic;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.SpawnSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.EnemySystem
{
    // Scene system: enemy services (ticking, targeting, sounds, the navmesh SpawnService places walkers on), and injection of the
    // enemies placed in the scene. Those are listed automatically when the scene is saved or Play starts, so nobody keeps the
    // list by hand. Spawning and pooling are SpawnInstaller's.
    public sealed class EnemyInstaller : SceneInstaller
    {
        [SerializeField, Tooltip("AstarPath of the scene's Navigation prefab")] AstarPath _navigation;
        [SerializeField, InlineEditor] EnemySystemSettings _settings;
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<EnemyBrain> _sceneEnemies = new();

        public override string Problem =>
            _navigation == null ? "no AstarPath (Navigation prefab) assigned" :
            _settings == null ? "no Enemy System Settings assigned" :
            !TryGetComponent(out SpawnInstaller spawning) || spawning.Problem != null ? "needs a ready SpawnInstaller on this scope (spawning)" :
            null;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_navigation);
            builder.RegisterInstance(_settings);
            builder.Register<EnemyRegistry>(Lifetime.Singleton);
            builder.Register<EnemyNavigation>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<TargetRegistry>(Lifetime.Singleton);
            builder.Register<TargetSelector>(Lifetime.Singleton);
            builder.Register<AttackSlots>(Lifetime.Singleton);
            builder.RegisterEntryPoint<EnemyManager>();
            builder.RegisterEntryPoint<EnemyAnimationManager>();
            builder.RegisterEntryPoint<EnemyBodyManager>().AsSelf();
            builder.RegisterEntryPoint<EnemySoundManager>();
            // Editor and development builds only.
            if (Debug.isDebugBuild)
            {
                builder.RegisterEntryPoint<EnemyCommands>();
                builder.RegisterEntryPoint<EnemyCheatCommands>();
                builder.RegisterEntryPoint<Debugging.RaidAutopilot>();
            }
            builder.RegisterBuildCallback(resolver =>
            {
                foreach (EnemyBrain enemy in _sceneEnemies)
                    if (enemy != null)
                        resolver.InjectGameObject(enemy.gameObject);
            });
        }

#if UNITY_EDITOR
        [Button("Collect scene enemies"), PropertyOrder(-1)]
        public void CollectSceneEnemies() => SceneCollection.Collect(this, _sceneEnemies);

        [UnityEditor.InitializeOnLoadMethod]
        static void HookCollection() => SceneCollection.Hook<EnemyInstaller>(installer => installer.CollectSceneEnemies());
#endif
    }
}
