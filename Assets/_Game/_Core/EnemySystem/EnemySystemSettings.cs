using System.Collections.Generic;
using MB13.Aipocalypse.Core.Entities;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // The enemy system's budgets and catalogue: shared by every enemy kind in a scene.
    [CreateAssetMenu(fileName = "EnemySystemSettings", menuName = "Aipocalypse/Enemies/Enemy System Settings")]
    public sealed class EnemySystemSettings : ScriptableObject
    {
        [Min(0.01f), Tooltip("Seconds between two thinks of one enemy's behaviour tree, at any frame rate. Lower = sharper, costlier")]
        public float ThinkInterval = 1f / 15f;
        [Min(1), Tooltip("Most trees ticked in one frame; a crowd past this thinks a little slower instead of spiking the frame")]
        public int MaxThinksPerFrame = 200;
        [Min(0f), Tooltip("Enemies farther than this from the player (m) think only every FarThinkInterval; 0 = all think at ThinkInterval")]
        public float FarThinkDistance = 60f;
        [Min(0.01f), Tooltip("Seconds between two thinks of a far enemy (it keeps walking in between)")]
        public float FarThinkInterval = 0.25f;

        [Header("Fights")]
        [SerializeField, Tooltip("How many enemies may close in on one target at once (AttackSlots); a kind without an entry is unlimited")]
        Dictionary<TargetKind, int> _attackersPerTarget = new() { { TargetKind.Player, 3 } };

        [Header("Spawning")]
        [Tooltip("Kinds this scene can spawn by name (enemy.spawn). Budgets, pooling and group spread: SpawnSystemSettings")]
        public List<EnemySpawnCard> Cards = new();

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
