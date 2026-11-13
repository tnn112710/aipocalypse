using System;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // What kind of gun a model is; generic parts list the families they fit. Flags so one part can fit several.
    [Flags]
    public enum GunFamily
    {
        None = 0,
        Pistol = 1 << 0,
        Smg = 1 << 1,
        // Assault rifles and carbines.
        Rifle = 1 << 2,
        // DMRs and battle rifles.
        Marksman = 1 << 3,
        // Bolt-action rifles.
        Sniper = 1 << 4,
        MachineGun = 1 << 5,
        Shotgun = 1 << 6,
    }
}
