using System;

namespace MB13.Aipocalypse.Core.Stats
{
    // Two kinds of stat. Values (no suffix): the caller passes its tuning value as the base and gets the modified value.
    // Scales (…Scale suffix): multipliers with base 1, read with GetScale; a perk "+10%" returns 1.1.
    public enum StatId
    {
        // Also speeds up sprint and crouch, which are multiples of it.
        WalkSpeed,
        // On top of the walk speed × the tuned sprint multiplier.
        SprintSpeedScale,
        JumpHeight,
        MaxHealth,
        MaxStamina,
        StaminaRegen,
        SprintStaminaCost,
        JumpStaminaCost,
        // Lower = steadier: held-item sway and bob.
        HeldItemSwayScale,
        // Gun spread while airborne.
        AirSpreadScale,
        // Stamina spent per shot / swing.
        AttackStaminaCostScale,
        // How hard low stamina hits recoil, melee and speed (0 = not at all).
        FatiguePenaltyScale,
        // Metres around the player that read clearly in the dark; a belt lantern adds its own radius on top.
        VisionRadius,
        WalkStaminaCost,
    }

    // Core reads tunable numbers through this seam; perks, XP and buffs plug in a modifying implementation and raise Changed
    // when their modifiers change, so cached values (max health, max stamina) are read again.
    public interface IStatProvider
    {
        event Action Changed;

        float Get(StatId stat, float baseValue);
    }

    public static class StatProviderExtensions
    {
        public static float GetScale(this IStatProvider stats, StatId stat) => stats.Get(stat, 1f);
    }
}
