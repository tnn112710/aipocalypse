using MB13.Aipocalypse.Core.Debugging;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.GraphicsSystem
{
    public sealed class GraphicsInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<GraphicsService>(Lifetime.Singleton);
            // Editor and cheat builds only.
            if (Cheats.Enabled)
                builder.RegisterEntryPoint<GraphicsCommands>();
        }
    }
}
