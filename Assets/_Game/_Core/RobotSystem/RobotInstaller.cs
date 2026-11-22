using System.Collections.Generic;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Scene system: the robots placed in the scene (listed on save or Play, then injected) and the yard they stay in. Needs
    // EnemyInstaller (navmesh) and WorldTimeInstaller on the same scope. Remove it, or turn RobotSettings.Enabled off, and the
    // robots just stand there.
    public sealed partial class RobotInstaller : SceneInstaller
    {
        [SerializeField, InlineEditor] RobotSettings _settings;
        [SerializeField, Tooltip("The yard the robots never leave; none = whistled robots go straight to the player")] RobotYard _yard;
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<Robot> _robots = new();

        public override string Problem =>
            _settings == null ? "no Robot Settings assigned" :
            !TryGetComponent(out EnemyInstaller enemies) || enemies.Problem != null ? "needs a ready EnemyInstaller on this scope (the navmesh)" :
            !TryGetComponent(out WorldTimeInstaller time) || time.Problem != null ? "needs a ready WorldTimeInstaller on this scope (WorldClock)" :
            null;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_settings);
            builder.Register<RobotRoster>(Lifetime.Singleton);
            builder.RegisterEntryPoint<RobotManager>().AsSelf();
            if (Cheats.Enabled)
                builder.RegisterEntryPoint<RobotCommands>();
            builder.RegisterBuildCallback(resolver =>
            {
                resolver.Resolve<RobotRoster>().Bind(_yard);
                foreach (Robot robot in _robots)
                    if (robot != null)
                        resolver.InjectGameObject(robot.gameObject);
            });
        }

        public void Setup(RobotSettings settings, RobotYard yard)
        {
            _settings = settings;
            _yard = yard;
        }

#if UNITY_EDITOR
        [Button("Collect scene robots"), PropertyOrder(-1)]
        public void CollectSceneRobots() => SceneCollection.Collect(this, _robots);

        // The lifecycle source generator registers this hook, hence the partial class.
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
        static void HookCollection() => SceneCollection.Hook<RobotInstaller>(installer => installer.CollectSceneRobots());
#endif
    }
}
