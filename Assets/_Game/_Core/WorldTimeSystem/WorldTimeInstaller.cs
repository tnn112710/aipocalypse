using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.WorldTimeSystem
{
    // Scene scope: the world clock belongs to a session, so it is created with the scene and disposed with it.
    public sealed class WorldTimeInstaller : IInstaller
    {
        readonly ClockSettings _settings;

        public WorldTimeInstaller(ClockSettings settings) => _settings = settings;

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_settings);
            builder.RegisterEntryPoint<WorldClock>().AsSelf();
            // Editor and development builds only.
            if (UnityEngine.Debug.isDebugBuild)
                builder.RegisterEntryPoint<WorldClockCheats>();
        }
    }
}
