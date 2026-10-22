using System;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    public enum SwingPhase
    {
        None,
        WindUp,   // the click to the hit
        Recovery, // the hit to the next swing
    }

    public struct MeleeInput
    {
        public bool AttackHeld;
        public bool AttackPressed;

        // Raised and usable; false while switching cancels the swing.
        public bool IsReady;
        // False while sprinting: no new swings, the current one finishes.
        public bool CanAttack;
    }

    // A melee weapon's rules: swing timing, holding to repeat, one buffered click. Plain C#; HeldMelee turns its
    // events into the hit check, sounds and motion.
    public sealed class MeleeAttack
    {
        readonly SwingSettings _swing;
        bool _queued;
        float _idleTime = float.MaxValue;

        // The swing's index since the last pause: even = first side, odd = the other.
        public event Action<int> SwingStarted;
        // The moment the swing lands: check what it hits.
        public event Action Struck;

        public MeleeAttack(SwingSettings swing) => _swing = swing;

        public SwingPhase Phase { get; private set; }
        public int SwingIndex { get; private set; }
        // Seconds since the current swing started.
        public float SwingTime { get; private set; }

        public void Tick(in MeleeInput input, float deltaTime)
        {
            if (!input.IsReady)
            {
                Interrupt();
                return;
            }

            if (input.AttackPressed)
                _queued = true;
            if (!input.CanAttack)
                _queued = false;

            float overflow = Advance(deltaTime);
            if (Phase == SwingPhase.None && input.CanAttack && (_queued || input.AttackHeld))
                Start(overflow);
        }

        // Holstered or switching: drops the swing before it lands. Time put away isn't counted, so the next swing starts on the first side.
        public void Interrupt()
        {
            _idleTime = float.MaxValue;
            Phase = SwingPhase.None;
            SwingTime = 0f;
            _queued = false;
        }

        // Seconds left over this frame after the swing ended (carried into the next one so held swings keep their rate).
        float Advance(float deltaTime)
        {
            if (Phase == SwingPhase.None)
            {
                _idleTime += deltaTime;
                return 0f;
            }

            SwingTime += deltaTime;
            if (Phase == SwingPhase.WindUp && SwingTime >= _swing.WindUp)
            {
                Phase = SwingPhase.Recovery;
                Struck?.Invoke();
            }

            if (Phase != SwingPhase.Recovery || SwingTime < _swing.Duration)
                return 0f;

            float overflow = SwingTime - _swing.Duration;
            Phase = SwingPhase.None;
            SwingTime = 0f;
            _idleTime = overflow;
            return overflow;
        }

        void Start(float elapsed)
        {
            SwingIndex = _idleTime > _swing.AlternateResetDelay ? 0 : SwingIndex + 1;
            _queued = false;
            Phase = SwingPhase.WindUp;
            SwingTime = 0f;
            SwingStarted?.Invoke(SwingIndex);
            if (elapsed > 0f)
                Advance(Mathf.Min(elapsed, _swing.WindUp));
        }
    }
}
