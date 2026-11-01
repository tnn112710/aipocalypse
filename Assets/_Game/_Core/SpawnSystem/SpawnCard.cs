using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.SpawnSystem
{
    public enum SpawnCapRule
    {
        // New ones aren't spawned (members of a group are dropped).
        Refuse,
        // The oldest one the player can't see goes back to the pool to make room (the oldest of all when every one is seen).
        RecycleOldest,
    }

    // One spawnable kind of anything (an animal, a flock, a coin, a tree): its prefab variants and how often each is picked,
    // how many come together, where it can stand and what it counts as. Kinds with more data derive from it (EnemySpawnCard).
    [CreateAssetMenu(fileName = "Card", menuName = "Aipocalypse/Spawning/Spawn Card")]
    public class SpawnCard : ScriptableObject
    {
        [SerializeField, Tooltip("Prefab variants of this kind and their relative weight")]
        Dictionary<Spawnable, float> _variants = new();
        [InfoBox("$" + nameof(Problem), InfoMessageType.Error, nameof(HasProblem))]
        [Tooltip("Instances per group, min and max (inclusive)")] public Vector2Int GroupSize = new(1, 1);
        [Tooltip("What it counts as for population caps")] public SpawnCategory Category;
        public SpawnPlacement Placement = new();
        [Min(0), Tooltip("Most of this card alive at once from every source (areas, drops, console); 0 = no limit")]
        public int MaxAlive;
        [Tooltip("At MaxAlive: refuse new ones, or recycle the oldest out of the player's sight (coins, drops)")]
        public SpawnCapRule WhenFull;

        public IReadOnlyDictionary<Spawnable, float> Variants => _variants;

        // What's wrong with the card's setup, or null: a variant reference that doesn't resolve (a prefab whose Spawnable
        // isn't saved on disk), no weight, or a flyer or swimmer without a band.
        public string Problem
        {
            get
            {
                float total = 0f;
                foreach ((Spawnable prefab, float weight) in _variants)
                {
                    if (prefab == null)
                        return "A variant is missing (its prefab or its Spawnable isn't saved).";
                    if (weight > 0f)
                        total += weight;
                }
                if (total <= 0f)
                    return "No variant with a weight: nothing can spawn.";
                if (Placement != null && Placement.Medium != SpawnMedium.Ground && Mathf.Max(Placement.Height.x, Placement.Height.y) <= 0f)
                    return $"{Placement.Medium} placement needs a height band.";
                return null;
            }
        }

        bool HasProblem => Problem != null;

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
