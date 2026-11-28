using System;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Slots accept items by category (the hotbar's gun, melee and quick slots, the gear slots). Flags so one slot can take
    // several kinds.
    [Flags]
    public enum ItemCategory
    {
        None = 0,
        Gun = 1 << 0,
        Melee = 1 << 1,
        // Worn in the Belt gear slot, works whatever is in the hands.
        Lantern = 1 << 2,
        // Held tools that aren't weapons (the repair hammer).
        Tool = 1 << 3,
        // Spare rounds (AmmoType): guns reload from them.
        Ammo = 1 << 4,
        // Carried and selectable in hand (medic box); using them comes later.
        Consumable = 1 << 5,
        // Tokens.
        Currency = 1 << 6,
        // Worn in the Backpack gear slot: more backpack slots and carry weight.
        Backpack = 1 << 7,
        Head = 1 << 8,
        Body = 1 << 9,
        Face = 1 << 10,
        // Gun parts (AttachmentDefinition): carried, fitted to guns in the inventory screen.
        Attachment = 1 << 11,
        // Materials (MaterialDefinition: wood): carried in the backpack, some burn as campfire fuel.
        Material = 1 << 12,
    }
}
