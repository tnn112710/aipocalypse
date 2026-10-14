using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // Whoever holds a weapon: where it aims from and what it must never hit.
    public interface IWeaponUser
    {
        // Camera aim point: melee swings and Camera-origin shots go along its forward.
        Transform Eye { get; }
        // Bullets and swings never hit anything under this.
        Transform Root { get; }
    }
}
