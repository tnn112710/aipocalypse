using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.RandomSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // What something drops: a weighted table of items with an amount range each (an enemy kind's body). Rolled with the
    // "loot" RandomService stream.
    [CreateAssetMenu(fileName = "LootTable", menuName = "Aipocalypse/Items/Loot Table")]
    public sealed class LootTable : ScriptableObject
    {
        [SerializeField, Tooltip("Count = how many picks per roll; each pick is one entry or nothing (NothingWeight)")]
        WeightedTable<LootDrop> _drops = new();

        readonly List<LootDrop> _picks = new();

        // Adds the rolled items to `results`, a stack per pick (same items merge when they go into a container).
        public void Roll(RandomStream random, List<ItemInstance> results)
        {
            _picks.Clear();
            _drops.Roll(random, _picks);
            foreach (LootDrop drop in _picks)
            {
                if (drop.Item == null)
                    continue;
                int count = Mathf.Max(1, random.RangeInclusive(drop.Count));
                while (count > 0)
                {
                    int size = Math.Min(count, drop.Item.MaxStack);
                    results.Add(drop.Item.CreateInstance(size));
                    count -= size;
                }
            }
        }

#if UNITY_EDITOR
        // Setup scripts and tests only: runtime code never writes definitions.
        public WeightedTable<LootDrop> Drops => _drops;
#endif
    }

    [Serializable]
    public sealed class LootDrop
    {
        public ItemDefinition Item;
        [Tooltip("Amount, min and max inclusive")] public Vector2Int Count = Vector2Int.one;
    }
}
