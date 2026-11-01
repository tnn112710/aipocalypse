using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.SpawnSystem
{
    // When a SpawnArea is awake and how many it brings in: pure rules for PopulationDirector and its tests.
    public static class PopulationRules
    {
        // Wakes inside `activate` metres, sleeps past `deactivate` (the gap keeps it from flickering at the edge).
        public static bool IsActive(bool wasActive, float distance, float activate, float deactivate) =>
            distance <= (wasActive ? Mathf.Max(activate, deactivate) : activate);

        // Members to request now: what's missing from `capacity` (alive and pending, plus killed ones still waiting to come
        // back), at most a group and within the category's and the global room.
        public static int ToSpawn(int capacity, int alive, int respawning, int groupSize, int categoryRoom, int globalRoom)
        {
            int missing = capacity - alive - respawning;
            if (groupSize > 0)
                missing = Mathf.Min(missing, groupSize);
            return Mathf.Max(0, Mathf.Min(missing, Mathf.Min(categoryRoom, globalRoom)));
        }

        // Drops the respawn times that are due by `now`.
        public static void ReleaseDue(List<float> respawnAt, float now)
        {
            for (int i = respawnAt.Count - 1; i >= 0; i--)
                if (respawnAt[i] <= now)
                    respawnAt.RemoveAt(i);
        }
    }
}
