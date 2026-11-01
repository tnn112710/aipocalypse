using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.SpawnSystem
{
    // Budgets and defaults of a scene's spawning: how fast instances come into play, where prewarmed ones wake, population caps.
    [CreateAssetMenu(fileName = "SpawnSystemSettings", menuName = "Aipocalypse/Spawning/Spawn System Settings")]
    public sealed class SpawnSystemSettings : ScriptableObject
    {
        [Header("Budget")]
        [Min(0.1f), Tooltip("Milliseconds a frame spent bringing queued instances into play, at least one a frame. A pooled goblin " +
            "costs about 1 ms (editor), a prop far less")]
        public float SpawnBudgetMs = 2f;
        [Min(1), Tooltip("Pooled instances created and woken once per frame while prewarming, so filling a pool doesn't hitch")]
        public int PrewarmPerFrame = 2;
        [Tooltip("Where prewarmed instances wake for a frame (Awake, Start) before they're parked: out of sight and off the navmesh")]
        public Vector3 StagingPoint = new(0f, -1000f, 0f);

        [Header("Groups")]
        [Tooltip("Metres round its anchor a group spreads over when its request names no formation: a random radius in this range " +
            "(widened for big groups)")]
        public Vector2 GroupRadius = new(3f, 5f);
        [Min(0.5f), Tooltip("Metres kept between neighbours of a group without a formation")] public float GroupSpacing = 1.2f;
        [Min(0f), Tooltip("Seconds between two tries of a request still looking for its spot")] public float RetryInterval = 0.25f;

        [Header("Catalogue")]
        [Tooltip("Kinds the console spawns by name (spawn.card)")] public List<SpawnCard> Cards = new();

        [Header("Rules")]
        [Tooltip("For requests that bring none (console, plain spawns)")] public SpawnRules DefaultRules = new();

        [Header("Population (SpawnArea)")]
        [Min(0.05f), Tooltip("Seconds between two looks of the PopulationDirector at its areas")] public float PopulationInterval = 0.5f;
        [Min(0), Tooltip("Most instances alive at once from all sources, past which areas stop topping up")] public int GlobalCap = 300;
        [SerializeField, Tooltip("Most alive at once per category, past which areas stop topping up that category; none = no cap")]
        Dictionary<SpawnCategory, int> _categoryCaps = new()
        {
            { SpawnCategory.Enemy, 60 }, { SpawnCategory.Animal, 30 }, { SpawnCategory.Bird, 8 }, { SpawnCategory.Fish, 8 },
            { SpawnCategory.Item, 200 }, { SpawnCategory.Prop, 500 },
        };

        public IReadOnlyDictionary<SpawnCategory, int> CategoryCaps => _categoryCaps;

        public int CapOf(SpawnCategory category) => _categoryCaps.TryGetValue(category, out int cap) ? cap : int.MaxValue;
    }
}
