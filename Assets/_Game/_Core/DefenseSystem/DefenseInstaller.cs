using System.Collections.Generic;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.Debugging;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.DefenseSystem
{
    // Scene system: the fences and gates placed in the scene. Lists them automatically when the scene is saved or Play starts,
    // and injects them; they register as enemy targets themselves (TargetRegistry, when the scene has an EnemyInstaller).
    public sealed partial class DefenseInstaller : SceneInstaller
    {
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<BarrierSegment> _sceneBarriers = new();

        public override string Problem => null;

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<BarrierRegistry>(Lifetime.Singleton);
            if (Cheats.Enabled)
                builder.RegisterEntryPoint<DefenseCommands>();
            builder.RegisterBuildCallback(resolver =>
            {
                foreach (BarrierSegment barrier in _sceneBarriers)
                    if (barrier != null)
                        resolver.InjectGameObject(barrier.gameObject);
            });
        }

#if UNITY_EDITOR
        [Button("Collect scene barriers"), PropertyOrder(-1)]
        public void CollectSceneBarriers() => SceneCollection.Collect(this, _sceneBarriers);

        // The lifecycle source generator registers this hook, hence the partial class.
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
        static void HookCollection() => SceneCollection.Hook<DefenseInstaller>(installer => installer.CollectSceneBarriers());
#endif
    }
}
