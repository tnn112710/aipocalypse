using System;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    [Serializable]
    public sealed class FatigueTier
    {
        [Range(0f, 1f), Tooltip("Applies while stamina is under this share of max")] public float Below = 0.5f;
        [Min(1f), Tooltip("Gun recoil multiplier")] public float Recoil = 1f;
        [Range(0f, 1f), Tooltip("Melee damage and knockback multiplier")] public float MeleePower = 1f;
        [Range(0f, 1f), Tooltip("Walk and sprint speed multiplier")] public float MoveSpeed = 1f;
    }

    [Serializable]
    public sealed class FatigueSettings
    {
        [Tooltip("The lowest matching tier applies; stamina at or above every threshold has no penalty")]
        public FatigueTier[] Tiers =
        {
            new() { Below = 0.5f, Recoil = 1.25f, MeleePower = 0.85f, MoveSpeed = 0.95f },
            new() { Below = 0.25f, Recoil = 1.6f, MeleePower = 0.65f, MoveSpeed = 0.85f },
            new() { Below = 0.1f, Recoil = 2.5f, MeleePower = 0.4f, MoveSpeed = 0.7f },
        };
        [Range(0f, 1f), Tooltip("Extra walk and sprint speed multiplier while winded (just ran dry, before regen starts)")]
        public float WindedMoveSpeed = 0.75f;
        [Min(0f), Tooltip("Seconds to ease most of the way into a new tier's values, so crossing a threshold never snaps")]
        public float EaseTime = 0.35f;
    }

    // Low stamina penalties: picks the tier for the current stamina (slower still while winded) and eases its multipliers in.
    // Firing is never blocked.
    public sealed class Fatigue
    {
        readonly FatigueSettings _settings;

        public float RecoilMultiplier { get; private set; } = 1f;
        public float MeleePower { get; private set; } = 1f;
        public float MoveSpeed { get; private set; } = 1f;

        public Fatigue(FatigueSettings settings) => _settings = settings;

        // `penaltyScale` (StatId.FatiguePenalty): 1 = the tier values, 0 = no penalty.
        public void Tick(float staminaFraction, float penaltyScale, float deltaTime, bool winded = false)
        {
            FatigueTier tier = TierFor(_settings.Tiers, staminaFraction);
            float scale = Mathf.Max(0f, penaltyScale);
            float recoil = tier != null ? 1f + (tier.Recoil - 1f) * scale : 1f;
            float melee = tier != null ? Mathf.Max(0f, 1f - (1f - tier.MeleePower) * scale) : 1f;
            float tierSpeed = tier != null ? tier.MoveSpeed : 1f;
            if (winded)
                tierSpeed *= _settings.WindedMoveSpeed;
            float speed = Mathf.Max(0f, 1f - (1f - tierSpeed) * scale);

            float t = Damping.ByTime(_settings.EaseTime, deltaTime);
            RecoilMultiplier = Mathf.Lerp(RecoilMultiplier, recoil, t);
            MeleePower = Mathf.Lerp(MeleePower, melee, t);
            MoveSpeed = Mathf.Lerp(MoveSpeed, speed, t);
        }

        public void Reset() => RecoilMultiplier = MeleePower = MoveSpeed = 1f;

        // The tier with the lowest threshold still above the stamina, or null when none applies.
        public static FatigueTier TierFor(FatigueTier[] tiers, float staminaFraction)
        {
            FatigueTier match = null;
            if (tiers == null)
                return null;
            foreach (FatigueTier tier in tiers)
            {
                if (tier != null && staminaFraction < tier.Below && (match == null || tier.Below < match.Below))
                    match = tier;
            }
            return match;
        }
    }
}
