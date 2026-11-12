using System.Collections.Generic;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // Scene system: the farm animals placed in the scene, their pen (gate, bell) and pasture. Lists them automatically when
    // the scene is saved or Play starts, and injects them. Needs the navmesh (EnemyInstaller) and WorldClock
    // (WorldTimeInstaller) on the same scope. Remove the component (or turn AnimalSettings.Enabled off) and the animals
    // just stand there.
    public sealed partial class AnimalInstaller : SceneInstaller
    {
        [SerializeField, InlineEditor] AnimalSettings _settings;
        [SerializeField, Tooltip("Home of the herd; none = the system stays off")] AnimalPen _pen;
        [SerializeField, Tooltip("Where released animals graze; none = they stay in the pen")] AnimalPasture _pasture;
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<Animal> _animals = new();
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<PenGate> _gates = new();
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<AnimalBell> _bells = new();

        public override string Problem =>
            _settings == null ? "no Animal Settings assigned" :
            !TryGetComponent(out EnemyInstaller enemies) || enemies.Problem != null ? "needs a ready EnemyInstaller on this scope (the navmesh)" :
            !TryGetComponent(out WorldTimeInstaller time) || time.Problem != null ? "needs a ready WorldTimeInstaller on this scope (WorldClock)" :
            null;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_settings);
            builder.Register<AnimalHerd>(Lifetime.Singleton);
            builder.RegisterEntryPoint<AnimalManager>().AsSelf();
            builder.RegisterEntryPoint<AnimalSaveSection>();
            if (Cheats.Enabled)
                builder.RegisterEntryPoint<AnimalCommands>();
            builder.RegisterBuildCallback(resolver =>
            {
                resolver.Resolve<AnimalHerd>().Bind(_pen, _pasture);
                foreach (PenGate gate in _gates)
                    if (gate != null)
                        resolver.InjectGameObject(gate.gameObject);
                foreach (AnimalBell bell in _bells)
                    if (bell != null)
                        resolver.InjectGameObject(bell.gameObject);
                foreach (Animal animal in _animals)
                    if (animal != null)
                        resolver.InjectGameObject(animal.gameObject);
            });
        }

#if UNITY_EDITOR
        [Button("Collect scene animals"), PropertyOrder(-1)]
        public void CollectSceneAnimals()
        {
            SceneCollection.Collect(this, _animals);
            SceneCollection.Collect(this, _gates);
            SceneCollection.Collect(this, _bells);
        }

        // The lifecycle source generator registers this hook, hence the partial class.
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
        static void HookCollection() => SceneCollection.Hook<AnimalInstaller>(installer => installer.CollectSceneAnimals());
#endif
    }
}
