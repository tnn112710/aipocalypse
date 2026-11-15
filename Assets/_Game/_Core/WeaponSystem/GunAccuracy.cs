using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // This frame's inputs to the hip envelope. Scales: 1 = normal.
    public struct AccuracyInput
    {
        public float Cone;          // the gun's hip cone at this aim weight (GunStats.ConeAt), degrees
        public float MoveScale;     // walk / crouch / sprint, from the holder
        public float Turn;          // degrees added by turning fast, from the holder
        public float AimWeight;
        public bool Airborne;
        public float AirScale;      // the holder's airborne spread scale
        public float Fatigue;
        public float Steadiness;    // lower = steadier hands
    }

    // The hip envelope: how far the aim may wander (degrees), from the gun's cone, movement, airborne, shot bloom,
    // fatigue and steadiness. The gun's visible wobble fills it; it isn't a hidden cone. It rises at once and settles.
    public sealed class GunAccuracy
    {
        readonly AccuracySettings _settings;
        AccuracyInput _input;
        float _bloom;
        float _sinceShot;

        public float Spread { get; private set; }
        // Degrees the recent hip shots add (before aiming, fatigue and steadiness).
        public float Bloom => _bloom;

        public GunAccuracy(AccuracySettings settings) => _settings = settings;

        public void Tick(in AccuracyInput input, float deltaTime)
        {
            _input = input;
            _sinceShot += deltaTime;
            if (_sinceShot >= _settings.BloomRecoveryDelay)
                _bloom = Mathf.MoveTowards(_bloom, 0f, _settings.BloomRecovery * deltaTime);

            float aim = Mathf.Clamp01(input.AimWeight);
            float air = input.Airborne ? _settings.AirborneSpread * Mathf.Lerp(1f, _settings.AimMultiplier, aim) * input.AirScale : 0f;
            float target = (input.Cone * input.MoveScale + air + (_bloom + input.Turn) * (1f - aim)) * Scale(input);

            Spread = target >= Spread ? target : Mathf.Lerp(Spread, target, Damping.ByRate(_settings.SettleSpeed, deltaTime));
        }

        // Adds a shot's bloom; returns the jolt it gives the wobble (degrees), from the last Tick's input.
        public float AddShot()
        {
            _bloom = Mathf.Min(_bloom + _settings.BloomPerShot, _settings.MaxBloom);
            _sinceShot = 0f;
            return _settings.BloomPerShot * (1f - Mathf.Clamp01(_input.AimWeight)) * Scale(_input);
        }

        public void Reset()
        {
            Spread = 0f;
            _bloom = 0f;
        }

        static float Scale(in AccuracyInput input) => Mathf.Max(0f, input.Fatigue) * Mathf.Max(0f, input.Steadiness);
    }
}
