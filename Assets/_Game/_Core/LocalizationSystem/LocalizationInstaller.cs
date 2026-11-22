using MB13.Aipocalypse.Core.Debugging;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.LocalizationSystem
{
    // Root: the language service, and the console's lang commands (editor and cheat builds).
    public sealed class LocalizationInstaller : IInstaller
    {
        readonly LanguageSettings _settings;

        public LocalizationInstaller(LanguageSettings settings) => _settings = settings;

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_settings ?? new LanguageSettings());
            builder.RegisterEntryPoint<LanguageService>().AsSelf();
            if (Cheats.Enabled)
                builder.RegisterEntryPoint<LanguageCommands>();
        }
    }
}
