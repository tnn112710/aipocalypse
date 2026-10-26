using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.GraphicsSystem
{
    public sealed class GraphicsInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<GraphicsService>(Lifetime.Singleton);
            // Editor and development builds only.
            if (Debug.isDebugBuild)
                builder.RegisterEntryPoint<GraphicsCommands>();
        }
    }
}
