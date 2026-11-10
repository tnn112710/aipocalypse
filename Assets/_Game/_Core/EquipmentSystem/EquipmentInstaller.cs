using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Root-scope services of the equipment system.
    public sealed class EquipmentInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder) => builder.Register<LootRegistry>(Lifetime.Singleton);
    }
}
