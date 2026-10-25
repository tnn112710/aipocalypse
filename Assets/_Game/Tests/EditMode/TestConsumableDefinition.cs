using Aipocalypse.Core.EquipmentSystem;

namespace Aipocalypse.Tests
{
    // A quick-use, stackable item for EquipmentTests; the game has no consumable yet.
    public sealed class TestConsumableDefinition : ItemDefinition
    {
        public override SelectBehaviour Selecting => SelectBehaviour.QuickUse;
        public override ItemCategory Category => ItemCategory.Melee;
    }
}
