using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.MapSystem
{
    // Root: the map marker registry, so markers in any scene (and the HUD) share one list.
    public sealed class MapInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder) => builder.Register<MapMarkerRegistry>(Lifetime.Singleton);
    }
}
