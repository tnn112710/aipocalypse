using System;

namespace Aipocalypse.Core.EquipmentSystem
{
    // Hotbar slots accept items by category. Flags so one slot can take several kinds.
    [Flags]
    public enum ItemCategory
    {
        None = 0,
        Gun = 1 << 0,
        Melee = 1 << 1,
        // Goes on the belt, not the hotbar (Equipment.Belt).
        Lantern = 1 << 2,
    }
}
