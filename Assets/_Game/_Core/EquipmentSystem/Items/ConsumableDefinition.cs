using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Carried in a quick slot (keys 3-9) and held in the hands when selected (medic box). Using it comes later.
    [CreateAssetMenu(fileName = "Consumable", menuName = "Aipocalypse/Items/Consumable")]
    public sealed class ConsumableDefinition : EquippableDefinition
    {
        public override ItemCategory Category => ItemCategory.Consumable;
    }
}
