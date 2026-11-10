namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // A consumable with several uses (first aid kit): one item whose slot shows the uses left, worth less as they run out.
    public sealed class ConsumableInstance : ItemInstance
    {
        public ConsumableInstance(ConsumableDefinition definition) : base(definition) => UsesLeft = definition.Uses;

        public ConsumableDefinition Consumable => (ConsumableDefinition)Definition;
        public int UsesLeft { get; private set; }

        public override ItemBadge Badge => new(UsesLeft);
        public override long Value => (long)Definition.Value * UsesLeft / Consumable.Uses;

        // One use spent; true when that was the last (the caller removes the item).
        public bool UseOnce()
        {
            if (UsesLeft > 0)
                UsesLeft--;
            RaiseChanged();
            return UsesLeft <= 0;
        }
    }
}
