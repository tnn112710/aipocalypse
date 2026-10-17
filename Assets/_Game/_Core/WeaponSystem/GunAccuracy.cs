using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // Extra spread from moving and jumping: grows with speed (more in the air), shrinks while aiming down sights,
    // and is scaled by the holder's steadiness. It rises at once and settles back over time.
    public sealed class GunAccuracy
    {
        readonly AccuracySettings _settings;

        // Degrees off the aim the next shot may go.
        public float Spread { get; private set; }

        public GunAccuracy(AccuracySettings settings) => _settings = settings;

        // movement: horizontal speed as a share of walking speed. steadiness: 1 = normal, lower = steadier.
        public void Tick(float movement, bool airborne, float aimWeight, float steadiness, float deltaTime)
        {
            float target = airborne ? _settings.AirborneSpread : _settings.MovingSpread * Mathf.Clamp01(movement);
            target *= Mathf.Lerp(1f, _settings.AimMultiplier, Mathf.Clamp01(aimWeight)) * Mathf.Max(0f, steadiness);

            Spread = target >= Spread || _settings.SettleSpeed <= 0f
                ? target
                : Mathf.Lerp(Spread, target, 1f - Mathf.Exp(-_settings.SettleSpeed * deltaTime));
        }

        public void Reset() => Spread = 0f;
    }
}
