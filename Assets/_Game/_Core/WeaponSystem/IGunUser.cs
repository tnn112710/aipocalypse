using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // Whoever holds a gun: where its ammo comes from and how it aims.
    public interface IGunUser
    {
        AmmoReserve Ammo { get; }
        // Camera aim point; Camera-origin shots leave from here along its forward.
        Transform Eye { get; }
        // Bullets never hit anything under this.
        Transform Root { get; }
        AimOrigin AimFrom { get; }
    }
}
