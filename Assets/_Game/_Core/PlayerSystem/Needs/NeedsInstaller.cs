using System.Collections.Generic;
using MB13.Aipocalypse.Core.Bootstrap;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    // Scene system: drains the player's needs on game time (NeedsTicker), with the scene's safe lights (collected on save /
    // Play) and comfort sources for sanity. The needs themselves live on the player (its tuning's PlayerNeedsSettings);
    // remove this component and they never drain. WorldClock and SleepService are optional.
    public sealed partial class NeedsInstaller : SceneInstaller
    {
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play: the lights that keep sanity (none = dark everywhere at night)")]
        List<SafeLight> _lights = new();

        public override string Problem => null;

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<ComfortSources>(Lifetime.Singleton);
            builder.RegisterEntryPoint<NeedsTicker>().WithParameter<IReadOnlyList<SafeLight>>(_lights).AsSelf();
            builder.RegisterEntryPoint<SanityPresenter>();
        }

#if UNITY_EDITOR
        [Button("Collect safe lights"), PropertyOrder(-1)]
        public void CollectLights() => SceneCollection.Collect(this, _lights);

        // The lifecycle source generator registers this hook, hence the partial class.
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
        static void HookCollection() => SceneCollection.Hook<NeedsInstaller>(installer => installer.CollectLights());
#endif
    }
}
