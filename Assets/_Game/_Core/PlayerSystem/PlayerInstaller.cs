using MB13.Aipocalypse.Core.Debugging;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Root-scope services of the player system. Per-player parts are registered by PlayerScope.
    public sealed class PlayerInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<PlayerRegistry>(Lifetime.Singleton);
            builder.RegisterEntryPoint<PlayerRespawnFlow>().AsSelf();
            builder.RegisterEntryPoint<PlayerSaveSection>();
            // Editor and cheat builds only.
            if (Cheats.Enabled)
            {
                builder.RegisterEntryPoint<PlayerCheats>();
                builder.RegisterEntryPoint<PlayerCommands>();
                builder.RegisterEntryPoint<PlayerCheatCommands>();
            }
        }
    }
}
