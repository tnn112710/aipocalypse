using System;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using MB13.Aipocalypse.Input;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.UI
{
    // The inventory screen's item rules: moving items between the carried containers, the searched crate or body and the
    // inspected gun's part slots. Each action returns what the screen shows (a sound, a refusal) instead of showing it.
    public sealed class InventoryActions
    {
        public static readonly LocalizedString NoRoom = Loc.UI("inventory.no_room");
        public static readonly LocalizedString NowhereToWear = Loc.UI("inventory.nowhere");
        public static readonly LocalizedString BackpackFull = Loc.UI("inventory.backpack_full");
        public static readonly LocalizedString PickGun = Loc.UI("inventory.part_pick_gun");
        public static readonly LocalizedString DropLocked = Loc.UI("inventory.drop_locked");
        public static readonly LocalizedString SlotGun = Loc.UI("inventory.slot_gun");
        static readonly LocalizedString SlotMelee = Loc.UI("inventory.slot_melee");
        static readonly LocalizedString SlotItems = Loc.UI("inventory.slot_items");

        public enum Cue
        {
            None,
            Place,
            Attach,
            Detach,
        }

        // A refusal always comes with the refusal sound, played after `Sound`.
        public readonly struct Outcome
        {
            public readonly Cue Sound;
            public readonly string Refusal;

            public Outcome(Cue sound, string refusal = null)
            {
                Sound = sound;
                Refusal = refusal;
            }
        }

        static readonly Outcome Placed = new(Cue.Place);

        readonly Equipment _equipment;
        readonly Inventory _inventory;
        readonly Func<int, string> _keyLabel;

        // `keyLabel`: a hotbar slot's key name, for "this slot takes guns (1)".
        public InventoryActions(Equipment equipment, Func<int, string> keyLabel)
        {
            _equipment = equipment;
            _inventory = equipment.Inventory;
            _keyLabel = keyLabel;
        }

        static Outcome Refused(string refusal) => new(Cue.None, refusal);

        // Double-click on an item: loot <-> backpack, backpack -> a free active or gear slot, the rest -> backpack. A gun
        // part goes onto the inspected `gun` (unless a crate is open and no gun is inspected).
        public Outcome SendAcross(ItemContainer container, int index, GunInstance gun, LootContainer loot)
        {
            ItemInstance item = container[index];
            string refusal = null;
            if (item.Definition is AttachmentDefinition && (gun != null || loot == null))
                return gun == null ? Refused(PickGun.Text()) : Attach(container, index, gun);
            if (loot != null && container == loot.Items)
            {
                if (_inventory.TakeFrom(container, index) == 0)
                    refusal = NoRoom.Text();
            }
            else if (loot != null && container == _inventory.Backpack)
            {
                if (_inventory.SendTo(container, index, loot.Items, out refusal) == 0 && refusal == null)
                    refusal = NoRoom.Text();
            }
            else if (container == _inventory.Backpack)
            {
                if (!Equip(container, index, out refusal))
                    refusal = NowhereToWear.Text();
            }
            else if (_inventory.SendTo(container, index, _inventory.Backpack, out refusal) == 0 && refusal == null)
            {
                refusal = BackpackFull.Text();
            }
            return refusal == null ? Placed : Refused(refusal);
        }

        // From the backpack into a free slot that takes it (active or gear), else swapped with the first one that does. True
        // with a `refusal` when a swap was refused (e.g. the worn backpack isn't empty): the caller shows it, once.
        bool Equip(ItemContainer from, int index, out string refusal)
        {
            refusal = null;
            ItemInstance item = from[index];
            ItemContainer[] targets = { _inventory.Hotbar, _inventory.Gear };
            foreach (ItemContainer target in targets)
            {
                int free = target.FindFreeSlot(item);
                if (free >= 0)
                    return _inventory.TryMove(from, index, target, free, out _);
            }
            foreach (ItemContainer target in targets)
            {
                for (int i = 0; i < target.Count; i++)
                {
                    if (!target.CanPlace(i, item))
                        continue;
                    if (_inventory.TryMove(from, index, target, i, out refusal))
                        return true;
                    if (refusal == null)
                        continue;
                    return true;
                }
            }
            return false;
        }

        // A drag between two container slots; `amount` moves part of a stack.
        public Outcome Move(ItemContainer from, int fromIndex, ItemContainer to, int toIndex, int amount = int.MaxValue) =>
            _inventory.TryMove(from, fromIndex, to, toIndex, out string refusal, amount)
                ? Placed
                : Refused(refusal ?? WrongSlot(from, fromIndex, to, toIndex));

        // Moves the whole stack to the hotbar slot `key` selects (swapping with what's there), as a drag onto it would.
        public Outcome ToHotbar(ItemContainer from, int fromIndex, HotbarKey key)
        {
            int index = _equipment.SlotOf(key);
            if (index < 0 || (from == _inventory.Hotbar && fromIndex == index))
                return default;
            return Move(from, fromIndex, _inventory.Hotbar, index);
        }

        // A move the containers refused without a reason: a hotbar slot that doesn't take that kind of item, either the one
        // dropped on or, for a swap, the one it came from.
        string WrongSlot(ItemContainer from, int fromIndex, ItemContainer to, int toIndex)
        {
            ItemContainer hotbar = _inventory.Hotbar;
            ItemInstance moved = from[fromIndex];
            ItemInstance swapped = to[toIndex];
            if (to == hotbar && moved != null && !hotbar.CanPlace(toIndex, moved))
                return SlotTakes(toIndex);
            if (from == hotbar && swapped != null && !hotbar.CanPlace(fromIndex, swapped))
                return SlotTakes(fromIndex);
            return null;
        }

        string SlotTakes(int hotbarSlot)
        {
            ItemCategory accepts = _inventory.Hotbar.AcceptsAt(hotbarSlot);
            LocalizedString text = accepts == ItemCategory.Gun ? SlotGun : accepts == ItemCategory.Melee ? SlotMelee : SlotItems;
            return text.Format(_keyLabel(hotbarSlot));
        }

        // Fits the part in `from[index]` to `gun`.
        public Outcome Attach(ItemContainer from, int index, GunInstance gun) =>
            GunModding.TryAttach(_inventory, from, index, gun, out string refusal) ? new Outcome(Cue.Attach) : Refused(refusal);

        // Takes the part in `slot` off `gun`, into `to[index]` (null: the backpack).
        public Outcome Detach(GunInstance gun, GunSlot slot, ItemContainer to, int index) =>
            GunModding.TryDetach(_inventory, gun, slot, to, index, out string refusal) ? new Outcome(Cue.Detach) : Refused(refusal);

        // Takes a carried item out to be thrown away (`removed`); a locked one stays.
        public Outcome Drop(ItemContainer container, int index, out ItemInstance removed)
        {
            removed = null;
            if (container[index].Locked)
                return Refused(DropLocked.Text());
            return _inventory.TryRemove(container, index, out removed, out string refusal) ? default : Refused(refusal);
        }

        public static Outcome ToggleLock(ItemInstance item)
        {
            item.Locked = !item.Locked;
            return Placed;
        }

        // As much of a crate's stack as fits.
        public Outcome Pickup(ItemContainer container, int index)
        {
            int count = container[index].Count;
            int taken = _inventory.TakeFrom(container, index);
            string refusal = taken < count ? taken == 0 ? NoRoom.Text() : BackpackFull.Text() : null;
            return new Outcome(taken > 0 ? Cue.Place : Cue.None, refusal);
        }

        // Every revealed stack of the crate, as much of each as fits.
        public Outcome TakeAll(LootContainer loot)
        {
            bool full = false;
            bool took = false;
            for (int i = 0; i < loot.Items.Count; i++)
                if (loot.Items[i] != null && !loot.IsHidden(i))
                {
                    int count = loot.Items[i].Count;
                    int taken = _inventory.TakeFrom(loot.Items, i);
                    full |= taken < count;
                    took |= taken > 0;
                }
            return new Outcome(took ? Cue.Place : Cue.None, full ? BackpackFull.Text() : null);
        }

        // Uses a consumable straight from the screen, with the same refusals as in hand ("Health full").
        public static Outcome Use(ItemInstance item, IConsumer consumer)
        {
            if (item.Definition is not ConsumableDefinition consumable || consumer == null)
                return default;
            string refusal = consumer.CannotUse(consumable);
            if (refusal != null)
                return Refused(refusal);
            consumer.Use(consumable);
            HeldConsumable.SpendUse(item);
            return default;
        }
    }
}
