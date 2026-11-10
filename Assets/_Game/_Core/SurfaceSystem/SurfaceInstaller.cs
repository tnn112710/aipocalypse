using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.SurfaceSystem
{
    // Root scope. Without settings (or with them off) the service answers the default surface and plays nothing.
    public sealed class SurfaceInstaller : IInstaller
    {
        readonly SurfaceSettings _settings;

        public SurfaceInstaller(SurfaceSettings settings) => _settings = settings;

        public void Install(IContainerBuilder builder) =>
            builder.Register(resolver => new SurfaceService(_settings, resolver.Resolve<AudioSystem.AudioService>(),
                resolver.Resolve<Effects.EffectPool>()), Lifetime.Singleton);
    }
}
