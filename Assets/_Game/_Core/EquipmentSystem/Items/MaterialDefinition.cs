using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // A crafting or building material (wood): carried in the backpack, never held. Some burn as fuel in a campfire.
    [CreateAssetMenu(menuName = "Aipocalypse/Items/Material")]
    public sealed class MaterialDefinition : ItemDefinition
    {
        [SerializeField, Min(0f), Tooltip("Game hours of campfire fuel one unit gives (0 = doesn't burn)")] float _fuelHours;

        public float FuelHours => _fuelHours;
        public bool IsFuel => _fuelHours > 0f;

        public override ItemCategory Category => ItemCategory.Material;
    }
}
