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
        readonly float _windedTime;
        float _regenDelayLeft;
        float _windedLeft;

        public float Max => _max;
        public float Current => _current;
        public float Normalized => _current / _max;
        public bool IsExhausted => _exhausted;
        // Just ran dry: catching breath before regen starts.
        public bool IsWinded => _windedLeft > 0f;
        public bool CanExert => !_exhausted && _current > 0f;

        // `windedTime`: seconds of no regen after running dry.
        public Stamina(float max, float regenDelay, float recoverFraction, float windedTime = 0f)
        {
            _max = Mathf.Max(1f, max);
            _current = _max;
            _regenDelay = regenDelay;
            _recoverFraction = Mathf.Clamp01(recoverFraction);
            _windedTime = Mathf.Max(0f, windedTime);
        }

        // Any spend (walking, sprinting, jumping, attacking) pauses regen and restarts the delay.
        public void Spend(float amount)
        {
            if (amount <= 0f)
                return;

            _current = Mathf.Max(0f, _current - amount);
            _regenDelayLeft = _regenDelay;
            // Only running dry winds you; draining an already exhausted meter again doesn't restart the wait.
            if (_current <= 0f && !_exhausted)
            {
                _exhausted = true;
                _windedLeft = _windedTime;
            }
        }

        // `resting` false (moving, airborne): no regen, and the delay holds full until the player stands idle.
        public void Tick(float deltaTime, float regenPerSecond, bool resting = true)
        {
            bool waiting = _regenDelayLeft > 0f || _windedLeft > 0f;
            _regenDelayLeft -= deltaTime;
            _windedLeft -= deltaTime;
            if (!resting)
            {
                _regenDelayLeft = Mathf.Max(_regenDelayLeft, _regenDelay);
                return;
            }
            if (waiting)
                return;

            _current = Mathf.Min(_max, _current + regenPerSecond * deltaTime);
            if (_exhausted && _current >= _max * _recoverFraction)
                _exhausted = false;
        }

        public void Restore()
        {
            _current = _max;
            _exhausted = false;
            _regenDelayLeft = 0f;
            _windedLeft = 0f;
        }
    }
}
