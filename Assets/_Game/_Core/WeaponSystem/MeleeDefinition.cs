using Aipocalypse.Core.EquipmentSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // Everything that makes one melee weapon swing the way it does. How the swing looks is on its held prefab.
    [CreateAssetMenu(fileName = "Melee", menuName = "Aipocalypse/Items/Melee")]
    public sealed class MeleeDefinition : EquippableDefinition
    {
        [field: SerializeField, FoldoutGroup("Swing", true), HideLabel, InlineProperty]
        public SwingSettings Swing { get; private set; } = new();

        [field: SerializeField, FoldoutGroup("Sounds"), HideLabel, InlineProperty]
        public MeleeSounds Sounds { get; private set; } = new();

        [field: SerializeField, Tooltip("Spawned where a swing lands; can be the bullet impact")]
        public PooledEffect ImpactEffect { get; private set; }

        public override ItemCategory Category => ItemCategory.Melee;
    }
}
