namespace Aipocalypse.Core.WeaponSystem
{
    // Whoever holds a gun: where its ammo comes from and how it aims.
    public interface IGunUser : IWeaponUser
    {
        AmmoReserve Ammo { get; }
        AimOrigin AimFrom { get; }
        // Scales the spread from moving and jumping: 1 = normal, lower = steadier.
        float MotionSpreadScale { get; }
    }
}
