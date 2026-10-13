using System;
using System.Collections.Generic;

namespace Aipocalypse.Core.EquipmentSystem
{
    // Fixed row of slots. Each slot accepts some item categories and holds at most one item.
    public sealed class Hotbar
    {
        public const int MaxSlots = 9;

        readonly ItemCategory[] _accepts;
        readonly ItemInstance[] _items;

        // index, previous item, new item
        public event Action<int, ItemInstance, ItemInstance> SlotChanged;

        public Hotbar(IReadOnlyList<ItemCategory> slotAccepts)
        {
            if (slotAccepts.Count > MaxSlots)
                throw new ArgumentException($"A hotbar has at most {MaxSlots} slots, got {slotAccepts.Count}.");

            _accepts = new ItemCategory[slotAccepts.Count];
            for (int i = 0; i < _accepts.Length; i++)
                _accepts[i] = slotAccepts[i];
            _items = new ItemInstance[_accepts.Length];
        }

        public int Count => _items.Length;
        public ItemInstance this[int index] => _items[index];
        public ItemCategory Accepts(int index) => _accepts[index];

        public bool CanPlace(int index, ItemInstance item) =>
            IsValid(index) && (item == null || (_accepts[index] & item.Definition.Category) != 0);

        public bool TryPlace(int index, ItemInstance item)
        {
            if (!CanPlace(index, item))
                return false;

            ItemInstance previous = _items[index];
            if (previous == item)
                return true;

            _items[index] = item;
            SlotChanged?.Invoke(index, previous, item);
            return true;
        }

        public ItemInstance Remove(int index)
        {
            ItemInstance previous = _items[index];
            TryPlace(index, null);
            return previous;
        }

        public int IndexOf(ItemInstance item) => item == null ? -1 : Array.IndexOf(_items, item);

        // Next slot after `from` in `direction` (+1 / -1) whose item passes the filter, wrapping around. -1 if none.
        public int FindNext(int from, int direction, Func<ItemInstance, bool> filter)
        {
            int count = _items.Length;
            if (count == 0 || direction == 0)
                return -1;

            direction = direction > 0 ? 1 : -1;
            int start = IsValid(from) ? from : (direction > 0 ? -1 : count);
            for (int step = 1; step <= count; step++)
            {
                int index = ((start + step * direction) % count + count) % count;
                ItemInstance item = _items[index];
                if (item != null && filter(item))
                    return index;
            }
            return -1;
        }

        bool IsValid(int index) => index >= 0 && index < _items.Length;
    }
}
