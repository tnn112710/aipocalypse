using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    public enum HitReaction
    {
        // The hit animation only; it keeps coming.
        Twitch,
        Flinch,
        Stagger,
    }

    // One enemy's stagger meter (StaggerProfile): each hit adds its power, the meter drains at Recovery per second (worked out
    // when the next hit lands, no tick), and its level picks the reaction. A stagger empties it and starts the cooldown.
    public sealed class StaggerMeter
    {
        readonly StaggerProfile _profile;
        float _value;
        float _at;
        float _canStaggerAt;

        public StaggerMeter(StaggerProfile profile) => _profile = profile;

        public float Value(float now) => Mathf.Max(0f, _value - _profile.Recovery * Mathf.Max(0f, now - _at));

        public HitReaction Add(float power, bool critical, float now)
        {
            _value = Value(now) + Mathf.Max(0f, power) * (critical ? _profile.CriticalMultiplier : 1f);
            _at = now;
            if (_profile.Staggers && _value >= _profile.StaggerAt && now >= _canStaggerAt)
            {
                _value = 0f;
                _canStaggerAt = now + _profile.StaggerTime + _profile.StaggerCooldown;
                return HitReaction.Stagger;
            }
            return _profile.Flinches && _value >= _profile.FlinchAt ? HitReaction.Flinch : HitReaction.Twitch;
        }
    }
}
