using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.RandomSystem
{
    public sealed class RandomInstaller : IInstaller
    {
        // Factory: VContainer would pick the seeded constructor and fail to resolve the ulong.
        public void Install(IContainerBuilder builder)
        {
            builder.Register(_ => new RandomService(), Lifetime.Singleton);
            builder.RegisterEntryPoint<RandomSaveSection>();
        }
    }
}
