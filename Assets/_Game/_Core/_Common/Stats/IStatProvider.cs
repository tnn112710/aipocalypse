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
    }

    // Core reads tunable numbers through this seam; perks, XP and buffs plug in a modifying implementation.
    public interface IStatProvider
    {
        float Get(StatId stat, float baseValue);
    }
}
