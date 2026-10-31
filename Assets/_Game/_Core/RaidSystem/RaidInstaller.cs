using System.Collections.Generic;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.WorldTimeSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.RaidSystem
{
    // Scene system: raid nights, their spawning from the scene's SpawnZones (collected on save / Play) and DeathTimeRule.
    // Needs WorldClock (WorldTimeInstaller) and the enemy services (EnemyInstaller) on the same scope.
    public sealed class RaidInstaller : SceneInstaller
    {
        [SerializeField, InlineEditor] RaidSettings _settings;
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<SpawnZone> _zones = new();

        public override string Problem =>
            _settings == null ? "no Raid Settings assigned" :
            _settings.Plan == null ? $"{_settings.name} has no raid plan" :
            _zones.Count == 0 ? "no SpawnZone in the scene" :
            !TryGetComponent(out WorldTimeInstaller time) || time.Problem != null ? "needs a ready WorldTimeInstaller on this scope (WorldClock)" :
            !TryGetComponent(out EnemyInstaller enemies) || enemies.Problem != null ? "needs a ready EnemyInstaller on this scope (spawning)" :
            null;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_settings);
            builder.Register<EveryNightSchedule>(Lifetime.Singleton).As<IRaidSchedule>();
            builder.Register<SpawnPointPicker>(Lifetime.Singleton);
            // IRaidSpawner + ITickable (the dawn withdrawal).
            builder.RegisterEntryPoint<RaidSpawner>().WithParameter<IReadOnlyList<SpawnZone>>(_zones);
            builder.RegisterEntryPoint<RaidDirector>().AsSelf();
            builder.RegisterEntryPoint<DeathTimeRule>();
            // Editor and development builds only.
            if (Debug.isDebugBuild)
                builder.RegisterEntryPoint<RaidCommands>();
        }

#if UNITY_EDITOR
        [Button("Collect spawn zones"), PropertyOrder(-1)]
        public void CollectSpawnZones() => SceneCollection.Collect(this, _zones);

        [UnityEditor.InitializeOnLoadMethod]
        static void HookCollection() => SceneCollection.Hook<RaidInstaller>(installer => installer.CollectSpawnZones());
#endif
    }
}
