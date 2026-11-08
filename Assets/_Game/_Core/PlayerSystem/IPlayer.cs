using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.WeaponSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
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
        // Hammer work: what its key would repair now, and whether it's knocking.
        PlayerRepair Repair { get; }
        bool IsSprinting { get; }
        // Where the crosshair sits this frame, off the screen centre, in units of half the screen height (x = right, y = up).
        Vector2 CrosshairPosition { get; }
        // Radius of the held item's spread cone on screen, in the same units.
        float CrosshairSpread { get; }
        Vector3 SpawnPosition { get; }
        // The camera's pivot: its forward is where the player looks (kick and sequences included).
        Transform View { get; }
        // Cheats: hits apply nothing (god); enemies don't choose it as a target (notarget).
        bool Invulnerable { get; set; }
        bool IgnoredByEnemies { get; set; }
        // Dev tools: plays the player instead of the keyboard, mouse and gamepad while set (null = InputService).
        IPlayerIntentSource IntentSource { get; set; }

        // Teleports the body to the spawn point (a running sequence keeps the camera, now relative to the new spot).
        void MoveToSpawn();
        void Revive();
    }
}
