using System;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // One concrete item the player owns: a single item or a stack of the same one. Per-copy state (ammo, durability) goes
    // in subclasses, which raise Changed when their badge may differ.
    public class ItemInstance
    {
        public ItemInstance(ItemDefinition definition) => Definition = definition;

        // Count or per-copy state changed (slot badges and container totals refresh).
        public event Action Changed;

        public ItemDefinition Definition { get; }
        public EquippableDefinition Equippable => Definition as EquippableDefinition;
        public bool IsEquippable => Definition is EquippableDefinition;
        // Always 1 unless the definition stacks (MaxStack > 1).
        public int Count { get; private set; } = 1;
        public bool IsEmpty => Count <= 0;
        // How many more fit on this stack.
        public int SpaceLeft => Math.Max(0, Definition.MaxStack - Count);
        public float Weight => Definition.Weight * Count;

        public virtual ItemBadge Badge => Definition.Stacks ? new ItemBadge(Count) : ItemBadge.None;

        // Whether `other` is the same stackable item, so some of it can go onto this stack.
        public bool CanStackWith(ItemInstance other) =>
            other != null && other != this && other.Definition == Definition && Definition.Stacks && !other.IsEmpty;

        // Whether all of `other` fits on this stack.
        public bool CanMerge(ItemInstance other) => CanStackWith(other) && other.Count <= SpaceLeft;

        // Moves all of `other` onto this stack (check CanMerge first); `other` is left empty.
        public void Merge(ItemInstance other)
        {
            if (CanMerge(other))
                MergeFrom(other);
        }

        // Moves as much of `other` as fits (at most `max`) onto this stack. Returns how many moved.
        public int MergeFrom(ItemInstance other, int max = int.MaxValue)
        {
            if (!CanStackWith(other))
                return 0;
            int moved = Math.Min(Math.Min(other.Count, SpaceLeft), max);
            if (moved <= 0)
                return 0;
            Count += moved;
            other.Count -= moved;
            RaiseChanged();
            other.RaiseChanged();
            return moved;
        }

        // Takes `amount` off into a new stack; null when that would leave this one empty or take nothing.
        public ItemInstance Split(int amount)
        {
            if (amount <= 0 || amount >= Count)
                return null;
            ItemInstance part = Definition.CreateInstance();
            part.Count = amount;
            Count -= amount;
            RaiseChanged();
            return part;
        }

        // Takes `amount` off the stack; false (and nothing taken) if there isn't that many.
        public bool Consume(int amount = 1)
        {
            if (amount <= 0 || amount > Count)
                return false;
            Count -= amount;
            RaiseChanged();
            return true;
        }

        internal void SetCount(int count)
        {
            Count = count;
            RaiseChanged();
        }

        protected void RaiseChanged() => Changed?.Invoke();
    }
}
