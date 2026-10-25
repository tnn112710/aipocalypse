using System.Collections.Generic;
using Aipocalypse.Core.Bootstrap;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.EnemySystem
{
    // Scene system: enemy services, and injection of the enemies placed in the scene. They are listed automatically when the
    // scene is saved or Play starts, so nobody keeps the list by hand.
    public sealed class EnemyInstaller : SceneInstaller
    {
        [SerializeField, Tooltip("AstarPath of the scene's Navigation prefab")] AstarPath _navigation;
        [SerializeField, InlineEditor] EnemySystemSettings _settings;
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<EnemyBrain> _sceneEnemies = new();

        public override string Problem =>
            _navigation == null ? "no AstarPath (Navigation prefab) assigned" :
            _settings == null ? "no Enemy System Settings assigned" : null;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_navigation);
            builder.RegisterInstance(_settings);
            builder.Register<EnemyRegistry>(Lifetime.Singleton);
            builder.RegisterEntryPoint<EnemyManager>();
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
