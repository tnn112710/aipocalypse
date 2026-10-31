using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // A weight class (Light, Medium, Heavy, Boss): how hard hits push an enemy and how its stagger meter reacts. Kinds share
    // one through EnemyReactionSettings.Stagger; a kind that needs its own numbers gets its own asset. Hit power is
    // DamageInfo.Force (N·s), so a rocket is just a big Force.
    [CreateAssetMenu(fileName = "Stagger", menuName = "Aipocalypse/Enemies/Stagger Profile")]
    public sealed class StaggerProfile : ScriptableObject
    {
        [Min(0f), Tooltip("Scales the knockback push (EnemyReactionSettings.KnockbackPerForce): 1 = full slide, 0 = never moved")]
        public float PushScale = 1f;
        [Min(0f), Tooltip("Meter points drained per second, so single taps fade and only sustained or heavy fire builds up")]
        public float Recovery = 4f;
        [Min(1f), Tooltip("Hits on a critical hitbox (the head) add this many times their power")]
        public float CriticalMultiplier = 2f;

        [Header("Flinch: slowed for a moment")]
        public bool Flinches = true;
        [Min(0f), Tooltip("Meter at which a hit makes it flinch")] public float FlinchAt = 1f;
        [Range(0f, 1f), Tooltip("Move speed multiplier while flinching")] public float FlinchSlow = 0.3f;
        [Min(0f), Tooltip("Seconds a flinch lasts; each further flinching hit starts it again")] public float FlinchTime = 0.25f;

        [Header("Stagger: stopped, its swing cancelled")]
        public bool Staggers = true;
        [Min(0f), Tooltip("Meter at which it staggers (the meter empties)")] public float StaggerAt = 6f;
        [Min(0f), Tooltip("Seconds it stands staggered")] public float StaggerTime = 0.8f;
        [Min(0f), Tooltip("Seconds after a stagger ends before it can stagger again (it still flinches), so it can't be stun-locked")]
        public float StaggerCooldown = 2f;

        void OnValidate() => StaggerAt = Mathf.Max(StaggerAt, FlinchAt);
    }
}
