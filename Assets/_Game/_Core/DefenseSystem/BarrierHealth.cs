using System;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DefenseSystem
{
    // A barrier's health. At 0 it breaks; repairs add health while broken too, and it stands again once it has StandAt of its
    // maximum back (the owner checks nothing is in the way, then calls Stand).
    [Serializable]
    public sealed class BarrierHealth
    {
        [SerializeField] float _max;
        [SerializeField] float _current;
        [SerializeField] bool _broken;

        public BarrierHealth(float max)
        {
            _max = Mathf.Max(1f, max);
            _current = _max;
        }

        public float Max => _max;
        public float Current => _current;
        public float Normalized => _current / _max;
        public bool IsBroken => _broken;
        public bool IsWhole => _current >= _max;

        // Returns the damage taken; a broken barrier takes none.
        public float Damage(float amount)
        {
            if (_broken || amount <= 0f)
                return 0f;
            float applied = Mathf.Min(amount, _current);
            _current -= applied;
            if (_current <= 0f)
            {
                _current = 0f;
                _broken = true;
            }
            return applied;
        }

        // Returns the health added.
        public float Repair(float amount)
        {
            if (amount <= 0f)
                return 0f;
            float applied = Mathf.Min(amount, _max - _current);
            _current += applied;
            return applied;
        }

        public bool CanStand(float standAt) => _broken && Normalized >= standAt;

        public void Stand() => _broken = false;

        // Loading a save: exactly this health and state.
        public void Set(float current, bool broken)
        {
            _current = Mathf.Clamp(current, 0f, _max);
            _broken = broken || _current <= 0f;
        }

        public void Restore()
        {
            _current = _max;
            _broken = false;
        }

        // An upgrade keeps the share of health it had; a broken barrier stays broken.
        public void SetMax(float max)
        {
            float share = Normalized;
            _max = Mathf.Max(1f, max);
            _current = share * _max;
        }
    }
}
