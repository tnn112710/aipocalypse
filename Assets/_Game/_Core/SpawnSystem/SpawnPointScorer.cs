using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.SpawnSystem
{
    // A sampled spawn point as the player sees it.
    public readonly struct SpawnCandidate
    {
        public readonly Vector3 Position;
        public readonly float PlayerDistance;
        // In the player's view and line of sight.
        public readonly bool Seen;
        // Degrees between where the player looks and the point.
        public readonly float ViewAngle;

        public SpawnCandidate(Vector3 position, float playerDistance, bool seen, float viewAngle)
        {
            Position = position;
            PlayerDistance = playerDistance;
            Seen = seen;
            ViewAngle = viewAngle;
        }
    }

    // Chooses a spawn point, never closer than MinPlayerDistance: a hidden one at random (`roll` in [0, 1)); when none is
    // hidden and visible ones are allowed (the wait is over), the one farthest from the view centre. -1 when none fits yet.
    public static class SpawnPointScorer
    {
        public static int Choose(IReadOnlyList<SpawnCandidate> candidates, SpawnRules rules, bool allowVisible, float roll)
        {
            int hidden = 0;
            for (int i = 0; i < candidates.Count; i++)
                if (Allowed(candidates[i], rules) && !candidates[i].Seen)
                    hidden++;
            if (hidden > 0)
            {
                int pick = Mathf.Min(hidden - 1, (int)(Mathf.Clamp01(roll) * hidden));
                for (int i = 0; i < candidates.Count; i++)
                    if (Allowed(candidates[i], rules) && !candidates[i].Seen && pick-- == 0)
                        return i;
            }
            if (!allowVisible)
                return -1;

            int best = -1;
            for (int i = 0; i < candidates.Count; i++)
                if (Allowed(candidates[i], rules) && (best < 0 || candidates[i].ViewAngle > candidates[best].ViewAngle))
                    best = i;
            return best;
        }

        static bool Allowed(SpawnCandidate candidate, SpawnRules rules) => candidate.PlayerDistance >= rules.MinPlayerDistance;
    }
}
