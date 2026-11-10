using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Worn in the Backpack gear slot: adds backpack slots and carry weight (Inventory).
    [CreateAssetMenu(fileName = "Backpack", menuName = "Aipocalypse/Items/Backpack")]
    public sealed class BackpackDefinition : ItemDefinition
    {
        [SerializeField, Min(0), Tooltip("Backpack slots it adds while worn")] int _extraSlots = 15;
        [SerializeField, Min(0f), Tooltip("Kilograms of carry weight it adds while worn")] float _extraWeight = 15f;

        public int ExtraSlots => _extraSlots;
        public float ExtraWeight => _extraWeight;

        public override ItemCategory Category => ItemCategory.Backpack;
    }
}
