using Aipocalypse.Core.Debugging;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.TimeSystem
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
