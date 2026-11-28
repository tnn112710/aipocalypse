using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.Stats;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    // The player's stat layer for needs: applies each tracked need's active tier row over the layer below, easing into a new
    // row. Changed fires when a tier changes, when its ease ends (so stats read once on Changed, like maxes, get the new row)
    // and when the layer below changes; never per frame of easing.
    public sealed class NeedConditions : IStatProvider, IDisposable
    {
        sealed class Tracked
        {
            public Need Need;
            public StatId PenaltyScaleStat;
            public NeedTier From;
            public NeedTier To;
            public float Blend = 1f;
            public Action<NeedTier> OnTierChanged;
        }

        readonly IStatProvider _below;
        readonly float _easeTime;
        readonly List<Tracked> _tracked = new();

        public event Action Changed;

        public NeedConditions(IStatProvider below, float easeTime)
        {
            _below = below;
            _easeTime = Mathf.Max(0f, easeTime);
            _below.Changed += RaiseChanged;
        }

        public void Track(Need need, StatId penaltyScaleStat)
        {
            var tracked = new Tracked { Need = need, PenaltyScaleStat = penaltyScaleStat, To = need.Tier };
            tracked.OnTierChanged = tier =>
            {
                tracked.From = tracked.To;
                tracked.To = tier;
                tracked.Blend = _easeTime > 0f ? 0f : 1f;
                RaiseChanged();
            };
            need.TierChanged += tracked.OnTierChanged;
            _tracked.Add(tracked);
        }

        public void Tick(float deltaTime)
        {
            if (_easeTime <= 0f)
                return;
            bool settled = false;
            for (int i = 0; i < _tracked.Count; i++)
            {
                Tracked tracked = _tracked[i];
                if (tracked.Blend >= 1f)
                    continue;
                tracked.Blend = Mathf.Min(1f, tracked.Blend + deltaTime / _easeTime);
                settled |= tracked.Blend >= 1f;
            }
            if (settled)
                RaiseChanged();
        }

        public float Get(StatId stat, float baseValue)
        {
            float value = _below.Get(stat, baseValue);
            for (int i = 0; i < _tracked.Count; i++)
            {
                Tracked tracked = _tracked[i];
                if (!tracked.Need.Settings.Enabled)
                    continue;
                if (tracked.Blend < 1f)
                    value = Apply(value, tracked.From, stat, 1f - tracked.Blend, tracked.PenaltyScaleStat);
                value = Apply(value, tracked.To, stat, tracked.Blend, tracked.PenaltyScaleStat);
            }
            return value;
        }

        float Apply(float value, NeedTier tier, StatId stat, float weight, StatId penaltyScaleStat)
        {
            if (tier == null || weight <= 0f || tier.Changes == null)
                return value;
            // Penalty scale reaches the low tiers only: a well-fed bonus isn't a penalty.
            float scale = tier.Severity == NeedSeverity.Good ? weight : weight * Mathf.Max(0f, _below.GetScale(penaltyScaleStat));
            if (scale <= 0f)
                return value;
            for (int i = 0; i < tier.Changes.Length; i++)
            {
                StatChange change = tier.Changes[i];
                if (change.Stat == stat)
                    value = (value + change.Add * scale) * (1f + (change.Multiply - 1f) * scale);
            }
            return value;
        }

        void RaiseChanged() => Changed?.Invoke();

        public void Dispose()
        {
            _below.Changed -= RaiseChanged;
            for (int i = 0; i < _tracked.Count; i++)
                _tracked[i].Need.TierChanged -= _tracked[i].OnTierChanged;
            _tracked.Clear();
        }
    }
}
