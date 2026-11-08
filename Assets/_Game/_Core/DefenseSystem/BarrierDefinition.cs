using System.Collections.Generic;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Effects;
using Ami.BroAudio;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DefenseSystem
{
    // What a kind of barrier is (wood fence, wood gate, later reinforced wood or a rock wall): health, how it takes damage,
    // repairs, its look and sounds, what striking it does to the attacker, and what it upgrades to.
    [CreateAssetMenu(fileName = "BarrierDefinition", menuName = "Aipocalypse/Defense/Barrier Definition")]
    public sealed class BarrierDefinition : ScriptableObject
    {
        [Tooltip("Shown in prompts: \"[E] Repair Fence\"")] public string DisplayName = "Fence";
        [Min(1f)] public float MaxHealth = 300f;
        [Tooltip("Damage multiplier per damage type; no entry = 1. Player damage never counts")]
        public Dictionary<DamageType, float> DamageScale = new();

        [Header("Repair")]
        [Min(0f), Tooltip("Health one hammer knock gives back")] public float RepairPerKnock = 30f;
        [Range(0.05f, 1f), Tooltip("Share of the maximum health a broken one needs back before it stands again")]
        public float StandAt = 0.3f;

        [Header("Look")]
        [Tooltip("Model spawned under the segment: lean per damage stage, fallen when broken (and the leaf's hinge on a gate)")]
        public BarrierLook Look;
        [Tooltip("Optional: splinters where a blow lands")] public PooledEffect HitEffect;
        [Tooltip("Optional: debris when it breaks, emitted from a 1 m wide box (scaled to the barrier's width, Shape scaling)")] public PooledEffect BreakEffect;
        [Tooltip("Optional: dust at each repair knock")] public PooledEffect RepairEffect;

        [Header("Sounds")]
        public SoundID HitSound;
        public SoundID BreakSound;
        [Tooltip("Each repair knock")] public SoundID RepairSound;
        [Tooltip("Gates only")] public SoundID OpenSound;
        [Tooltip("Gates only")] public SoundID CloseSound;

        [Header("Behaviour and upgrades")]
        [Tooltip("What a blow on it does to the attacker (thorns…), in order")]
        public List<BarrierEffect> OnStruck = new();
        [Tooltip("The next tier (BarrierSegment.Upgrade); empty = top tier")] public BarrierDefinition UpgradesTo;

        public float ScaleFor(DamageType type) => DamageScale != null && DamageScale.TryGetValue(type, out float scale) ? scale : 1f;
    }
}
