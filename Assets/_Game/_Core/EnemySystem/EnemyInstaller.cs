using System.Collections.Generic;
using Aipocalypse.Core.Bootstrap;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.EnemySystem
{
    // Scene system: enemy services, and injection of the enemies and spawners placed in the scene. Those are listed
    // automatically when the scene is saved or Play starts, so nobody keeps the list by hand.
    public sealed class EnemyInstaller : SceneInstaller
    {
        [SerializeField, Tooltip("AstarPath of the scene's Navigation prefab")] AstarPath _navigation;
        [SerializeField, InlineEditor] EnemySystemSettings _settings;
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<EnemyBrain> _sceneEnemies = new();
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<EnemySpawner> _sceneSpawners = new();

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
                foreach (EnemySpawner spawner in _sceneSpawners)
                    if (spawner != null)
                        resolver.Inject(spawner);
            });
        }

#if UNITY_EDITOR
        [Button("Collect scene enemies"), PropertyOrder(-1)]
        public void CollectSceneEnemies()
        {
            bool changed = Collect(_sceneEnemies) | Collect(_sceneSpawners);
            if (changed)
                UnityEditor.EditorUtility.SetDirty(this);
        }

        // Compared as a set: re-collecting the same objects must not dirty the scene on every Play.
        bool Collect<T>(List<T> list) where T : Component
        {
            var found = new List<T>();
            foreach (T item in FindObjectsByType<T>(FindObjectsInactive.Include))
                if (item.gameObject.scene == gameObject.scene)
                    found.Add(item);
            if (found.Count == list.Count && new HashSet<T>(found).SetEquals(list))
                return false;
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            list.Clear();
            list.AddRange(found);
            return true;
        }

        [UnityEditor.InitializeOnLoadMethod]
        static void HookCollection()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += (scene, _) => CollectIn(scene);
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state != UnityEditor.PlayModeStateChange.ExitingEditMode)
                    return;
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                    CollectIn(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i));
            };
        }

        static void CollectIn(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (EnemyInstaller installer in FindObjectsByType<EnemyInstaller>(FindObjectsInactive.Include))
                if (installer.gameObject.scene == scene)
                    installer.CollectSceneEnemies();
        }
#endif
    }
}
