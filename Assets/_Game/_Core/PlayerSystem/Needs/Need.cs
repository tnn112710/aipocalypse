using System;
using MB13.Aipocalypse.Core.Stats;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    // One need meter (hunger, thirst, sanity): a value from 0 to max and the tier it sits in. Starts full.
    public sealed class Need : IMeter
    {
        readonly NeedSettings _settings;
        float _max;
        float _current;

        public NeedSettings Settings => _settings;
        public float Current => _current;
        public float Max => _max;
        public float Normalized => _current / _max;
        public NeedTier Tier { get; private set; }
        public NeedSeverity Severity => Tier != null ? Tier.Severity : NeedSeverity.Normal;

        public event Action<NeedTier> TierChanged;

        public Need(NeedSettings settings, float max)
        {
            _settings = settings;
            _max = Mathf.Max(1f, max);
            _current = _max;
            Tier = TierFor(_settings.Tiers, Normalized, _current <= 0f);
        }

        public void Set(float current)
        {
            _current = Mathf.Clamp(current, 0f, _max);
            UpdateTier();
        }

        public void Add(float delta) => Set(_current + delta);

        // A new maximum keeps the current share.
        public void SetMax(float max)
        {
            float share = Normalized;
            _max = Mathf.Max(1f, max);
            _current = share * _max;
            UpdateTier();
        }

        void UpdateTier()
        {
            NeedTier tier = TierFor(_settings.Tiers, Normalized, _current <= 0f);
            if (tier == Tier)
                return;
            Tier = tier;
            TierChanged?.Invoke(tier);
        }

        // The tier with the highest From at or below the share; an Empty tier wins at 0 only.
        public static NeedTier TierFor(NeedTier[] tiers, float share, bool empty)
        {
            if (tiers == null)
                return null;
            NeedTier best = null;
            for (int i = 0; i < tiers.Length; i++)
            {
                NeedTier tier = tiers[i];
                if (tier == null)
                    continue;
                if (tier.Severity == NeedSeverity.Empty)
                {
                    if (empty)
                        return tier;
                    continue;
                }
                if (share >= tier.From && (best == null || tier.From > best.From))
                    best = tier;
            }
            return best;
        }
    }
}
