using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    [CreateAssetMenu(fileName = "PlayerCameraTuning", menuName = "Aipocalypse/Player/Camera Tuning")]
    public sealed class PlayerCameraTuning : ScriptableObject
    {
        [field: Header("Look (degrees)"), SerializeField, Range(-90f, 0f)] public float MinPitch { get; private set; } = -85f;
        [field: SerializeField, Range(0f, 90f)] public float MaxPitch { get; private set; } = 85f;

        [field: Header("Head bob"), SerializeField, Min(0.1f), Tooltip("Metres per footstep; bob follows distance, not time")]
        public float StepLength { get; private set; } = 1.7f;
        [field: SerializeField, Min(0f)] public float WalkBobAmplitude { get; private set; } = 0.03f;
        [field: SerializeField, Min(0f)] public float SprintBobAmplitude { get; private set; } = 0.055f;
        [field: SerializeField, Range(0f, 2f)] public float BobSideRatio { get; private set; } = 0.5f;
        [field: SerializeField, Min(0f)] public float BobFadeSpeed { get; private set; } = 8f;

        [field: Header("Landing dip"), SerializeField, Min(0f), Tooltip("Impacts slower than this (m/s) don't dip")]
        public float LandingDipMinSpeed { get; private set; } = 3f;
        [field: SerializeField, Min(0f), Tooltip("Dip speed per m/s of impact")]
        public float LandingDipPerSpeed { get; private set; } = 0.35f;
        [field: SerializeField, Min(0f)] public float LandingDipMax { get; private set; } = 0.25f;
        [field: SerializeField, Min(1f)] public float LandingDipStiffness { get; private set; } = 120f;
        [field: SerializeField, Min(0f), Tooltip("Head nod in degrees per metre of dip")]
        public float LandingDipPitch { get; private set; } = 15f;
    }
}
