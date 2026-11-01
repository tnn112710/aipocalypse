using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.SpawnSystem
{
    // One spawnable kind of anything (an animal, a flock, a coin, a tree): its prefab variants and how often each is picked,
    // how many come together, where it can stand and what it counts as. Kinds with more data derive from it (EnemySpawnCard).
    [CreateAssetMenu(fileName = "Card", menuName = "Aipocalypse/Spawning/Spawn Card")]
    public class SpawnCard : ScriptableObject
    {
        [SerializeField, Tooltip("Prefab variants of this kind and their relative weight")]
        Dictionary<Spawnable, float> _variants = new();
        [Tooltip("Instances per group, min and max (inclusive)")] public Vector2Int GroupSize = new(1, 1);
        [Tooltip("What it counts as for population caps")] public SpawnCategory Category;
        public SpawnPlacement Placement = new();

        public IReadOnlyDictionary<Spawnable, float> Variants => _variants;

        // `roll` in [0, 1); null when there's no variant with a weight.
        public Spawnable PickVariant(float roll)
        {
            float total = 0f;
            foreach ((Spawnable prefab, float weight) in _variants)
                if (prefab != null && weight > 0f)
                    total += weight;
            if (total <= 0f)
                return null;
            float pick = Mathf.Clamp01(roll) * total;
            Spawnable last = null;
            foreach ((Spawnable prefab, float weight) in _variants)
            {
                if (prefab == null || weight <= 0f)
                    continue;
                last = prefab;
                pick -= weight;
                if (pick < 0f)
                    return prefab;
            }
            return last;
        }

        public int PickGroupSize(float roll)
        {
            int min = Mathf.Max(1, GroupSize.x);
            int max = Mathf.Max(min, GroupSize.y);
            return Mathf.Min(max, min + (int)(Mathf.Clamp01(roll) * (max - min + 1)));
        }

#if UNITY_EDITOR
        // Setup scripts and tests only.
        public void SetVariant(Spawnable prefab, float weight) => _variants[prefab] = weight;
        public void ClearVariants() => _variants.Clear();
#endif
    }
}
