using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // How an enemy kind takes hits and dies: knockback from DamageInfo.Force, hit animations, and how long the body stays.
    [CreateAssetMenu(fileName = "EnemyReactionSettings", menuName = "Aipocalypse/Enemies/Enemy Reaction Settings")]
    public sealed class EnemyReactionSettings : ScriptableObject
    {
        [Min(0f), Tooltip("Push speed (m/s) per N·s of hit force. The push slides about speed / Drag metres")]
        public float KnockbackPerForce = 1.6f;
        [Min(0.1f), Tooltip("How fast the push dies out (1/s): higher = shorter, snappier slide")]
        public float KnockbackDrag = 8f;
        [Min(0f), Tooltip("Fastest push (m/s), whatever lands in one frame (a full shotgun blast)")]
        public float MaxKnockbackSpeed = 8f;
        [Min(0f), Tooltip("Push speed (m/s) at which a hit plays the heavy reaction instead of the light one")]
        public float HeavyHitSpeed = 4f;
        [Min(0f), Tooltip("Seconds a body lies after dying before it sinks")]
        public float CorpseTime = 6f;
        [Min(0.01f), Tooltip("Seconds it takes to sink out of sight, then it's disabled")]
        public float SinkTime = 2f;
        [Min(0f), Tooltip("Metres it sinks")]
        public float SinkDepth = 1.2f;
    }
}
