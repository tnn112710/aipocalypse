using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using MB13.Aipocalypse.Core.Stats;
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
        // The whistle: what the aim would call now ("[Q] Call Bolt").
        PlayerWhistle Whistle { get; }
        // Searching a crate or body; Opened fires when its loot is shown.
        PlayerLooting Looting { get; }
        // Consumables at work: buffs and healing over time.
        PlayerEffects Effects { get; }
        // Hunger, thirst and sanity; null when the player's tuning has no needs settings.
        PlayerNeeds Needs { get; }
        // What the carried weight does now (limits, speed and stamina scales, state for the bag gauge).
        CarryLoad Load { get; }
        // How dark it is around the player now: 0 by day to 1 at night (always 0 without a time-of-day director).
        float Darkness { get; }
        // Dev tools: kg added to what the inventory weighs (cheat.carry).
        float ExtraLoad { get; set; }
        bool IsSprinting { get; }
        // Where the crosshair sits this frame, off the screen centre, in units of half the screen height (x = right, y = up).
        Vector2 CrosshairPosition { get; }
        // Radius of the held item's wobble envelope plus pellet spread on screen, in the same units.
        float CrosshairSpread { get; }
        Vector3 SpawnPosition { get; }
        // The camera's pivot: its forward is where the player looks (kick and sequences included).
        Transform View { get; }
        // Cheats: hits apply nothing (god); enemies don't choose it as a target (notarget).
        bool Invulnerable { get; set; }
        bool IgnoredByEnemies { get; set; }
        // Dev tools: plays the player instead of the keyboard, mouse and gamepad while set (null = InputService).
        IPlayerIntentSource IntentSource { get; set; }
        // Dev tools: shows the speed and its parts on screen (player.speed; editor and cheat builds).
        bool ShowMovementReadout { get; set; }

        // A scripted first-person moment is playing (dying, sleeping): the player can't act.
        bool InSequence { get; }
        // The playing sequence has reached its last beat.
        bool SequenceSettled { get; }
        // Hands the camera to a pose sequence; with an anchor its beats are placed in the anchor's space (a bed), else around
        // the player. StopSequence gives the camera back.
        void PlaySequence(PoseSequence sequence, Transform anchor = null);
        void StopSequence();
        // Puts the body there, facing `yaw`, looking level (no momentum, no fall damage).
        void Teleport(Vector3 position, float yaw);

        // Teleports the body to the spawn point (a running sequence keeps the camera, now relative to the new spot).
        void MoveToSpawn();
        void Revive();
        // Any meter by id (null for a need when the needs are off): cheats, HUD and scenarios use this one door.
        IMeter Meter(PlayerMeterId id);
        // Throws an item that's already out of the inventory to the ground in front.
        void ThrowAway(ItemInstance item);
    }
}
