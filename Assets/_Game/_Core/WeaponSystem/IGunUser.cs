using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // Whoever holds a gun: where its ammo comes from and how it aims.
    public interface IGunUser : IWeaponUser
    {
        AmmoReserve Ammo { get; }
        AimOrigin AimFrom { get; }
        // Scales the spread from moving and jumping: 1 = normal, lower = steadier.
        float MotionSpreadScale { get; }
        // Recoil multiplier from fatigue: 1 = fresh, higher = shakier.
        float FatigueRecoilScale { get; }
        // Where the crosshair is, as a turn from the eye's forward (aim sway). Camera and muzzle-to-crosshair shots follow it.
        Quaternion AimOffset { get; }
    }
}
