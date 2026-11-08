using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.EnemySystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RaidSystem
{
    // One night's raid: its waves in order. Read-only at runtime.
    [CreateAssetMenu(fileName = "Night", menuName = "Aipocalypse/Raids/Raid Plan")]
    public sealed class RaidPlan : ScriptableObject
    {
        public List<RaidWave> Waves = new();

        public int TotalEnemies
        {
            get
            {
                int total = 0;
                foreach (RaidWave wave in Waves)
                    total += wave.Count;
                return total;
            }
        }
    }

    // A wave: what spawns (in groups of each card's group size, GroupInterval apart, from Zone) and when the next wave may
    // start once this one has fully spawned: as soon as at most NextWhenAliveAtMost raiders are alive and MinBreather has
    // passed, or MaxWait after the wave started, whichever comes first.
    [Serializable]
    public sealed class RaidWave
    {
        public List<RaidWaveEntry> Entries = new();
        [Min(0f), Tooltip("Seconds between two groups of this wave")] public float GroupInterval = 4f;
        [Tooltip("Id of the SpawnZone its groups come from")] public string Zone = "Front";
        [Min(0), Tooltip("The next wave may start once this many raiders or fewer are alive")] public int NextWhenAliveAtMost = 2;
        [Min(0f), Tooltip("Seconds after this wave fully spawned before the next may start early")] public float MinBreather = 15f;
        [Min(0f), Tooltip("Seconds after this wave started when the next starts anyway (once this one has fully spawned)")]
        public float MaxWait = 90f;

        public int Count
        {
            get
            {
                int count = 0;
                foreach (RaidWaveEntry entry in Entries)
                    count += Mathf.Max(0, entry.Count);
                return count;
            }
        }
    }

    [Serializable]
    public struct RaidWaveEntry
    {
        public EnemySpawnCard Card;
        [Min(0)] public int Count;

        public RaidWaveEntry(EnemySpawnCard card, int count)
        {
            Card = card;
            Count = count;
        }
    }
}
