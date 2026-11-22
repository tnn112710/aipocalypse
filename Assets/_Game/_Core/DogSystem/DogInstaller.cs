using System.Collections.Generic;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.RobotSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // Scene system: the companion dog placed in the scene (listed on save or Play, then injected), its dog house, the yard
    // and the spots it visits at home; the command wheel; its save. Needs EnemyInstaller (navmesh) and WorldTimeInstaller
    // on the same scope. Remove it, or turn DogSettings.Enabled off, and the dog just stands there.
    public sealed partial class DogInstaller : SceneInstaller
    {
        [SerializeField, InlineEditor] DogSettings _settings;
        [SerializeField, Tooltip("Its home in the yard")] DogHouse _house;
        [SerializeField, Tooltip("The yard (shared with the robots); none = home is just around the house")] RobotYard _yard;
        [SerializeField, Tooltip("Spots it trots over to at home")] List<Transform> _visits = new();
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<Dog> _dogs = new();

        public override string Problem =>
            _settings == null ? "no Dog Settings assigned" :
            !TryGetComponent(out EnemyInstaller enemies) || enemies.Problem != null ? "needs a ready EnemyInstaller on this scope (the navmesh)" :
            !TryGetComponent(out WorldTimeInstaller time) || time.Problem != null ? "needs a ready WorldTimeInstaller on this scope (WorldClock)" :
            null;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_settings);
            builder.Register<DogRoster>(Lifetime.Singleton);
            builder.RegisterEntryPoint<CommandWheelPresenter>().AsSelf();
            builder.RegisterEntryPoint<DogManager>().AsSelf().As<IWhistleFallback>();
            builder.RegisterEntryPoint<DogSaveSection>();
            if (Cheats.Enabled)
                builder.RegisterEntryPoint<DogCommands>();
            builder.RegisterBuildCallback(resolver =>
            {
                resolver.Resolve<DogRoster>().Bind(_house, _yard, _visits);
                foreach (Dog dog in _dogs)
                    if (dog != null)
                        resolver.InjectGameObject(dog.gameObject);
            });
        }

        public void Setup(DogSettings settings, DogHouse house, RobotYard yard, List<Transform> visits)
        {
            _settings = settings;
            _house = house;
            _yard = yard;
            _visits = visits;
        }

#if UNITY_EDITOR
        [Button("Collect scene dogs"), PropertyOrder(-1)]
        public void CollectSceneDogs() => SceneCollection.Collect(this, _dogs);

        // The lifecycle source generator registers this hook, hence the partial class.
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
        static void HookCollection() => SceneCollection.Hook<DogInstaller>(installer => installer.CollectSceneDogs());
#endif
    }
}
