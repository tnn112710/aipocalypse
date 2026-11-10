using System.Collections.Generic;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Every enabled LootContainer that was injected (crates in the scene scope's Auto Inject list, enemy bodies through their
    // EntityScope): for the HUD's loot markers.
    public sealed class LootRegistry
    {
        readonly List<LootContainer> _containers = new();

        public IReadOnlyList<LootContainer> Containers => _containers;

        public void Add(LootContainer container)
        {
            if (container != null && !_containers.Contains(container))
                _containers.Add(container);
        }

        public void Remove(LootContainer container) => _containers.Remove(container);
    }
}
