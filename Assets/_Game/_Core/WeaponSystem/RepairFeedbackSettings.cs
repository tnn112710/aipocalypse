using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // An additive, self-returning offset of the gun model: one kick, or a damped wobble.
    [Serializable]
    public struct GunJolt
    {
        [Tooltip("Local offset at the peak (m)")] public Vector3 Position;
        [Tooltip("Local rotation at the peak (degrees)")] public Vector3 Euler;
        [Min(0.01f)] public float Duration;
        [Min(0f), Tooltip("0 = one kick and return; more = that many damped back-and-forth swings")] public float Wobbles;

        public GunJolt(Vector3 position, Vector3 euler, float duration, float wobbles = 0f)
        {
            Position = position;
            Euler = euler;
            Duration = duration;
            Wobbles = wobbles;
        }

        public bool IsEmpty => Duration <= 0f || (Position == Vector3.zero && Euler == Vector3.zero);

        // Signed weight 0..1 at normalized time t.
        public float Weight(float t)
        {
            if (t <= 0f || t >= 1f)
                return 0f;
            if (Wobbles > 0f)
            {
                float decay = 1f - t;
                return Mathf.Sin(t * Mathf.PI * 2f * Wobbles) * decay * decay;
            }
            const float attack = 0.15f;
            if (t < attack)
            {
                float rise = 1f - t / attack;
                return 1f - rise * rise;
            }
            float fall = 1f - (t - attack) / (1f - attack);
            return fall * fall * fall;
        }
    }

    [CreateAssetMenu(menuName = "Aipocalypse/Weapons/Repair Feedback", fileName = "RepairFeedback")]
    public sealed class RepairFeedbackSettings : ScriptableObject
    {
        [BoxGroup("Jam")] public GunJolt JamJolt = new(new Vector3(0f, -0.006f, 0.012f), new Vector3(3f, -1.5f, 4f), 0.35f, 2f);
        [BoxGroup("Jam"), Tooltip("Each trigger pull while the gun is stuck")] public GunJolt DryFireJolt = new(new Vector3(0f, 0f, -0.004f), new Vector3(-0.8f, 0f, 0.6f), 0.12f);

        [BoxGroup("Repair hits")] public GunJolt SlapJolt = new(new Vector3(0.004f, -0.003f, 0f), new Vector3(-1f, 1.5f, -2f), 0.18f);
        [BoxGroup("Repair hits")] public GunJolt PunchJolt = new(new Vector3(0.007f, -0.006f, 0.004f), new Vector3(-2f, 3f, -4f), 0.24f, 1.5f);

        [BoxGroup("Success")] public Color SuccessColor = new(0.5f, 1f, 0.64f, 1f);
        [BoxGroup("Success"), Min(0.05f)] public float SuccessDuration = 1f;
        [BoxGroup("Success"), Min(0f), Tooltip("Time to keep the successful widget visible before its final fade.")] public float SuccessHold = 0.32f;
        [BoxGroup("Success"), Range(1f, 1.8f)] public float SuccessScale = 1.5f;
        [BoxGroup("Success"), Range(1f, 3f)] public float BurstScale = 2.6f;
        [BoxGroup("Success")] public GunJolt SuccessJolt = new(new Vector3(0f, 0.008f, -0.03f), new Vector3(-5f, 1.5f, 2.5f), 0.4f);
        [BoxGroup("Success"), Tooltip("The hit that frees the gun")] public AudioClip SuccessSound;
        [BoxGroup("Success"), Range(0f, 1f)] public float SuccessVolume = 1f;
        [BoxGroup("Success"), Tooltip("Reward chime")] public AudioClip SuccessAccent;
        [BoxGroup("Success"), Range(0f, 1f)] public float AccentVolume = 0.65f;
        [BoxGroup("Success"), Tooltip("Action snapping home, a moment after the hit")] public AudioClip SuccessMechanism;
        [BoxGroup("Success"), Range(0f, 1f)] public float MechanismVolume = 1f;
        [BoxGroup("Success"), Min(0f)] public float MechanismDelay = 0.12f;

        [BoxGroup("Miss")] public Color MissColor = new(1f, 0.2f, 0.12f, 1f);
        [BoxGroup("Miss"), Min(0.05f)] public float MissDuration = 0.4f;
        [BoxGroup("Miss"), Min(0f)] public float MissShakePixels = 22f;
        [BoxGroup("Miss"), Range(0f, 0.5f), Tooltip("Widget squash on a miss")] public float MissSquash = 0.18f;
        [BoxGroup("Miss"), Tooltip("Fumbled hit")] public GunJolt MissJolt = new(new Vector3(-0.006f, -0.01f, 0.006f), new Vector3(4f, -4f, 7f), 0.45f, 2.5f);
        [BoxGroup("Miss")] public AudioClip MissSound;
        [BoxGroup("Miss"), Range(0f, 1f)] public float MissVolume = 0.8f;
        [BoxGroup("Miss"), Tooltip("Dull thud layered under the miss sound")] public AudioClip MissAccent;
        [BoxGroup("Miss"), Range(0f, 1f)] public float MissAccentVolume = 0.8f;

        [BoxGroup("Natural completion"), Min(0.05f)] public float NaturalFadeDuration = 0.18f;
        [BoxGroup("Natural completion")] public AudioClip NaturalSound;
        [BoxGroup("Natural completion"), Range(0f, 1f)] public float NaturalVolume = 0.45f;
    }
}
