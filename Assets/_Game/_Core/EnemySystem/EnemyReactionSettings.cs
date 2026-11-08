using MB13.Aipocalypse.Core.Effects;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // How an enemy kind takes hits and dies: its weight class (Stagger), knockback from DamageInfo.Force, hit animations, how
    // long the body stays, and its blood (pooled hit effects).
    [CreateAssetMenu(fileName = "EnemyReactionSettings", menuName = "Aipocalypse/Enemies/Enemy Reaction Settings")]
    public sealed class EnemyReactionSettings : ScriptableObject
    {
        [InlineEditor, Tooltip("Weight class: push scale and the stagger meter (flinch, stagger). Empty = full push, hits only twitch")]
        public StaggerProfile Stagger;
        [Min(0f), Tooltip("Push speed (m/s) per N·s of hit force. The push slides about speed / Drag metres")]
        public float KnockbackPerForce = 1.6f;
        [Min(0.1f), Tooltip("How fast the push dies out (1/s): higher = shorter, snappier slide")]
        public float KnockbackDrag = 8f;
        [Min(0f), Tooltip("Fastest push (m/s), whatever lands in one frame (a full shotgun blast)")]
        public float MaxKnockbackSpeed = 8f;
        [Min(0f), Tooltip("The killing hit pushes this many times harder, so a body is thrown back by a heavy shot")]
        public float KillPushScale = 2f;

        [Min(0f), Tooltip("Push speed (m/s) at which a hit plays the heavy reaction instead of the light one")]
        public float HeavyHitSpeed = 4f;
        [Min(0f), Tooltip("Seconds a body lies after dying before it sinks")]
        public float CorpseTime = 6f;
        [Min(0.01f), Tooltip("Seconds it takes to sink out of sight, then it's disabled")]
        public float SinkTime = 2f;
        [Min(0f), Tooltip("Metres it sinks")]
        public float SinkDepth = 1.2f;

        [Header("Hit effects (pooled; empty = none)")]
        [Tooltip("At the hit point, facing back along the shot")] public PooledEffect HitEffect;
        [Tooltip("A hit on a critical hitbox (headshot); empty = HitEffect")] public PooledEffect CriticalEffect;
        [Tooltip("Added on the killing hit")] public PooledEffect KillEffect;
        [Min(0), Tooltip("Copies of each effect made with each enemy of the kind (prewarmed before a raid), so a fight's " +
            "first hits don't instantiate them")]
        public int EffectPrewarm = 12;
    }
}
