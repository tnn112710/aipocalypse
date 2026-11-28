using MB13.Aipocalypse.Core.Stats;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    // Hunger, thirst and sanity: drains, tiers and the health rules. Rates are per game hour. The field defaults are the
    // design's starting values (docs/superpowers/specs/2026-11-28-survival-stats-design.md). Asset: Data/Player/PlayerNeedsSettings.
    [CreateAssetMenu(fileName = "PlayerNeedsSettings", menuName = "Aipocalypse/Player/Needs Settings")]
    public sealed class PlayerNeedsSettings : ScriptableObject
    {
        static readonly StatId[] StaminaCosts =
            { StatId.SprintStaminaCost, StatId.JumpStaminaCost, StatId.WalkStaminaCost, StatId.AttackStaminaCostScale };

        [BoxGroup("Hunger"), InlineProperty, HideLabel] public NeedSettings Hunger = new()
        {
            BaseMax = 100f,
            MaxStat = StatId.MaxHunger,
            DrainStat = StatId.HungerDrain,
            PenaltyScaleStat = StatId.HungerPenaltyScale,
            DrainPerHour = 100f / 24f,
            PerStaminaSpent = 0.015f,
            Tiers = new[]
            {
                Tier(NeedSeverity.Good, 0.75f, 0f, new StatChange(StatId.HealthRegen, add: 12f)),
                Tier(NeedSeverity.Normal, 0.6f, 0f),
                Tier(NeedSeverity.Low, 0.3f, 0f, Melee(0.85f), Sway(1.25f)),
                Tier(NeedSeverity.VeryLow, 0f, 0f, Melee(0.65f), Sway(1.6f)),
                Tier(NeedSeverity.Empty, 0f, -10f, Melee(0.5f), Sway(2f)),
            },
        };

        [BoxGroup("Thirst"), InlineProperty, HideLabel] public NeedSettings Thirst = new()
        {
            BaseMax = 100f,
            MaxStat = StatId.MaxThirst,
            DrainStat = StatId.ThirstDrain,
            PenaltyScaleStat = StatId.ThirstPenaltyScale,
            DrainPerHour = 100f / 16f,
            PerStaminaSpent = 0.025f,
            Tiers = new[]
            {
                Tier(NeedSeverity.Good, 0.75f, 0f, new StatChange(StatId.StaminaRegen, multiply: 1.15f)),
                Tier(NeedSeverity.Normal, 0.6f, 0f),
                Tier(NeedSeverity.Low, 0.3f, 0f, Parched(0.8f, 1.15f)),
                Tier(NeedSeverity.VeryLow, 0f, 0f, Parched(0.6f, 1.35f)),
                Tier(NeedSeverity.Empty, 0f, -10f, Parched(0.4f, 1.5f)),
            },
        };

        [BoxGroup("Sanity"), InlineProperty, HideLabel] public NeedSettings Sanity = new()
        {
            BaseMax = 100f,
            MaxStat = StatId.MaxSanity,
            DrainStat = StatId.SanityDrain,
            PenaltyScaleStat = StatId.SanityPenaltyScale,
            Tiers = new[]
            {
                Tier(NeedSeverity.Good, 0.6f, 0f),
                Tier(NeedSeverity.Normal, 0.3f, 0f),
                Tier(NeedSeverity.Low, 0.1f, 0f),
                Tier(NeedSeverity.VeryLow, 0f, 0f),
                Tier(NeedSeverity.Empty, 0f, 0f),
            },
        };

        [BoxGroup("Sanity"), InlineProperty, LabelText("Sanity rules"), Tooltip("Lack of sleep, darkness outside the lights, recovery in them, comfort")]
        public SanitySettings SanityRules = new();
        [BoxGroup("Sanity"), InlineProperty, LabelText("Sanity effects"), Tooltip("Where each screen look fades in")]
        public SanityEffectSettings SanityEffects = new();

        [BoxGroup("Rules"), Range(0f, 1f), Tooltip("Share of the time drain while asleep")] public float AsleepDrainScale = 0.33f;
        [BoxGroup("Rules"), Range(0f, 1f), Tooltip("An empty need's health drain stops at this share of max health")]
        public float HealthFloor = 0.25f;
        [BoxGroup("Rules"), Min(0f), Tooltip("Real seconds since the last hit before health regenerates")] public float OutOfCombatSeconds = 10f;
        [BoxGroup("Rules"), Range(0f, 1f), Tooltip("After a death, each need below this share is raised to it (StatId.DeathNeedFloor)")]
        public float BaseDeathFloor = 0.5f;
        [BoxGroup("Rules"), Min(0f), Tooltip("Real seconds to ease into a new tier's stat changes")] public float EaseTime = 0.35f;

        static NeedTier Tier(NeedSeverity severity, float from, float healthPerHour, params StatChange[] changes) =>
            new() { Severity = severity, From = from, HealthPerHour = healthPerHour, Changes = changes };

        static StatChange Melee(float multiply) => new(StatId.MeleePowerScale, multiply: multiply);
        static StatChange Sway(float multiply) => new(StatId.HeldItemSwayScale, multiply: multiply);

        static StatChange[] Parched(float regen, float costs)
        {
            var changes = new StatChange[StaminaCosts.Length + 1];
            changes[0] = new StatChange(StatId.StaminaRegen, multiply: regen);
            for (int i = 0; i < StaminaCosts.Length; i++)
                changes[i + 1] = new StatChange(StaminaCosts[i], multiply: costs);
            return changes;
        }
    }
}
