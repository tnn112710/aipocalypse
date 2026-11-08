using System;
using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RandomSystem
{
    // Picks an index by relative weight (chance = weight / total) from a roll in [0, 1). Returns -1 for "nothing":
    // the roll landed in the nothing share, or no entry has weight. Weights ≤ 0 are never picked.
    // Pass static lambdas (and the context overload instead of captures) to stay GC-free.
    public static class WeightedPick
    {
        public static int Index(IReadOnlyList<float> weights, float roll, float nothingWeight = 0f) =>
            Index(weights, 0, static (w, _) => w, roll, nothingWeight);

        public static int Index<T>(IReadOnlyList<T> items, Func<T, float> weightOf, float roll, float nothingWeight = 0f) =>
            Index(items, weightOf, static (item, weigh) => weigh(item), roll, nothingWeight);

        // `weightOf` gets `context` too, e.g. conditions that rule entries out (weight 0).
        public static int Index<T, TContext>(IReadOnlyList<T> items, TContext context, Func<T, TContext, float> weightOf,
            float roll, float nothingWeight = 0f)
        {
            if (items == null)
                return -1;
            nothingWeight = Mathf.Max(0f, nothingWeight);
            float total = nothingWeight;
            for (int i = 0; i < items.Count; i++)
                total += Mathf.Max(0f, weightOf(items[i], context));
            if (total <= 0f)
                return -1;

            float pick = Mathf.Clamp01(roll) * total;
            int last = -1;
            for (int i = 0; i < items.Count; i++)
            {
                float weight = weightOf(items[i], context);
                if (weight <= 0f)
                    continue;
                last = i;
                pick -= weight;
                if (pick < 0f)
                    return i;
            }
            // Past every entry: the nothing share, or a roll of 1 with none.
            return nothingWeight > 0f ? -1 : last;
        }
    }
}
