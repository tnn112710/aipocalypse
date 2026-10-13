using System;
using UnityEngine;

namespace Aipocalypse.Core.Combat
{
    [Serializable]
    public sealed class Health
    {
        [SerializeField] float _max;
        [SerializeField] float _current;

        public float Max => _max;
        public float Current => _current;
        public float Normalized => _max > 0f ? _current / _max : 0f;
        public bool IsDead => _current <= 0f;

        public Health(float max)
        {
            _max = Mathf.Max(1f, max);
            _current = _max;
        }

        // Returns the damage actually applied (0 when already dead).
        public float Damage(float amount)
        {
            if (IsDead || amount <= 0f)
                return 0f;

            float applied = Mathf.Min(amount, _current);
            _current -= applied;
            return applied;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f)
                return;

            _current = Mathf.Min(_max, _current + amount);
        }

        public void Restore() => _current = _max;
    }
}
