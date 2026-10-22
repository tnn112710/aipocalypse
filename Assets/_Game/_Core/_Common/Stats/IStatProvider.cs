namespace Aipocalypse.Core.Stats
{
    public enum StatId
    {
        WalkSpeed,
        SprintSpeed,
        JumpHeight,
        MaxHealth,
        MaxStamina,
        StaminaRegen,
        SprintStaminaCost,
        JumpStaminaCost,
        // Multipliers (base 1, lower = steadier): held-item sway and bob, and gun spread while airborne.
        HeldItemSway,
        AirSpread,
        // Multipliers (base 1): stamina spent per shot / swing, and how hard low stamina hits recoil, melee and speed (0 = not at all).
        AttackStaminaCost,
        FatiguePenalty,
        // Metres around the player that read clearly in the dark; a belt lantern adds its own radius on top.
        VisionRadius,
    }

    // Core reads tunable numbers through this seam; perks, XP and buffs plug in a modifying implementation.
    public interface IStatProvider
    {
        float Get(StatId stat, float baseValue);
    }
}
