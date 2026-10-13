using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.PlayerSystem
{
    // Root-scope services of the player system. Per-player parts are registered by PlayerScope.
    public sealed class PlayerInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder) => builder.Register<PlayerRegistry>(Lifetime.Singleton);
    }
}
