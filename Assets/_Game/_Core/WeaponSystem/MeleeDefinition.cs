using Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    [CreateAssetMenu(fileName = "Melee", menuName = "Aipocalypse/Items/Melee")]
    public sealed class MeleeDefinition : EquippableDefinition
    {
        public override ItemCategory Category => ItemCategory.Melee;
    }
}
