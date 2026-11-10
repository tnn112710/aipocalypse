using MB13.Aipocalypse.Core.Debugging;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.SaveSystem
{
    // Root: save slots, the game session, scene loading, and the console's save commands (editor and cheat builds).
    public sealed class SaveInstaller : IInstaller
    {
        readonly SaveSettings _settings;

        public SaveInstaller(SaveSettings settings) => _settings = settings;

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_settings);
            builder.Register<SaveService>(Lifetime.Singleton);
            builder.Register<SaveRegistry>(Lifetime.Singleton);
            builder.Register<SceneLoader>(Lifetime.Singleton);
            builder.RegisterEntryPoint<GameSession>().AsSelf();
            if (Cheats.Enabled)
                builder.RegisterEntryPoint<SaveCommands>();
        }
    }
}
