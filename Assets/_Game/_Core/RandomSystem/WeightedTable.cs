using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.RandomSystem
{
    // A serializable weighted table (loot, drops, choices): Count picks per roll, each one entry by weight or nothing
    // (NothingWeight). Use as a field, e.g. [SerializeField] WeightedTable<ItemDefinition> _loot.
    [Serializable]
    public sealed class WeightedTable<T>
    {
        public List<WeightedEntry<T>> Entries = new();
        [Min(0f), Tooltip("Relative weight of picking nothing")] public float NothingWeight;
        [Tooltip("How many picks per roll, min and max inclusive")] public Vector2Int Count = Vector2Int.one;

        // One pick from a roll in [0, 1); false for nothing.
        public bool TryPick(float roll, out T value)
        {
            int index = WeightedPick.Index(Entries, static e => IsSet(e.Value) ? e.Weight : 0f, roll, NothingWeight);
            value = index >= 0 ? Entries[index].Value : default;
            return index >= 0;
        }

        public bool TryPick(RandomStream random, out T value) => TryPick(random.Value, out value);

        // Rolls Count, then picks that many (with repeats), adding the non-nothing ones to `results`. Returns how many were added.
        public int Roll(RandomStream random, List<T> results)
        {
            int count = Mathf.Max(0, random.RangeInclusive(Count));
            int added = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TryPick(random, out T value))
                    continue;
                results.Add(value);
                added++;
            }
            return added;
        }

        // Unity's null check for assets (missing references are fake-null objects).
        static bool IsSet(T value) => value is UnityEngine.Object asset ? asset != null : value != null;
    }

    [Serializable]
    public sealed class WeightedEntry<T>
    {
        public T Value;
        [Min(0f)] public float Weight = 1f;
    }
}
