using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.TimeSystem
{
    public sealed class TimeInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder) => builder.RegisterEntryPoint<TimeService>().AsSelf();
    }
}
