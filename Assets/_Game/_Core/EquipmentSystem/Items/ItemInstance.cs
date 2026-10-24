using System;
using UnityEngine;

namespace Aipocalypse.Core.EquipmentSystem
{
    // One concrete item the player owns: a single item or a stack of the same one. Per-copy state (ammo, durability) goes
    // in subclasses, which raise Changed when their badge may differ.
    public class ItemInstance
    {
        public ItemInstance(ItemDefinition definition) => Definition = definition;

        // Count or per-copy state changed (the hotbar badge refreshes).
        public event Action Changed;

        public ItemDefinition Definition { get; }
        public EquippableDefinition Equippable => Definition as EquippableDefinition;
        public bool IsEquippable => Definition is EquippableDefinition;
        // Always 1 unless the definition stacks (MaxStack > 1).
        public int Count { get; private set; } = 1;
        public bool IsEmpty => Count <= 0;

        public virtual ItemBadge Badge => Definition.MaxStack > 1 ? new ItemBadge(Count) : ItemBadge.None;

        // Whether all of `other` fits on this stack.
        public bool CanMerge(ItemInstance other) =>
            other != null && other != this && other.Definition == Definition && Count + other.Count <= Definition.MaxStack;

        // Moves all of `other` onto this stack (check CanMerge first); `other` is left empty.
        public void Merge(ItemInstance other)
        {
            if (!CanMerge(other))
                return;
            Count += other.Count;
            other.Count = 0;
            RaiseChanged();
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

        protected void RaiseChanged() => Changed?.Invoke();
    }
}
