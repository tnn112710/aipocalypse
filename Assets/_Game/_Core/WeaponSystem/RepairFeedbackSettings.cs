using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    [CreateAssetMenu(menuName = "Aipocalypse/Weapons/Repair Feedback", fileName = "RepairFeedback")]
    public sealed class RepairFeedbackSettings : ScriptableObject
    {
        public Color SuccessColor = new(0.5f, 1f, 0.64f, 1f);
        public Color MissColor = new(1f, 0.36f, 0.18f, 1f);
        [Min(0.05f)] public float SuccessDuration = 1f;
        [Min(0f), Tooltip("Time to keep the successful widget visible before its final fade.")] public float SuccessHold = 0.32f;
        [Range(1f, 1.5f)] public float SuccessScale = 1.35f;
        [Range(1f, 2.5f)] public float BurstScale = 2.1f;
        [Min(0.05f)] public float MissDuration = 0.3f;
        [Min(0f)] public float MissShakePixels = 12f;
        [Min(0.05f)] public float NaturalFadeDuration = 0.18f;
        [Min(0.05f)] public float WeaponKickDuration = 0.32f;
        public Vector3 WeaponKickPosition = new(0f, 0.004f, -0.018f);
        public Vector3 WeaponKickEuler = new(-2.3f, 0.5f, 0.9f);
        public AudioClip SuccessSound;
        [Range(0f, 1f)] public float SuccessVolume = 1f;
        public AudioClip SuccessAccent;
        [Range(0f, 1f)] public float AccentVolume = 0.65f;
        public AudioClip MissSound;
        [Range(0f, 1f)] public float MissVolume = 0.5f;
        public AudioClip NaturalSound;
        [Range(0f, 1f)] public float NaturalVolume = 0.45f;
    }
}
