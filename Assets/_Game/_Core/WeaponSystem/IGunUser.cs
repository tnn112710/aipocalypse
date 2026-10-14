namespace Aipocalypse.Core.WeaponSystem
{
    // Whoever holds a gun: where its ammo comes from and how it aims.
    public interface IGunUser : IWeaponUser
    {
        AmmoReserve Ammo { get; }
        AimOrigin AimFrom { get; }
    }
}
