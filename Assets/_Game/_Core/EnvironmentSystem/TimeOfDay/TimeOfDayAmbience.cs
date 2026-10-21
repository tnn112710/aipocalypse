using System;
using Aipocalypse.Core.WorldTimeSystem;
using Ami.BroAudio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // The day's soundscape: looping beds whose volume follows a curve over the hours (dawn chorus), and calls played
    // now and then inside their hours (owls at night). Sounds are BroAudio entities (Data/Audio/Ambience).
    [CreateAssetMenu(menuName = "Aipocalypse/Environment/Time Of Day Ambience", fileName = "Ambience")]
    public sealed class TimeOfDayAmbience : ScriptableObject
    {
        [Serializable]
        public struct Bed
        {
            public string Name;
            public SoundID Sound;
            [Tooltip("Volume 0–1 over the day: time axis 0–24 h")] public AnimationCurve Volume;
        }

        [Serializable]
        public struct Calls
        {
            public string Name;
            public SoundID Sound;
            public TimeWindow Hours;
            [MinMaxSlider(1f, 180f, true), Tooltip("Real seconds between calls")] public Vector2 Interval;
            [Range(0f, 1f)] public float Volume;
        }

        public Bed[] Beds = Array.Empty<Bed>();
        public Calls[] OneShots = Array.Empty<Calls>();
        [Min(0f), Tooltip("Seconds a bed takes to follow its curve")] public float VolumeFade = 2f;
    }
}
