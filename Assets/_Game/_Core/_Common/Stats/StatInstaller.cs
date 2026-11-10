using MB13.Aipocalypse.Core.Debugging;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.Stats
{
    // Root scope: the stat modifier layer is every entity's IStatProvider (an entity's own default applies only without it).
    public sealed class StatInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<StatModifiers>(Lifetime.Singleton).AsSelf().As<IStatProvider>();
            builder.RegisterEntryPoint<StatSaveSection>();
            if (Cheats.Enabled)
                builder.RegisterEntryPoint<StatCommands>();
        }
    }
}
