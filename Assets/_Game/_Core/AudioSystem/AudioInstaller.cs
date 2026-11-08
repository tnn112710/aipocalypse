using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.AudioSystem
{
    public sealed class AudioInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder) => builder.Register<AudioService>(Lifetime.Singleton);
    }
}
