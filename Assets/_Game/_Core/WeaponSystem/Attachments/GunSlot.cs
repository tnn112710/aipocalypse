namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // Where a part goes on a gun, in the order the inspector shows them (Escape from Duckov's).
    public enum GunSlot
    {
        Optic,
        Muzzle,
        // Underbarrel: grips, bipods.
        Grip,
        Stock,
        // Lasers and lights.
        Tactical,
        Magazine,
    }

    public static class GunSlots
    {
        public const int Count = 6;
    }
}
