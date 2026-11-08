using MB13.Aipocalypse.Core.Effects;
using MB13.Aipocalypse.Core.EquipmentSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // Everything that makes one melee weapon swing the way it does. The pose markers it swings between are on its held prefab.
    [CreateAssetMenu(fileName = "Melee", menuName = "Aipocalypse/Items/Melee")]
    public sealed class MeleeDefinition : EquippableDefinition
    {
        [SerializeField, FoldoutGroup("Swing", true), HideLabel, InlineProperty]
        SwingSettings _swing = new();

        [SerializeField, FoldoutGroup("Sounds"), HideLabel, InlineProperty]
        MeleeSounds _sounds = new();

        [SerializeField, Tooltip("Spawned where a swing lands; can be the bullet impact")]
        PooledEffect _impactEffect;

        [SerializeField, Min(0), Tooltip("Impact copies made when the weapon is bound")]
        int _impactPrewarm = 4;

        public SwingSettings Swing => _swing;
        public MeleeSounds Sounds => _sounds;
        public PooledEffect ImpactEffect => _impactEffect;
        public int ImpactPrewarm => _impactPrewarm;

        public override ItemCategory Category => ItemCategory.Melee;
    }
}
