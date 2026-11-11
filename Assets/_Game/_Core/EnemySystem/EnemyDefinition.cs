using System.Collections.Generic;
using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.EquipmentSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // One kind of enemy: its base stats, how it takes hits, and the behaviour tuning its tree's tasks read. Lives with the
    // enemy in Content/Enemies/<Name>/; behaviour and reaction tuning can be shared by several kinds.
    [CreateAssetMenu(fileName = "Enemy", menuName = "Aipocalypse/Enemies/Enemy")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Base value of each stat (MaxHealth, MoveSpeed in m/s, AttackDamage per blow); runtime modifiers apply on top (EnemyStats)")]
        Dictionary<EnemyStat, float> _stats = new() { { EnemyStat.MaxHealth, 100f }, { EnemyStat.MoveSpeed, 2f }, { EnemyStat.AttackDamage, 0f } };
        [SerializeField, Tooltip("What it hunts and how much it prefers each kind (TargetSelector: weight per metre); none = it hunts nothing")]
        Dictionary<TargetKind, float> _targets = new() { { TargetKind.Player, 1f } };
        [Range(0f, 0.5f), Tooltip("Each life's MoveSpeed is scaled by a random 1 ± this, so a group strings out instead of moving in step")]
        public float SpeedVariance = 0.12f;
        [Min(0.05f), Tooltip("Metres from a destination that count as arrived")] public float StopDistance = 0.3f;
        [Min(0f), Tooltip("Seconds a spawned enemy stands still before it starts thinking, while its appear clip plays (placed enemies don't)")]
        public float AppearTime = 1f;
        [Min(0f), Tooltip("Up to this many seconds more, at random per enemy, so a group doesn't set off together")]
        public float AppearJitter = 0.5f;
        [Min(0f), Tooltip("Radius (m) of grass it bends around its feet, through the scene's GrassBendField; 0 = none")] public float GrassBendRadius = 0.6f;

        [Header("Senses")]
        [Min(0f), Tooltip("Metres within which it notices the player when nothing solid is in the way (a fence is seen through); " +
            "a hit, or an alarmed neighbour, makes it notice at any distance. 0 = it always knows where the player is")]
        public float SpotRange = 20f;
        [Min(0f), Tooltip("Metres around it within which enemies are alarmed when it notices the player (one hop: they don't pass it on)")]
        public float AlertRadius = 12f;
        [Min(0f), Tooltip("Seconds without a sight of the player (out of 1.5 × SpotRange, or behind something) before it forgets " +
            "them and goes back to its objective or roams")]
        public float LoseSightTime = 6f;
        [Tooltip("What blocks its sight; its own layer (other enemies) and barriers never do")]
        public LayerMask SightBlockers = Physics.DefaultRaycastLayers;
        [InlineEditor, Tooltip("Its elite tier (chance, stats, mark); empty = never elite")] public EliteSettings Elite;
        [Required, InlineEditor, Tooltip("Knockback, hit reactions and how long the body stays (EnemyBody)")] public EnemyReactionSettings Reaction;
        [Required, InlineEditor, Tooltip("Read by the Wander subtree's tasks")] public WanderSettings Wander;
        [InlineEditor, Tooltip("Read by the Melee subtree's tasks; empty for kinds that don't fight (they ignore the player)")]
        public MeleeSettings Melee;
        [InlineEditor, Tooltip("Read by the Ranged subtree's tasks; empty for kinds that don't throw or shoot")]
        public RangedSettings Ranged;
        [InlineEditor, Tooltip("Glances, fidgets and leaning of an animated model (EnemyAnimator); empty for unanimated kinds")]
        public EnemyAnimationSettings Animation;
        [InlineEditor, Tooltip("Voice, swing, impact and footsteps (EnemyVoice); empty = silent")] public EnemySoundSet Sounds;
        [InlineEditor, Tooltip("What its body holds when it dies (searched through its LootContainer); empty = nothing")]
        public LootTable Loot;

        public IReadOnlyDictionary<TargetKind, float> Targets => _targets;

        public float BaseStat(EnemyStat stat) => _stats != null && _stats.TryGetValue(stat, out float value) ? value : 0f;
        public bool HasBaseStat(EnemyStat stat) => _stats != null && _stats.ContainsKey(stat);

#if UNITY_EDITOR
        // Setup scripts and tests only: runtime code never writes definitions.
        public void SetBaseStat(EnemyStat stat, float value)
        {
            _stats ??= new Dictionary<EnemyStat, float>();
            _stats[stat] = value;
        }

        public void SetTargetWeight(TargetKind kind, float weight)
        {
            _targets ??= new Dictionary<TargetKind, float>();
            _targets[kind] = weight;
        }
#endif
    }
}
