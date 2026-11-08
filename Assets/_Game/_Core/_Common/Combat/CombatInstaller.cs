using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.Combat
{
    public sealed class CombatInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder) => builder.Register<DamageService>(Lifetime.Singleton);
    }
}
