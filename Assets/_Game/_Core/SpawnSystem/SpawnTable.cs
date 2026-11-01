using System;
using System.Collections.Generic;
using Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;

namespace Aipocalypse.Core.SpawnSystem
{
    // What may spawn somewhere, by weight: each entry's card when its day phase, day range and alive cap allow it, or nothing
    // at all (NothingWeight). A request from a table picks one card for its whole group.
    [CreateAssetMenu(fileName = "Table", menuName = "Aipocalypse/Spawning/Spawn Table")]
    public sealed class SpawnTable : ScriptableObject
    {
        public List<SpawnTableEntry> Entries = new();
        [Min(0f), Tooltip("Relative weight of picking nothing")] public float NothingWeight;

        // A card allowed by `conditions` and `counts` by weight, `roll` in [0, 1); null for nothing (or none allowed).
        public SpawnCard Pick(in SpawnConditions conditions, ISpawnCounts counts, float roll)
        {
            float total = NothingWeight;
            foreach (SpawnTableEntry entry in Entries)
                if (entry.Allows(conditions, counts))
                    total += entry.Weight;
            if (total <= 0f)
                return null;
            float pick = Mathf.Clamp01(roll) * total;
            SpawnCard last = null;
            foreach (SpawnTableEntry entry in Entries)
            {
                if (!entry.Allows(conditions, counts))
                    continue;
                last = entry.Card;
                pick -= entry.Weight;
                if (pick < 0f)
                    return entry.Card;
            }
            // Past every entry: the nothing share, or a roll of 1 with none.
            return NothingWeight > 0f ? null : last;
        }
    }

    [Serializable]
    public sealed class SpawnTableEntry
    {
        public SpawnCard Card;
        [Min(0f)] public float Weight = 1f;
        [Tooltip("Day phases it may spawn in; empty = any")] public List<DayPhase> Phases = new();
        [Tooltip("First and last day it may spawn on (inclusive); 0 = no limit on that side")] public Vector2Int Days;
        [Min(0), Tooltip("Most alive at once, from every source; 0 = no limit")] public int MaxAlive;

        public bool Allows(in SpawnConditions conditions, ISpawnCounts counts)
        {
            if (Card == null || Weight <= 0f)
                return false;
            if (conditions.HasPhase && Phases.Count > 0 && !Phases.Contains(conditions.Phase))
                return false;
            if (conditions.Day > 0 && ((Days.x > 0 && conditions.Day < Days.x) || (Days.y > 0 && conditions.Day > Days.y)))
                return false;
            return MaxAlive <= 0 || counts == null || counts.CountAlive(Card) < MaxAlive;
        }
    }

    // When a table is picked from: the world clock's phase and day (unknown ones don't filter).
    public readonly struct SpawnConditions
    {
        public readonly bool HasPhase;
        public readonly DayPhase Phase;
        // Day number from 1; 0 = unknown.
        public readonly int Day;

        public SpawnConditions(DayPhase phase, int day)
        {
            HasPhase = true;
            Phase = phase;
            Day = day;
        }

        public static SpawnConditions Any => default;

        // Now on `clock`; Any without one.
        public static SpawnConditions Of(WorldClock clock) => clock != null ? new SpawnConditions(clock.Phase, clock.Now.Day) : Any;
    }

    // How many are alive, for alive caps (SpawnService).
    public interface ISpawnCounts
    {
        int CountAlive(SpawnCard card);
        int CountAlive(SpawnCategory category);
    }
}
