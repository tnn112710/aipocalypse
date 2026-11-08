using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // When shots happen: fire mode, fire rate and bursts. Frame-rate independent: a long frame can hold several shots.
    // Call Update once per frame, then TryShot until it returns false.
    public sealed class GunTrigger
    {
        readonly FireSettings _fire;

        float _cooldown;
        int _burstLeft;
        bool _held;
        bool _pending;
        bool _inSequence;

        public GunTrigger(FireSettings fire) => _fire = fire;

        public void Update(float deltaTime, bool held, bool pressed)
        {
            _cooldown -= deltaTime;
            _held = held;

            // A click is remembered until it can fire, so fast clicking still respects the fire rate.
            if (pressed && _fire.Mode is FireMode.SemiAuto or FireMode.SingleBurst)
                _pending = true;
        }

        public bool TryShot()
        {
            if (_cooldown > 0f)
                return false;
            if (!WantsShot())
            {
                _inSequence = false;
                return false;
            }

            float interval = _fire.SecondsPerShot;
            float wait = interval;
            switch (_fire.Mode)
            {
                case FireMode.SemiAuto:
                    _pending = false;
                    break;

                case FireMode.AutoBurst:
                case FireMode.SingleBurst:
                    if (_burstLeft == 0)
                    {
                        _burstLeft = Mathf.Max(1, _fire.BurstCount);
                        _pending = false;
                    }
                    _burstLeft--;
                    if (_burstLeft == 0)
                        wait = Mathf.Max(interval, _fire.BurstCooldown);
                    break;
            }

            // A running sequence keeps the overdue time so the rate holds at any frame rate; a new one starts clean.
            _cooldown = _inSequence ? _cooldown + wait : wait;
            _inSequence = true;
            return true;
        }

        // Stops a burst and forgets a buffered click (blocked, out of ammo, jammed).
        public void Cancel()
        {
            _burstLeft = 0;
            _pending = false;
            _inSequence = false;
        }

        bool WantsShot() => _fire.Mode switch
        {
            FireMode.Auto => _held,
            FireMode.SemiAuto => _pending,
            FireMode.AutoBurst => _burstLeft > 0 || _held,
            FireMode.SingleBurst => _burstLeft > 0 || _pending,
            _ => false,
        };
    }
}
