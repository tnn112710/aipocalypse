using System.Collections.Generic;
using Aipocalypse.Core.Entities;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // The enemy system's budgets and catalogue: shared by every enemy kind in a scene.
    [CreateAssetMenu(fileName = "EnemySystemSettings", menuName = "Aipocalypse/Enemies/Enemy System Settings")]
    public sealed class EnemySystemSettings : ScriptableObject
    {
        [Min(0.01f), Tooltip("Seconds between two thinks of one enemy's behaviour tree, at any frame rate. Lower = sharper, costlier")]
        public float ThinkInterval = 1f / 15f;
        [Min(1), Tooltip("Most trees ticked in one frame; a crowd past this thinks a little slower instead of spiking the frame")]
        public int MaxThinksPerFrame = 200;

        [Header("Fights")]
        [SerializeField, Tooltip("How many enemies may close in on one target at once (AttackSlots); a kind without an entry is unlimited")]
        Dictionary<TargetKind, int> _attackersPerTarget = new() { { TargetKind.Player, 3 } };

        [Header("Spawning")]
        [Tooltip("Kinds this scene can spawn by name (enemy.spawn) and prewarm")] public List<EnemySpawnCard> Cards = new();
        [Min(1), Tooltip("Pooled enemies created per frame while prewarming, so filling the pool doesn't hitch")]
        public int PrewarmPerFrame = 2;
        [Min(1), Tooltip("Enemies a spawned group brings into play per frame (waking one costs several ms), so a wave doesn't hitch")]
        public int SpawnsPerFrame = 1;
        [Tooltip("Metres round its spawn point a group spreads over: a random radius in this range per group (widened for big groups)")]
        public Vector2 GroupRadius = new(3f, 5f);
        [Min(0.5f), Tooltip("Metres kept between neighbours in a group; a big group spreads wider than GroupRadius to keep it")]
        public float GroupSpacing = 1.2f;

        [Header("Looks")]
        [Tooltip("Added to every enemy renderer: the night highlight (a light only enemies receive) picks them by it")]
        public RenderingLayerMask RenderingLayer;

        [Header("Sound")]
        [Min(1), Tooltip("Most enemy footsteps played per second, all enemies together")] public int MaxFootstepsPerSecond = 12;

        public IReadOnlyDictionary<TargetKind, int> AttackersPerTarget => _attackersPerTarget;

#if UNITY_EDITOR
        // Setup scripts and tests only.
        public void SetAttackersPerTarget(TargetKind kind, int count) => _attackersPerTarget[kind] = count;
#endif
    }
}
