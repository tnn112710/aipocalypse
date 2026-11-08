using MB13.Aipocalypse.Core.Debugging;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.TimeSystem
{
    public sealed class TimeInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<TimeService>().AsSelf();
            // Editor and cheat builds only.
            if (Cheats.Enabled)
                builder.RegisterEntryPoint<TimeCommands>();
        }
    }
}
