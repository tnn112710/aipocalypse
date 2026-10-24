using Aipocalypse.Core.EquipmentSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // Everything that makes one melee weapon swing the way it does. The pose markers it swings between are on its held prefab.
    [CreateAssetMenu(fileName = "Melee", menuName = "Aipocalypse/Items/Melee")]
    public sealed class MeleeDefinition : EquippableDefinition
    {
        [field: SerializeField, FoldoutGroup("Swing", true), HideLabel, InlineProperty]
        public SwingSettings Swing { get; private set; } = new();

        [field: SerializeField, FoldoutGroup("Sounds"), HideLabel, InlineProperty]
        public MeleeSounds Sounds { get; private set; } = new();

        [field: SerializeField, Tooltip("Spawned where a swing lands; can be the bullet impact")]
        public PooledEffect ImpactEffect { get; private set; }

        [field: SerializeField, Min(0), Tooltip("Impact copies made when the weapon is bound")]
        public int ImpactPrewarm { get; private set; } = 4;

        public override ItemCategory Category => ItemCategory.Melee;
    }
}
