using System;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    [Serializable]
    public sealed class BreathingSettings
    {
        [Range(0f, 1f), Tooltip("Heavy breathing starts under this share of max stamina, and always while exhausted")]
        public float Below = 0.25f;
        [Min(0f), Tooltip("Seconds for the breathing to build up or calm down as stamina changes (sound and aim sway)")]
        public float EaseTime = 0.6f;
    }

    // Heavy breathing while out of breath: Tiredness builds as stamina drops (1 when empty or exhausted) and eases back
    // as it recovers. The player loops the breath sound while IsActive, and the aim sway breathes harder with it.
    public sealed class PlayerBreathing
    {
        readonly BreathingSettings _settings;

        public bool IsActive { get; private set; }
        // 0 calm .. 1 gasping; eased.
        public float Tiredness { get; private set; }

        public PlayerBreathing(BreathingSettings settings) => _settings = settings;

        public void Tick(float staminaFraction, bool exhausted, float deltaTime)
        {
            bool heavy = exhausted || staminaFraction < _settings.Below;
            float target = heavy ? TirednessFor(staminaFraction, exhausted, _settings.Below) : 0f;
            float t = Damping.ByTime(_settings.EaseTime, deltaTime);
            Tiredness = Mathf.Lerp(Tiredness, target, t);
            IsActive = heavy;
        }

        public void Reset()
        {
            Tiredness = 0f;
            IsActive = false;
        }

        // 0 at the threshold, 1 at empty or while exhausted.
        public static float TirednessFor(float staminaFraction, bool exhausted, float below) =>
            exhausted || below <= 0f ? 1f : Mathf.Clamp01(1f - staminaFraction / below);
    }
}
