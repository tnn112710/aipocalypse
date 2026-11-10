using System;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Everything the player carries: the hotbar (active slots: guns, melee, quick slots), the backpack and the gear slots
    // (head, body, face, backpack, belt). The worn backpack adds backpack slots and carry weight.
    public sealed class Inventory : IDisposable
    {
        public const string BackpackNotEmpty = "Empty the backpack's extra slots first";

        readonly int _baseSlots;
        readonly float _baseMaxWeight;

        // Anything carried changed (slots, counts, the backpack's size).
        public event Action Changed;

        public Inventory(ItemContainer hotbar, ItemContainer gear, int baseSlots, float baseMaxWeight)
        {
            Hotbar = hotbar;
            Gear = gear;
            _baseSlots = Math.Max(0, baseSlots);
            _baseMaxWeight = baseMaxWeight;
            Backpack = new ItemContainer(_baseSlots);
            Hotbar.Changed += RaiseChanged;
            Backpack.Changed += RaiseChanged;
            Gear.Changed += OnGearChanged;
            FitBackpack();
        }

        public ItemContainer Hotbar { get; }
        public ItemContainer Backpack { get; }
        public ItemContainer Gear { get; }

        public BackpackDefinition WornBackpack => FindGear(ItemCategory.Backpack)?.Definition as BackpackDefinition;
        public float Weight => Hotbar.Weight + Backpack.Weight + Gear.Weight;
        public float MaxWeight => _baseMaxWeight + (WornBackpack != null ? WornBackpack.ExtraWeight : 0f);

        public void Dispose()
        {
            Hotbar.Changed -= RaiseChanged;
            Backpack.Changed -= RaiseChanged;
            Gear.Changed -= OnGearChanged;
        }

        // The worn item in the first gear slot that takes `category` (the lantern on the belt), or null.
        public ItemInstance FindGear(ItemCategory category)
        {
            for (int i = 0; i < Gear.Count; i++)
                if ((Gear.AcceptsAt(i) & category) != 0 && Gear[i] != null && (Gear[i].Definition.Category & category) != 0)
                    return Gear[i];
            return null;
        }

        public bool Contains(ItemInstance item) =>
            item != null && (Hotbar.IndexOf(item) >= 0 || Backpack.IndexOf(item) >= 0 || Gear.IndexOf(item) >= 0);

        // Carried units of an item, hotbar and backpack.
        public int CountOf(ItemDefinition definition) => Hotbar.CountOf(definition) + Backpack.CountOf(definition);

        // Takes up to `amount`, backpack first. Returns how many were taken.
        public int Take(ItemDefinition definition, int amount)
        {
            int taken = Backpack.Take(definition, amount);
            return taken < amount ? taken + Hotbar.Take(definition, amount - taken) : taken;
        }

        // How many of `item` could be carried now.
        public int SpaceFor(ItemInstance item)
        {
            long space = (long)Hotbar.SpaceFor(item) + Backpack.SpaceFor(item);
            if (FreeGearSlot(item) >= 0)
                space += item.Count;
            return (int)Math.Min(space, int.MaxValue);
        }

        // Adds as much of `item` as fits, where a player would put it: onto its stacks (hotbar, then backpack); worn gear
        // into a free gear slot; a gun or melee weapon into a free active slot; everything else into the backpack, then any
        // free hotbar slot that takes it. True when all of it went in; otherwise `item` keeps what's left.
        public bool TryAdd(ItemInstance item, out int added)
        {
            added = 0;
            if (item == null || item.IsEmpty || Contains(item))
                return false;

            added += TopUpStacks(Hotbar, item) + TopUpStacks(Backpack, item);
            if (item.IsEmpty)
                return true;

            int gear = FreeGearSlot(item);
            if (gear >= 0)
            {
                added += item.Count;
                Gear.TryPlace(gear, item);
                return true;
            }

            if ((item.Definition.Category & (ItemCategory.Gun | ItemCategory.Melee)) != 0 && TryPlaceFree(Hotbar, item, ref added))
                return true;

            bool all = Backpack.TryAdd(item, out int inBackpack);
            added += inBackpack;
            return all || TryPlaceFree(Hotbar, item, ref added);
        }

        // Adds `count` new units of an item (cheats, ammo crates, loot). Returns how many fit.
        public int AddNew(ItemDefinition definition, int count)
        {
            int added = 0;
            while (count > 0)
            {
                ItemInstance item = definition.CreateInstance(Math.Min(count, definition.MaxStack));
                int size = item.Count;
                TryAdd(item, out int moved);
                added += moved;
                count -= size;
                if (moved < size)
                    break;
            }
            return added;
        }

        // ItemContainer.Move with the inventory's own rules: the worn backpack can't be taken off (or swapped for a
        // smaller one) while slots it adds hold items, and it can't go inside itself. `refusal` says why a move failed.
        public bool TryMove(ItemContainer from, int fromIndex, ItemContainer to, int toIndex, out string refusal, int amount = int.MaxValue)
        {
            refusal = null;
            if (from == null || to == null || (from == to && fromIndex == toIndex))
                return false;
            if ((from == Gear) != (to == Gear) && !WornBackpackCanLeave(from, fromIndex, to, toIndex))
            {
                refusal = BackpackNotEmpty;
                return false;
            }
            return ItemContainer.Move(from, fromIndex, to, toIndex, amount);
        }

        // Takes `amount` (all by default) out of a slot, to throw it away. The worn backpack can't come off while slots it
        // adds hold items.
        public bool TryRemove(ItemContainer from, int index, out ItemInstance removed, out string refusal, int amount = int.MaxValue)
        {
            removed = null;
            refusal = null;
            ItemInstance item = from[index];
            if (item == null || amount <= 0)
                return false;
            if (IsWornBackpackWithItems(from, item))
            {
                refusal = BackpackNotEmpty;
                return false;
            }
            removed = amount < item.Count ? item.Split(amount) : from.Remove(index);
            return removed != null;
        }

        // Takes the item in a slot of another container (the loot panel) into the inventory where TryAdd puts it. What
        // doesn't fit stays in its slot. Returns the units taken.
        public int TakeFrom(ItemContainer from, int index)
        {
            ItemInstance item = from[index];
            if (item == null)
                return 0;
            from.Remove(index);
            if (!TryAdd(item, out int added) && !item.IsEmpty)
                from.TryPlace(index, item);
            return added;
        }

        // Sends the item in a slot to another container the quick way (onto its stacks, then a free slot): the backpack to
        // the loot panel, an active slot to the backpack. What doesn't fit stays. Returns the units sent.
        public int SendTo(ItemContainer from, int index, ItemContainer to, out string refusal)
        {
            refusal = null;
            ItemInstance item = from[index];
            if (item == null || from == to)
                return 0;
            if (IsWornBackpackWithItems(from, item))
            {
                refusal = BackpackNotEmpty;
                return 0;
            }
            from.Remove(index);
            if (!to.TryAdd(item, out int sent) && !item.IsEmpty)
                from.TryPlace(index, item);
            return sent;
        }

        bool IsWornBackpackWithItems(ItemContainer from, ItemInstance item) =>
            from == Gear && item.Definition is BackpackDefinition && !Backpack.IsEmptyFrom(_baseSlots);

        // A move between the gear and another container that takes off (or swaps) the worn backpack: every backpack slot
        // that goes away must be empty, and the slot at the other end of the move must stay.
        bool WornBackpackCanLeave(ItemContainer from, int fromIndex, ItemContainer to, int toIndex)
        {
            bool fromGear = from == Gear;
            int gearIndex = fromGear ? fromIndex : toIndex;
            ItemInstance leaving = Gear[gearIndex];
            if (leaving == null || leaving.Definition is not BackpackDefinition)
                return true;

            ItemContainer other = fromGear ? to : from;
            int otherIndex = fromGear ? toIndex : fromIndex;
            ItemInstance coming = other[otherIndex];
            // Anything but a backpack can't go in that slot: the move fails on its own.
            if (coming != null && coming.Definition is not BackpackDefinition)
                return true;

            int capacity = _baseSlots + (coming?.Definition is BackpackDefinition next ? next.ExtraSlots : 0);
            if (other == Backpack && otherIndex >= capacity)
                return false;
            return Backpack.IsEmptyFrom(capacity);
        }

        int FreeGearSlot(ItemInstance item)
        {
            if (item == null || item.Definition.Stacks)
                return -1;
            return Gear.FindFreeSlot(item);
        }

        static int TopUpStacks(ItemContainer container, ItemInstance item)
        {
            int added = 0;
            for (int i = 0; i < container.Count && !item.IsEmpty; i++)
                if (container[i] != null && container[i].CanStackWith(item))
                    added += container[i].MergeFrom(item);
            return added;
        }

        static bool TryPlaceFree(ItemContainer container, ItemInstance item, ref int added)
        {
            if (item.Count > item.Definition.MaxStack)
                return false;
            int slot = container.FindFreeSlot(item);
            if (slot < 0)
                return false;
            added += item.Count;
            container.TryPlace(slot, item);
            return true;
        }

        void OnGearChanged()
        {
            FitBackpack();
            RaiseChanged();
        }

        // Backpack slots follow the worn backpack. Shrinking never drops an item: TryMove refuses a change that would.
        void FitBackpack()
        {
            BackpackDefinition worn = WornBackpack;
            Backpack.Resize(_baseSlots + (worn != null ? worn.ExtraSlots : 0));
        }

        void RaiseChanged() => Changed?.Invoke();
    }
}
