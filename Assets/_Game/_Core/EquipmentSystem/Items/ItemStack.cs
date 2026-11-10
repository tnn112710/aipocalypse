using System;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // An item and an amount, for authored lists (starting items, a crate's contents).
    [Serializable]
    public struct ItemStack
    {
        public ItemDefinition Item;
        [Min(1)] public int Count;

        public ItemStack(ItemDefinition item, int count)
        {
            Item = item;
            Count = count;
        }
    }
}
