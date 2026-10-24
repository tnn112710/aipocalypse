using Aipocalypse.Core.Entities;
using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.WeaponSystem;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // The active player as PlayerRegistry hands it out: read-only state for the HUD and services, plus the respawn steps.
    // Enemies and companions only need the narrower IPlayerTarget.
    public interface IPlayer : IPlayerTarget
    {
        Health Health { get; }
        Stamina Stamina { get; }
        Equipment Equipment { get; }
        AmmoReserve Ammo { get; }
        HeldItem HeldItem { get; }
        PlayerInteractor Interactor { get; }
        bool IsSprinting { get; }
        // Where the crosshair sits this frame, off the screen centre, in units of half the screen height (x = right, y = up).
        Vector2 CrosshairPosition { get; }
        // Radius of the held item's spread cone on screen, in the same units.
        float CrosshairSpread { get; }
        Vector3 SpawnPosition { get; }

        // Teleports the body to the spawn point (a running sequence keeps the camera, now relative to the new spot).
        void MoveToSpawn();
        void Revive();
    }
}
