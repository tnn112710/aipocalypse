using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    [CreateAssetMenu(fileName = "PlayerCameraTuning", menuName = "Aipocalypse/Player/Camera Tuning")]
    public sealed class PlayerCameraTuning : ScriptableObject
    {
        [Header("Eye"), SerializeField, Min(0.1f), Tooltip("Camera height above the feet (m) when standing")]
        float _eyeHeight = 1.65f;

        [Header("Look (degrees)"), SerializeField, Range(-90f, 0f)] float _minPitch = -85f;
        [SerializeField, Range(0f, 90f)] float _maxPitch = 85f;

        [Header("Head bob"), SerializeField, Min(0.1f), Tooltip("Metres per footstep; bob follows distance, not time")]
        float _stepLength = 1.7f;
        [SerializeField, Min(0f)] float _walkBobAmplitude = 0.03f;
        [SerializeField, Min(0f)] float _sprintBobAmplitude = 0.055f;
        [SerializeField, Range(0f, 2f)] float _bobSideRatio = 0.5f;
        [SerializeField, Min(0f)] float _bobFadeSpeed = 8f;
        [SerializeField, Range(0f, 1f), Tooltip("Footsteps play only while the bob is above this share of walking speed")]
        float _stepMinIntensity = 0.3f;

        [Header("Landing dip"), SerializeField, Min(0f), Tooltip("Impacts slower than this (m/s) don't dip")]
        float _landingDipMinSpeed = 3f;
        [SerializeField, Min(0f), Tooltip("Dip speed per m/s of impact")]
        float _landingDipPerSpeed = 0.35f;
        [SerializeField, Min(0f)] float _landingDipMax = 0.25f;
        [SerializeField, Min(1f)] float _landingDipStiffness = 120f;
        [SerializeField, Min(0f), Tooltip("Head nod in degrees per metre of dip")]
        float _landingDipPitch = 15f;

        [Header("Hurt flinch"), SerializeField, Min(0f), Tooltip("Degrees the view jerks up per point of damage taken")]
        float _hurtKickPerDamage = 0.15f;
        [SerializeField, Min(0f), Tooltip("Most degrees of flinch, however hard or often it's hit")] float _hurtKickMax = 5f;
        [SerializeField, Min(1f), Tooltip("Higher = the view settles back faster")] float _hurtKickStiffness = 150f;

        [Header("Sprint"), SerializeField, Range(1f, 1.3f), Tooltip("Field of view multiplier while sprinting (1 = none)")]
        float _sprintFovScale = 1.07f;
        [SerializeField, Min(0f), Tooltip("How fast the view widens and settles (higher = sooner)")] float _sprintFovSpeed = 6f;

        [Header("Aiming"), SerializeField, Range(0f, 1f), Tooltip("How much of the aim zoom the held gun's own lens takes: 0 = the gun " +
            "keeps its size while the world zooms, 1 = it zooms with the world (sights grow on screen, COD-style)")]
        float _heldAimZoomShare = 1f;

        public float HeldAimZoomShare => _heldAimZoomShare;
        public float SprintFovScale => _sprintFovScale;
        public float SprintFovSpeed => _sprintFovSpeed;
        public float EyeHeight => _eyeHeight;
        public float MinPitch => _minPitch;
        public float MaxPitch => _maxPitch;
        public float StepLength => _stepLength;
        public float WalkBobAmplitude => _walkBobAmplitude;
        public float SprintBobAmplitude => _sprintBobAmplitude;
        public float BobSideRatio => _bobSideRatio;
        public float BobFadeSpeed => _bobFadeSpeed;
        public float StepMinIntensity => _stepMinIntensity;
        public float LandingDipMinSpeed => _landingDipMinSpeed;
        public float LandingDipPerSpeed => _landingDipPerSpeed;
        public float LandingDipMax => _landingDipMax;
        public float LandingDipStiffness => _landingDipStiffness;
        public float LandingDipPitch => _landingDipPitch;
        public float HurtKickPerDamage => _hurtKickPerDamage;
        public float HurtKickMax => _hurtKickMax;
        public float HurtKickStiffness => _hurtKickStiffness;
    }
}
