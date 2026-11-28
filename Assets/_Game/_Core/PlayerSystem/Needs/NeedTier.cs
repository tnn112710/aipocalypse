using System;
using MB13.Aipocalypse.Core.Stats;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    public enum NeedSeverity
    {
        Good,
        Normal,
        Low,
        VeryLow,
        // Only at exactly 0.
        Empty,
    }

    // One band of a need: from a share of max up to the next band, the stat changes and health per game hour it applies.
    [Serializable]
    public sealed class NeedTier
    {
        public NeedSeverity Severity = NeedSeverity.Normal;
        [Range(0f, 1f), Tooltip("Lowest share of max this tier covers (an Empty tier covers 0 only)")] public float From;
        [Tooltip("Applied while this tier is active, through the player's stats"), ListDrawerSettings(CustomAddFunction = nameof(NewChange))]
        public StatChange[] Changes = Array.Empty<StatChange>();
        [Tooltip("Health per game hour while active (negative drains, down to the needs' health floor)")] public float HealthPerHour;

        // A row added in the Inspector multiplies by 1 (a struct's default 0 would zero the stat).
        StatChange NewChange() => new(default(StatId));
    }
}
