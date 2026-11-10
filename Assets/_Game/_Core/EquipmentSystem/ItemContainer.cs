using System;
using System.Collections.Generic;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // A row of slots, each holding one item or stack. Slots accept some item categories (the hotbar, gear) or anything
    // (the backpack, a loot container). An emptied stack leaves its slot by itself.
    public sealed class ItemContainer
    {
        readonly ItemCategory[] _accepts;
        readonly List<ItemInstance> _items = new();
        readonly Action _onItemChanged;

        // index, previous item, new item
        public event Action<int, ItemInstance, ItemInstance> SlotChanged;
        // Anything inside changed: a slot, a stack's count, the number of slots.
        public event Action Changed;
        // The number of slots changed (the worn backpack).
        public event Action Resized;

        // Slots that each accept only their categories, in order.
        public ItemContainer(IReadOnlyList<ItemCategory> slotAccepts)
        {
            _accepts = new ItemCategory[slotAccepts.Count];
            for (int i = 0; i < _accepts.Length; i++)
                _accepts[i] = slotAccepts[i];
            for (int i = 0; i < _accepts.Length; i++)
                _items.Add(null);
            _onItemChanged = OnItemChanged;
        }

        // `capacity` slots that take anything; Resize changes how many.
        public ItemContainer(int capacity)
        {
            for (int i = 0; i < capacity; i++)
                _items.Add(null);
            _onItemChanged = OnItemChanged;
        }

        public int Count => _items.Count;
        public ItemInstance this[int index] => _items[index];
        public bool AcceptsAnything => _accepts == null;

        public int UsedSlots
        {
            get
            {
                int used = 0;
                foreach (ItemInstance item in _items)
                    if (item != null)
                        used++;
                return used;
            }
        }

        public bool IsEmpty => UsedSlots == 0;

        public float Weight
        {
            get
            {
                float weight = 0f;
                foreach (ItemInstance item in _items)
                    if (item != null)
                        weight += item.Weight;
                return weight;
            }
        }

        // The categories slot `index` takes; every category for a container that takes anything.
        public ItemCategory AcceptsAt(int index) => _accepts != null ? _accepts[index] : (ItemCategory)~0;

        public bool CanPlace(int index, ItemInstance item) =>
            IsValid(index) && (item == null || _accepts == null || (_accepts[index] & item.Definition.Category) != 0);

        // Puts `item` in the slot, replacing what's there (no merging). False if the slot doesn't take it.
        public bool TryPlace(int index, ItemInstance item)
        {
            if (!CanPlace(index, item))
                return false;

            ItemInstance previous = _items[index];
            if (previous == item)
                return true;

            _items[index] = item;
            if (previous != null && IndexOf(previous) < 0)
                previous.Changed -= _onItemChanged;
            if (item != null && !Contains(item, except: index))
                item.Changed += _onItemChanged;
            SlotChanged?.Invoke(index, previous, item);
            Changed?.Invoke();
            return true;
        }

        public ItemInstance Remove(int index)
        {
            ItemInstance previous = _items[index];
            TryPlace(index, null);
            return previous;
        }

        public int IndexOf(ItemInstance item) => item == null ? -1 : _items.IndexOf(item);

        // First empty slot that accepts the item, or -1.
        public int FindFreeSlot(ItemInstance item)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] == null && CanPlace(i, item))
                    return i;
            }
            return -1;
        }

        // Next slot after `from` in `direction` (+1 / -1) whose item passes the filter, wrapping around. -1 if none.
        public int FindNext(int from, int direction, Func<ItemInstance, bool> filter)
        {
            int count = _items.Count;
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

        public int CountOf(ItemDefinition definition)
        {
            int count = 0;
            foreach (ItemInstance item in _items)
                if (item != null && item.Definition == definition)
                    count += item.Count;
            return count;
        }

        // Takes up to `amount` of the item, from the last stack first. Returns how many were taken.
        public int Take(ItemDefinition definition, int amount)
        {
            int taken = 0;
            for (int i = _items.Count - 1; i >= 0 && taken < amount; i--)
            {
                ItemInstance item = _items[i];
                if (item == null || item.Definition != definition)
                    continue;
                int take = Math.Min(item.Count, amount - taken);
                item.Consume(take);
                taken += take;
            }
            return taken;
        }

        // How many of `item` would fit: room on its stacks plus free slots that take it.
        public int SpaceFor(ItemInstance item)
        {
            if (item == null)
                return 0;
            long space = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                ItemInstance slot = _items[i];
                if (slot == null)
                    space += CanPlace(i, item) ? item.Definition.MaxStack : 0;
                else if (slot.Definition == item.Definition && slot.Definition.Stacks)
                    space += slot.SpaceLeft;
            }
            return (int)Math.Min(space, int.MaxValue);
        }

        // Adds as much of `item` as fits: tops up its stacks first, then puts the rest in free slots (the item itself, so a
        // gun keeps its state). `added` = units taken. True when all of it is in now; otherwise `item` keeps what's left.
        public bool TryAdd(ItemInstance item, out int added)
        {
            added = 0;
            if (item == null || item.IsEmpty || IndexOf(item) >= 0)
                return false;

            for (int i = 0; i < _items.Count && !item.IsEmpty; i++)
            {
                if (_items[i] != null && _items[i].CanStackWith(item))
                    added += _items[i].MergeFrom(item);
            }
            if (item.IsEmpty)
                return true;

            // A stack over the limit (never made by the game, but loot data can ask for it) is split across slots.
            while (item.Count > item.Definition.MaxStack)
            {
                int free = FindFreeSlot(item);
                if (free < 0)
                    return false;
                ItemInstance part = item.Split(item.Definition.MaxStack);
                TryPlace(free, part);
                added += part.Count;
            }

            int slot = FindFreeSlot(item);
            if (slot < 0)
                return false;
            added += item.Count;
            TryPlace(slot, item);
            return true;
        }

        // Changes the number of slots of a container that takes anything. Shrinking only drops empty slots at the end; false
        // (and nothing changed) when one of them holds an item.
        public bool Resize(int capacity)
        {
            if (_accepts != null || capacity < 0)
                return false;
            for (int i = capacity; i < _items.Count; i++)
                if (_items[i] != null)
                    return false;
            if (capacity == _items.Count)
                return true;

            if (capacity < _items.Count)
                _items.RemoveRange(capacity, _items.Count - capacity);
            while (_items.Count < capacity)
                _items.Add(null);
            Resized?.Invoke();
            Changed?.Invoke();
            return true;
        }

        // Whether slots from `start` on are all empty.
        public bool IsEmptyFrom(int start)
        {
            for (int i = Math.Max(0, start); i < _items.Count; i++)
                if (_items[i] != null)
                    return false;
            return true;
        }

        // Merges partial stacks, then orders by category, name and size, packed to the front. Containers that take anything only.
        public void Sort()
        {
            if (_accepts != null)
                return;

            var items = new List<ItemInstance>(_items.Count);
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null)
                    items.Add(Remove(i));
            }
            for (int i = 0; i < items.Count; i++)
            {
                for (int j = 0; j < i && !items[i].IsEmpty; j++)
                    items[j].MergeFrom(items[i]);
            }
            items.RemoveAll(item => item.IsEmpty);
            items.Sort(Compare);
            for (int i = 0; i < items.Count; i++)
                TryPlace(i, items[i]);
        }

        // Moves up to `amount` (all by default) from one slot to another, in the same container or across: into an empty
        // slot, onto the same stackable item (as much as fits), or a whole-stack swap when both slots take the other item.
        public static bool Move(ItemContainer from, int fromIndex, ItemContainer to, int toIndex, int amount = int.MaxValue)
        {
            if (from == null || to == null || !from.IsValid(fromIndex) || !to.IsValid(toIndex))
                return false;
            if (from == to && fromIndex == toIndex)
                return false;
            ItemInstance item = from[fromIndex];
            if (item == null || amount <= 0)
                return false;
            amount = Math.Min(amount, item.Count);

            ItemInstance target = to[toIndex];
            if (target == null)
            {
                if (!to.CanPlace(toIndex, item))
                    return false;
                if (amount < item.Count)
                    return to.TryPlace(toIndex, item.Split(amount));
                // Placed before it leaves, so it's never in no container (the hands keep holding a gun moved along the hotbar).
                to.TryPlace(toIndex, item);
                from.Remove(fromIndex);
                return true;
            }

            if (target.CanStackWith(item))
                return target.MergeFrom(item, amount) > 0;

            if (amount < item.Count || !to.CanPlace(toIndex, item) || !from.CanPlace(fromIndex, target))
                return false;
            to.TryPlace(toIndex, item);
            from.TryPlace(fromIndex, target);
            return true;
        }

        static int Compare(ItemInstance a, ItemInstance b)
        {
            int byCategory = a.Definition.Category.CompareTo(b.Definition.Category);
            if (byCategory != 0)
                return byCategory;
            int byName = string.CompareOrdinal(a.Definition.DisplayName, b.Definition.DisplayName);
            if (byName != 0)
                return byName;
            int byDefinition = string.CompareOrdinal(a.Definition.name, b.Definition.name);
            return byDefinition != 0 ? byDefinition : b.Count.CompareTo(a.Count);
        }

        bool Contains(ItemInstance item, int except)
        {
            for (int i = 0; i < _items.Count; i++)
                if (i != except && _items[i] == item)
                    return true;
            return false;
        }

        // A stack in here changed: emptied ones leave their slot.
        void OnItemChanged()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null && _items[i].IsEmpty)
                    Remove(i);
            }
            Changed?.Invoke();
        }

        bool IsValid(int index) => index >= 0 && index < _items.Count;
    }
}
