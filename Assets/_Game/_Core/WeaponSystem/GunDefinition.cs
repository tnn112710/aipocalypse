using Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    [CreateAssetMenu(fileName = "Gun", menuName = "Aipocalypse/Items/Gun")]
    public sealed class GunDefinition : EquippableDefinition
    {
        public override ItemCategory Category => ItemCategory.Gun;
    }
}
