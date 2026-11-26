using System.Collections.Generic;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.Debugging;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.ForecastSystem
{
    // Scene system: the night forecast and the scene's forecast boards (collected on save / Play, injected here). Remove the
    // component (or turn ForecastSettings.Enabled off) and the boards stay blank.
    public sealed partial class ForecastInstaller : SceneInstaller
    {
        [SerializeField, InlineEditor] ForecastSettings _settings;
        [SerializeField, ReadOnly, Tooltip("Collected on save / Play")] List<ForecastBoard> _boards = new();

        public override string Problem => _settings == null ? "no Forecast Settings assigned" : null;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_settings);
            builder.Register<ForecastService>(Lifetime.Singleton);
            // Editor and cheat builds only.
            if (Cheats.Enabled)
                builder.RegisterEntryPoint<ForecastCommands>();
            builder.RegisterBuildCallback(resolver =>
            {
                foreach (ForecastBoard board in _boards)
                    if (board != null)
                        resolver.InjectGameObject(board.gameObject);
            });
        }

#if UNITY_EDITOR
        [Button("Collect boards"), PropertyOrder(-1)]
        public void CollectBoards() => SceneCollection.Collect(this, _boards);

        // The lifecycle source generator registers this hook, hence the partial class.
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
        static void HookCollection() => SceneCollection.Hook<ForecastInstaller>(installer => installer.CollectBoards());
#endif
    }
}
