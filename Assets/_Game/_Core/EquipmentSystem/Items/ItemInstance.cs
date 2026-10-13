namespace Aipocalypse.Core.EquipmentSystem
{
    // One concrete item the player owns. Per-copy state (ammo, durability, stack count) goes in subclasses.
    public class ItemInstance
    {
        public ItemInstance(ItemDefinition definition) => Definition = definition;

        public ItemDefinition Definition { get; }
        public EquippableDefinition Equippable => Definition as EquippableDefinition;
        public bool IsEquippable => Definition is EquippableDefinition;
    }
}
