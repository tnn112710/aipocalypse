using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
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
    }
}
