using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // Whoever holds a weapon: where it aims from, what it must never hit, and how tired it gets.
    public interface IWeaponUser
    {
        // Camera aim point: melee swings and Camera-origin shots go along its forward.
        Transform Eye { get; }
        // Bullets and swings never hit anything under this.
        Transform Root { get; }
        // Melee damage and knockback multiplier from fatigue: 1 = fresh.
        float MeleePower { get; }

        // Spends stamina for a shot or swing. Never blocks the attack.
        void Exert(float stamina);
    }
}
