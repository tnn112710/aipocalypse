using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.AudioSystem
{
    public sealed class AudioInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder) => builder.Register<AudioService>(Lifetime.Singleton);
    }
}
