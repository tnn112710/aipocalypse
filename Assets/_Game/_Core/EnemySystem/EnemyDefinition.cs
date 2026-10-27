using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // One kind of enemy: its base stats, how it takes hits, and the behaviour tuning its tree's tasks read. Lives with the
    // enemy in Content/Enemies/<Name>/; behaviour and reaction tuning can be shared by several kinds.
    [CreateAssetMenu(fileName = "Enemy", menuName = "Aipocalypse/Enemies/Enemy")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Base value of each stat (MaxHealth, MoveSpeed in m/s); runtime modifiers apply on top (EnemyStats)")]
        Dictionary<EnemyStat, float> _stats = new() { { EnemyStat.MaxHealth, 100f }, { EnemyStat.MoveSpeed, 2f } };
        [Min(0.05f), Tooltip("Metres from a destination that count as arrived")] public float StopDistance = 0.3f;
        [Min(0f), Tooltip("Radius (m) of grass it bends around its feet, through the scene's GrassBendField; 0 = none")] public float GrassBendRadius = 0.6f;
        [Required, InlineEditor, Tooltip("Knockback, hit reactions and how long the body stays (EnemyBody)")] public EnemyReactionSettings Reaction;
        [Required, InlineEditor, Tooltip("Read by the Wander subtree's tasks")] public WanderSettings Wander;
        [InlineEditor, Tooltip("Read by the Melee subtree's tasks; empty for kinds that don't fight (they ignore the player)")]
        public MeleeSettings Melee;
        [InlineEditor, Tooltip("Glances, fidgets and leaning of an animated model (EnemyAnimator); empty for unanimated kinds")]
        public EnemyAnimationSettings Animation;

        public float BaseStat(EnemyStat stat) => _stats != null && _stats.TryGetValue(stat, out float value) ? value : 0f;
        public bool HasBaseStat(EnemyStat stat) => _stats != null && _stats.ContainsKey(stat);

#if UNITY_EDITOR
        // Setup scripts and tests only: runtime code never writes definitions.
        public void SetBaseStat(EnemyStat stat, float value)
        {
            _stats ??= new Dictionary<EnemyStat, float>();
            _stats[stat] = value;
        }
#endif
    }
}
