using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.WeaponSystem
{
    // Root-scope services: shared by every gun in every scene, ticked centrally.
    public sealed class WeaponInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<EffectPool>().AsSelf();
            builder.RegisterEntryPoint<BulletSystem>().AsSelf();
            builder.Register<GunJamFactory>(Lifetime.Singleton);
        }
    }
}
