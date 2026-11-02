using Aipocalypse.Core.Debugging;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.GraphicsSystem
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
