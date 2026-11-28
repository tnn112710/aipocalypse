using System.Collections.Generic;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.Debugging;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.CampfireSystem
{
    // Scene system: the campfires and wood piles placed in the scene (collected on save / Play), injected so they register,
    // burn on game time (CampfireTicker), save and take console commands. Remove it and the fires are props.
    public sealed partial class CampfireInstaller : SceneInstaller
    {
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<Campfire> _fires = new();
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<WoodPile> _piles = new();

        public override string Problem => null;

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<CampfireRegistry>(Lifetime.Singleton);
            builder.RegisterEntryPoint<CampfireTicker>().AsSelf();
            builder.RegisterEntryPoint<CampfireSaveSection>();
            if (Cheats.Enabled)
                builder.RegisterEntryPoint<CampfireCommands>();
            builder.RegisterBuildCallback(resolver =>
            {
                foreach (Campfire fire in _fires)
                    if (fire != null)
                        resolver.InjectGameObject(fire.gameObject);
                foreach (WoodPile pile in _piles)
                    if (pile != null)
                        resolver.InjectGameObject(pile.gameObject);
            });
        }

#if UNITY_EDITOR
        [Button("Collect campfires and wood piles"), PropertyOrder(-1)]
        public void CollectScene()
        {
            SceneCollection.Collect(this, _fires);
            SceneCollection.Collect(this, _piles);
        }

        // The lifecycle source generator registers this hook, hence the partial class.
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
        static void HookCollection() => SceneCollection.Hook<CampfireInstaller>(installer => installer.CollectScene());
#endif
    }
}
