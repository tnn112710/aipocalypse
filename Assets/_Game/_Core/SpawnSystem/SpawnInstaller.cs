using System.Collections.Generic;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.Debugging;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using RM = StylizedWater3.RiverModeler;

namespace MB13.Aipocalypse.Core.SpawnSystem
{
    // Scene system: spawning and pooling for anything (SpawnService, PrefabPool), spot picking and placement on the ground, in
    // the air or under water (lakes, ponds, rivers), and the open-world population of the scene's SpawnAreas (collected on
    // save / Play). The navmesh comes from EnemyInstaller (INavmeshQuery) when the scene has one.
    public sealed partial class SpawnInstaller : SceneInstaller
    {
        [SerializeField, InlineEditor] SpawnSystemSettings _settings;
        [SerializeField, Tooltip("Flat water bodies (lake, ponds) swimmers spawn in and flyers fly over; not sloped river segments")]
        List<Renderer> _waterBodies = new();
        [SerializeField, Tooltip("Rivers and streams (River Modeler) swimmers spawn in; sampled into a course when the scene starts")]
        List<RM.RiverModeler> _rivers = new();
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<SpawnArea> _areas = new();

        public override string Problem => _settings == null ? "no Spawn System Settings assigned" : null;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_settings);
            builder.RegisterInstance<IWaterLevel>(new WaterBodies(_waterBodies, _rivers.Count > 0 ? RiverCourse.From(_rivers) : null));
            builder.Register<SpawnVisibility>(Lifetime.Singleton);
            builder.Register<SpawnPlacer>(Lifetime.Singleton);
            builder.Register<SpawnPointPicker>(Lifetime.Singleton);
            builder.RegisterEntryPoint<PrefabPool>().AsSelf();
            builder.RegisterEntryPoint<SpawnService>().AsSelf();
            builder.RegisterEntryPoint<PopulationDirector>().WithParameter<IReadOnlyList<SpawnArea>>(_areas).AsSelf();
            // Editor and cheat builds only.
            if (Cheats.Enabled)
                builder.RegisterEntryPoint<SpawnCommands>();
        }

#if UNITY_EDITOR
        [Button("Collect spawn areas"), PropertyOrder(-1)]
        public void CollectSpawnAreas() => SceneCollection.Collect(this, _areas);

        // The lifecycle source generator registers this hook, hence the partial class.
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
        static void HookCollection() => SceneCollection.Hook<SpawnInstaller>(installer => installer.CollectSpawnAreas());
#endif
    }
}
