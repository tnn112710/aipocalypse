using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.SpawnSystem;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.AmbientSystem
{
    // Scene system: ambient life that only decorates (flocks of birds, schools of fish), moved by one central tick. They come
    // into play through the SpawnService (a SpawnArea or a request), so it needs a SpawnInstaller on the same scope.
    public sealed class AmbientInstaller : SceneInstaller
    {
        public override string Problem =>
            !TryGetComponent(out SpawnInstaller spawning) || spawning.Problem != null ? "needs a ready SpawnInstaller on this scope (spawning)" : null;

        public override void Install(IContainerBuilder builder) => builder.RegisterEntryPoint<FlockSystem>().AsSelf();
    }
}
