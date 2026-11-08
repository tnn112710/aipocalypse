using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.QuickTimeSystem
{
    public sealed class QuickTimeInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder) => builder.RegisterEntryPoint<QuickTimeService>().AsSelf();
    }
}
