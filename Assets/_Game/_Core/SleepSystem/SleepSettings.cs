using Ami.BroAudio;
using MB13.Aipocalypse.Core.PlayerSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.SleepSystem
{
    // Sleeping in a bed: how long, how the time passes and what it looks like. Asset: Data/Sleep/SleepSettings.
    [CreateAssetMenu(fileName = "SleepSettings", menuName = "Aipocalypse/Sleep/Sleep Settings")]
    public sealed class SleepSettings : ScriptableObject
    {
        [Tooltip("Off: beds offer nothing")] public bool Enabled = true;

        [BoxGroup("Rules"), Min(1), Tooltip("Shortest sleep (game hours); wake-up times are whole clock hours")] public int MinHours = 1;
        [BoxGroup("Rules"), Min(1), Tooltip("Longest sleep (game hours)")] public int MaxHours = 12;
        [BoxGroup("Rules"), Range(0f, 23f), Tooltip("The wake-up hour offered first when it's in reach")] public float DefaultWakeHour = 7f;
        [BoxGroup("Rules"), Min(0f), Tooltip("The latest wake-up time is this long before a raid that hasn't run starts (game hours)")]
        public float WakeBeforeRaid = 0.5f;

        [BoxGroup("Moment"), Tooltip("Getting into the bed, in the bed anchor's space: x = the side you get in from, z = towards the pillow, " +
            "heights above the floor. Ends lying down")]
        public PoseSequence LieDown;
        [BoxGroup("Moment"), Tooltip("Getting out again. Its first beat (duration 0) is the lying pose, its last where the player ends up standing")]
        public PoseSequence GetUp;
        [BoxGroup("Moment"), Min(0f), Tooltip("Seconds the eyes take to close once lying")] public float FadeOut = 1.4f;
        [BoxGroup("Moment"), Min(0f), Tooltip("Seconds the slept hours take to pass")] public float SkipSeconds = 4f;
        [BoxGroup("Moment"), Min(0f), Tooltip("Seconds the eyes take to open")] public float FadeIn = 1.6f;

        [BoxGroup("Sounds"), Tooltip("Getting into the bed")] public SoundID LieDownSound;
        [BoxGroup("Sounds"), Tooltip("Waking up")] public SoundID WakeSound;
    }
}
