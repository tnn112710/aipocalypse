using Ami.BroAudio;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // The sounds of one enemy kind (EnemyDefinition.Sounds), played by its EnemyVoice; unset ones stay silent.
    [CreateAssetMenu(fileName = "Sounds", menuName = "Aipocalypse/Enemies/Enemy Sounds")]
    public sealed class EnemySoundSet : ScriptableObject
    {
        [Tooltip("Voice when a swing starts")] public SoundID Attack;
        [Range(0f, 1f), Tooltip("Share of swings that also get the attack voice")] public float AttackVoiceChance = 0.6f;
        [Tooltip("Whoosh of the swing")] public SoundID Swing;
        [Tooltip("A blow landing on its target")] public SoundID Impact;
        [Tooltip("Voice when hurt but not killed")] public SoundID Hurt;
        [Min(0f), Tooltip("Seconds between two hurt voices (a shotgun volley gives one)")] public float HurtCooldown = 0.3f;
        public SoundID Death;
        [Tooltip("Battle cry when it closes in on a target (enters EngageRadius)")] public SoundID Shout;
        [Min(0f), Tooltip("Seconds before the same enemy shouts again")] public float ShoutCooldown = 8f;
        [Tooltip("One step; played by EnemySoundManager per stride walked")] public SoundID Footstep;
        [Min(0.1f), Tooltip("Metres walked per footstep")] public float StrideLength = 1.2f;
        [Min(0f), Tooltip("Metres from the player within which its footsteps play")] public float FootstepRange = 20f;
    }
}
