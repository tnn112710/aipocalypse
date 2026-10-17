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
        // Multipliers (base 1, lower = steadier): held-item sway and bob, and gun spread from moving / jumping.
        HeldItemSway,
        MotionSpread,
    }

    // Core reads tunable numbers through this seam; perks, XP and buffs plug in a modifying implementation.
    public interface IStatProvider
    {
        float Get(StatId stat, float baseValue);
    }
}
