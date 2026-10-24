using UnityEngine;

namespace Aipocalypse.Core.EquipmentSystem
{
    // What a hotbar slot shows under the item's icon: a count (rounds in the magazine, a stack's size), optionally what the
    // owner has left in reserve for Reserve (an ammo type), and whether it needs attention (empty, jammed).
    public readonly struct ItemBadge
    {
        public static readonly ItemBadge None = default;

        public readonly bool Visible;
        public readonly int Count;
        public readonly bool Warning;
        // Looked up in the owner's reserve (AmmoReserve for an AmmoType); null shows the count alone.
        public readonly ScriptableObject Reserve;

        public ItemBadge(int count, bool warning = false, ScriptableObject reserve = null)
        {
            Visible = true;
            Count = count;
            Warning = warning;
            Reserve = reserve;
        }
    }
}
