using System.Collections.Generic;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.SleepSystem
{
    // Scene system: sleeping in the scene's beds (collected on save / Play, injected here). Needs WorldClock
    // (WorldTimeInstaller) on the same scope; raids are optional. Remove the component (or turn SleepSettings.Enabled off)
    // and beds offer nothing.
    public sealed partial class SleepInstaller : SceneInstaller
    {
        [SerializeField, InlineEditor] SleepSettings _settings;
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<Bed> _beds = new();

        public override string Problem =>
            _settings == null ? "no Sleep Settings assigned" :
            !TryGetComponent(out WorldTimeInstaller time) || time.Problem != null ? "needs a ready WorldTimeInstaller on this scope (WorldClock)" :
            null;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_settings);
            builder.RegisterEntryPoint<SleepService>().AsSelf();
            builder.RegisterBuildCallback(resolver =>
            {
                foreach (Bed bed in _beds)
                    if (bed != null)
                        resolver.InjectGameObject(bed.gameObject);
            });
        }

#if UNITY_EDITOR
        [Button("Collect beds"), PropertyOrder(-1)]
        public void CollectBeds() => SceneCollection.Collect(this, _beds);

        // The lifecycle source generator registers this hook, hence the partial class.
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
        static void HookCollection() => SceneCollection.Hook<SleepInstaller>(installer => installer.CollectBeds());
#endif
    }
}
