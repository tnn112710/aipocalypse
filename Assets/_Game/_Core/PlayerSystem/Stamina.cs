using System;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    [Serializable]
    public sealed class Stamina
    {
        [SerializeField] float _max;
        [SerializeField] float _current;
        [SerializeField] bool _exhausted;

        readonly float _regenDelay;
        readonly float _recoverFraction;
        float _regenDelayLeft;

        public float Max => _max;
        public float Current => _current;
        public float Normalized => _current / _max;
        public bool IsExhausted => _exhausted;
        public bool CanExert => !_exhausted && _current > 0f;

        public Stamina(float max, float regenDelay, float recoverFraction)
        {
            _max = Mathf.Max(1f, max);
            _current = _max;
            _regenDelay = regenDelay;
            _recoverFraction = Mathf.Clamp01(recoverFraction);
        }

        // `delaysRegen` false: regen keeps running (attacks), so only a cost above the regen rate drains the meter.
        public void Spend(float amount, bool delaysRegen = true)
        {
            if (amount <= 0f)
                return;

            _current = Mathf.Max(0f, _current - amount);
            if (delaysRegen)
                _regenDelayLeft = _regenDelay;
            if (_current <= 0f)
                _exhausted = true;
        }

        public void Tick(float deltaTime, float regenPerSecond)
        {
            if (_regenDelayLeft > 0f)
            {
                _regenDelayLeft -= deltaTime;
                return;
            }

            _current = Mathf.Min(_max, _current + regenPerSecond * deltaTime);
            if (_exhausted && _current >= _max * _recoverFraction)
                _exhausted = false;
        }

        public void Restore()
        {
            _current = _max;
            _exhausted = false;
            _regenDelayLeft = 0f;
        }
    }
}
