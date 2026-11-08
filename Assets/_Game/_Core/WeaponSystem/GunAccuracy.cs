using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // Extra spread while airborne (walking shows as aim sway instead): shrinks while aiming down sights and is scaled by
    // the holder's steadiness. It rises at once and settles back over time after landing.
    public sealed class GunAccuracy
    {
        readonly AccuracySettings _settings;

        // Degrees off the aim the next shot may go.
        public float Spread { get; private set; }

        public GunAccuracy(AccuracySettings settings) => _settings = settings;

        // steadiness: 1 = normal, lower = steadier.
        public void Tick(bool airborne, float aimWeight, float steadiness, float deltaTime)
        {
            float target = airborne ? _settings.AirborneSpread : 0f;
            target *= Mathf.Lerp(1f, _settings.AimMultiplier, Mathf.Clamp01(aimWeight)) * Mathf.Max(0f, steadiness);

            Spread = target >= Spread ? target : Mathf.Lerp(Spread, target, Damping.ByRate(_settings.SettleSpeed, deltaTime));
        }

        public void Reset() => Spread = 0f;
    }
}
