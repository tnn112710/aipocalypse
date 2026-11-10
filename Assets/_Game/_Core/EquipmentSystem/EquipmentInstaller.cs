using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Root: the loot registry and the save sections for containers and items lying in the world (they need the catalog).
    public sealed class EquipmentInstaller : IInstaller
    {
        readonly ItemCatalog _catalog;

        public EquipmentInstaller(ItemCatalog catalog) => _catalog = catalog;

        public void Install(IContainerBuilder builder)
        {
            builder.Register<LootRegistry>(Lifetime.Singleton);
            if (_catalog == null)
            {
                UnityEngine.Debug.LogError("ProjectLifetimeScope: no Item Catalog; items, crates and the inventory won't be saved.");
                return;
            }
            builder.RegisterInstance(_catalog);
            builder.RegisterEntryPoint<LootSaveSection>();
            builder.RegisterEntryPoint<WorldItemSaveSection>();
        }
    }
}
