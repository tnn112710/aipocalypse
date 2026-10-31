using Ami.BroAudio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    [CreateAssetMenu(fileName = "PlayerSoundTuning", menuName = "Aipocalypse/Player/Sound Tuning")]
    public sealed class PlayerSoundTuning : ScriptableObject
    {
        [SerializeField, Tooltip("One per footstep, timed by the head bob")]
        SoundID _footstepSound;
        [SerializeField, Range(0f, 1f), Tooltip("Footstep volume while walking")]
        float _walkStepVolume = 0.7f;
        [SerializeField, Range(0f, 1f), Tooltip("Footstep volume while sprinting")]
        float _sprintStepVolume = 1f;
        [SerializeField] SoundID _jumpSound;
        [SerializeField] SoundID _landSound;
        [SerializeField, Min(0f), Tooltip("Landings slower than this (m/s) are silent")]
        float _landingSoundMinSpeed = 2f;
        [SerializeField, Min(0.1f), Tooltip("Landings this fast (m/s) play at full volume")]
        float _landingSoundFullSpeed = 8f;

        [Header("Hurt"), SerializeField, Tooltip("Pain grunt on a hit that doesn't kill (death has its own sounds in the death sequence)")]
        SoundID _hurtSound;
        [SerializeField, Min(0f), Tooltip("Seconds before another grunt")] float _hurtSoundCooldown = 0.4f;

        [Header("Out of breath"), SerializeField, Tooltip("Looped while out of breath (turn Loop on in its entity), louder as stamina drops")]
        SoundID _breathSound;
        [SerializeField, InlineProperty, HideLabel, Tooltip("When it breathes heavily: PlayerVitalsTuning → Out of breath")]
        BreathSoundSettings _breath = new();

        public SoundID FootstepSound => _footstepSound;
        public float WalkStepVolume => _walkStepVolume;
        public float SprintStepVolume => _sprintStepVolume;
        public SoundID JumpSound => _jumpSound;
        public SoundID LandSound => _landSound;
        public float LandingSoundMinSpeed => _landingSoundMinSpeed;
        public float LandingSoundFullSpeed => _landingSoundFullSpeed;
        public SoundID HurtSound => _hurtSound;
        public float HurtSoundCooldown => _hurtSoundCooldown;
        public SoundID BreathSound => _breathSound;
        public BreathSoundSettings Breath => _breath;
    }
}
