using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Money carried as an item (Tokens): a stack in the backpack, dropped and picked up like anything else.
    [CreateAssetMenu(fileName = "Currency", menuName = "Aipocalypse/Items/Currency")]
    public sealed class CurrencyDefinition : ItemDefinition
    {
        public override ItemCategory Category => ItemCategory.Currency;
    }
}
