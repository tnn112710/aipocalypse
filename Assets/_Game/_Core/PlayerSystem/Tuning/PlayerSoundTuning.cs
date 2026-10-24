using Ami.BroAudio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    [CreateAssetMenu(fileName = "PlayerSoundTuning", menuName = "Aipocalypse/Player/Sound Tuning")]
    public sealed class PlayerSoundTuning : ScriptableObject
    {
        [field: SerializeField, Tooltip("One per footstep, timed by the head bob")]
        public SoundID FootstepSound { get; private set; }
        [field: SerializeField, Range(0f, 1f), Tooltip("Footstep volume while walking")]
        public float WalkStepVolume { get; private set; } = 0.7f;
        [field: SerializeField, Range(0f, 1f), Tooltip("Footstep volume while sprinting")]
        public float SprintStepVolume { get; private set; } = 1f;
        [field: SerializeField] public SoundID JumpSound { get; private set; }
        [field: SerializeField] public SoundID LandSound { get; private set; }
        [field: SerializeField, Min(0f), Tooltip("Landings slower than this (m/s) are silent")]
        public float LandingSoundMinSpeed { get; private set; } = 2f;
        [field: SerializeField, Min(0.1f), Tooltip("Landings this fast (m/s) play at full volume")]
        public float LandingSoundFullSpeed { get; private set; } = 8f;

        [field: Header("Out of breath"), SerializeField, Tooltip("Looped while out of breath (turn Loop on in its entity), louder as stamina drops")]
        public SoundID BreathSound { get; private set; }
        [field: SerializeField, InlineProperty, HideLabel, Tooltip("When it breathes heavily: PlayerVitalsTuning → Out of breath")]
        public BreathSoundSettings Breath { get; private set; } = new();
    }
}
