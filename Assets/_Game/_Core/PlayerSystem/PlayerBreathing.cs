using System;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    [Serializable]
    public sealed class BreathingSettings
    {
        [Range(0f, 1f), Tooltip("Heavy breathing starts under this share of max stamina, and always while exhausted")]
        public float Below = 0.25f;
        [Min(0.1f), Tooltip("Seconds between breaths just under the threshold; keep it at least the clip's length")]
        public float SlowInterval = 1.6f;
        [Min(0.1f), Tooltip("Seconds between breaths at 0 stamina or exhausted")]
        public float FastInterval = 0.9f;
        [Range(0f, 1f), Tooltip("Breath volume just under the threshold; full at 0 / exhausted")]
        public float MinVolume = 0.4f;
    }

    // Heavy breathing while out of breath: faster and louder the lower stamina gets. The player plays the sound.
    public sealed class PlayerBreathing
    {
        readonly BreathingSettings _settings;
        float _untilNext;

        public bool IsActive { get; private set; }

        // Volume 0..1.
        public event Action<float> Breathed;

        public PlayerBreathing(BreathingSettings settings) => _settings = settings;

        public void Tick(float staminaFraction, bool exhausted, float deltaTime)
        {
            IsActive = exhausted || staminaFraction < _settings.Below;
            if (!IsActive)
            {
                _untilNext = 0f;
                return;
            }

            _untilNext -= deltaTime;
            if (_untilNext > 0f)
                return;

            float tiredness = Tiredness(staminaFraction, exhausted, _settings.Below);
            _untilNext = Mathf.Lerp(_settings.SlowInterval, _settings.FastInterval, tiredness);
            Breathed?.Invoke(Mathf.Lerp(_settings.MinVolume, 1f, tiredness));
        }

        // 0 at the threshold, 1 at empty or while exhausted.
        public static float Tiredness(float staminaFraction, bool exhausted, float below) =>
            exhausted || below <= 0f ? 1f : Mathf.Clamp01(1f - staminaFraction / below);
    }
}
