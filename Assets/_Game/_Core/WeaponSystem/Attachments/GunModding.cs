using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.LocalizationSystem;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // Fitting and removing gun parts with the inventory: where a part comes from and goes to, and the rounds a smaller
    // magazine can't hold going back to the backpack as ammo. Changes nothing when something has no room.
    public static class GunModding
    {
        public static readonly LocalizedString NoFit = Loc.UI("inventory.part_no_fit");
        public static readonly LocalizedString NoRoom = Loc.UI("inventory.backpack_full");

        // Fits the part in `from[index]` to `gun`. The part it replaces takes its place (or goes to the backpack).
        public static bool TryAttach(Inventory inventory, ItemContainer from, int index, GunInstance gun, out string refusal)
        {
            refusal = null;
            ItemInstance part = from?[index];
            if (part == null || gun == null)
                return false;
            if (!gun.Fits(part))
            {
                refusal = NoFit.Text();
                return false;
            }

            GunSlot slot = ((AttachmentDefinition)part.Definition).Slot;
            ItemInstance previous = gun.AttachmentIn(slot);
            bool previousSwaps = previous == null || from.CanPlace(index, previous);
            if ((!previousSwaps && inventory.Backpack.FindFreeSlot(previous) < 0) ||
                !HasRoomForRounds(inventory, gun, gun.StatsWith(slot, part)))
            {
                refusal = NoRoom.Text();
                return false;
            }

            from.Remove(index);
            gun.TryAttach(part, out previous);
            if (previous != null && !(previousSwaps && from.TryPlace(index, previous)))
                inventory.Backpack.TryAdd(previous, out _);
            ReturnExcessRounds(inventory, gun);
            return true;
        }

        // Takes the part in `slot` off `gun` into `to[index]`; a full or unfitting target (or `to` null) sends it to the
        // backpack instead. Dropped on another part for the same slot that fits, the two swap.
        public static bool TryDetach(Inventory inventory, GunInstance gun, GunSlot slot, ItemContainer to, int index, out string refusal)
        {
            refusal = null;
            ItemInstance part = gun?.AttachmentIn(slot);
            if (part == null)
                return false;
            ItemInstance there = to?[index];
            if (there != null && gun.Fits(there) && ((AttachmentDefinition)there.Definition).Slot == slot)
                return TryAttach(inventory, to, index, gun, out refusal);

            bool intoTarget = to != null && there == null && to.CanPlace(index, part);
            if ((!intoTarget && inventory.Backpack.FindFreeSlot(part) < 0) ||
                !HasRoomForRounds(inventory, gun, gun.StatsWith(slot, null)))
            {
                refusal = NoRoom.Text();
                return false;
            }

            gun.Detach(slot);
            if (!(intoTarget && to.TryPlace(index, part)))
                inventory.Backpack.TryAdd(part, out _);
            ReturnExcessRounds(inventory, gun);
            return true;
        }

        static bool HasRoomForRounds(Inventory inventory, GunInstance gun, GunStats after)
        {
            int excess = gun.Rounds - after.MagazineSize;
            AmmoType ammo = gun.Gun.Magazine.AmmoType;
            return excess <= 0 || ammo == null || inventory.SpaceFor(ammo.CreateInstance(excess)) >= excess;
        }

        static void ReturnExcessRounds(Inventory inventory, GunInstance gun)
        {
            int excess = gun.TakeExcessRounds();
            AmmoType ammo = gun.Gun.Magazine.AmmoType;
            if (excess > 0 && ammo != null)
                inventory.AddNew(ammo, excess);
        }
    }
}
