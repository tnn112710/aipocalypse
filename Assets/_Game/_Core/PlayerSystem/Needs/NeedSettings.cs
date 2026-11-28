using System;
using MB13.Aipocalypse.Core.Stats;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    // One need's data: its max, drain and tiers, and the stats upgrades use to change them.
    [Serializable]
    public sealed class NeedSettings
    {
        [Tooltip("Off: the meter stays where it is and its tiers apply nothing")] public bool Enabled = true;
        [Min(1f)] public float BaseMax = 100f;
        public StatId MaxStat = StatId.MaxHunger;
        [Tooltip("Scale on the drain (upgrades slow it)")] public StatId DrainStat = StatId.HungerDrain;
        [Tooltip("Scale on the low tiers' penalties (0 = none)")] public StatId PenaltyScaleStat = StatId.HungerPenaltyScale;
        [Tooltip("Points lost per game hour, awake and at rest"), Min(0f)] public float DrainPerHour;
        [Tooltip("Extra points lost per stamina point spent (effort)"), Min(0f)] public float PerStaminaSpent;
        [Tooltip("Highest From first; an Empty tier applies at 0 only")] public NeedTier[] Tiers = Array.Empty<NeedTier>();
    }
}
