using Aipocalypse.Core.Bootstrap;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.WorldTimeSystem
{
    // Scene system: the world clock belongs to a session, so it is created with the scene and disposed with it.
    // Without it the HUD clock hides and the sky keeps its preview hour.
    public sealed class WorldTimeInstaller : SceneInstaller
    {
        [SerializeField, Tooltip("How fast the clock runs and where its phases fall")] ClockSettings _settings;

        public override string Problem => _settings == null ? "no Clock Settings assigned" : null;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_settings);
            builder.RegisterEntryPoint<WorldClock>().AsSelf();
            // Editor and development builds only.
            if (Debug.isDebugBuild)
            {
                builder.RegisterEntryPoint<WorldClockCheats>();
                builder.RegisterEntryPoint<WorldClockCommands>();
            }
        }
    }
}
