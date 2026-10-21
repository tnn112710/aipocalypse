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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.RegisterEntryPoint<WorldClockCheats>();
#endif
        }
    }
}
