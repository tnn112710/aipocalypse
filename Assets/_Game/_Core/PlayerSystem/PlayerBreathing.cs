using System;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    [Serializable]
    public sealed class BreathingSettings
    {
        [Range(0f, 1f), Tooltip("Heavy breathing starts under this share of max stamina, and always while exhausted")]
        public float Below = 0.25f;
        [Range(0f, 1f), Tooltip("Breath volume just under the threshold; full at 0 / exhausted")]
        public float MinVolume = 0.4f;
        [Min(0f), Tooltip("Seconds for the breathing to build up or calm down as stamina changes (sound and aim sway)")]
        public float EaseTime = 0.6f;
        [Min(0f), Tooltip("Seconds the breath loop fades in when heavy breathing starts")]
        public float FadeIn = 0.4f;
        [Min(0f), Tooltip("Seconds it fades out once you've caught your breath")]
        public float FadeOut = 1.5f;
    }

    // Heavy breathing while out of breath: Tiredness builds as stamina drops (1 when empty or exhausted) and eases back
    // as it recovers. The player loops the breath sound at Volume while IsActive, and the aim sway breathes harder with it.
    public sealed class PlayerBreathing
    {
        readonly BreathingSettings _settings;

        public bool IsActive { get; private set; }
        // 0 calm .. 1 gasping; eased.
        public float Tiredness { get; private set; }
        public float Volume => Mathf.Lerp(_settings.MinVolume, 1f, Tiredness);

        public PlayerBreathing(BreathingSettings settings) => _settings = settings;

        public void Tick(float staminaFraction, bool exhausted, float deltaTime)
        {
            bool heavy = exhausted || staminaFraction < _settings.Below;
            float target = heavy ? TirednessFor(staminaFraction, exhausted, _settings.Below) : 0f;
            float t = _settings.EaseTime > 0f ? 1f - Mathf.Exp(-deltaTime / _settings.EaseTime) : 1f;
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
