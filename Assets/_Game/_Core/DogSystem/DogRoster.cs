using System.Collections.Generic;
using MB13.Aipocalypse.Core.RobotSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // Scene service: the dog in play, its house, the yard, the places it likes to visit at home, and what DogManager last
    // saw of the world (night, a raid, the player) for its behaviour tasks.
    public sealed class DogRoster
    {
        readonly List<Transform> _visits = new();
        readonly List<DogLinkInfo> _crossings = new();
        readonly List<(Vector3 Near, Vector3 Far, bool Standing)> _ways = new();

        public DogRoster(DogSettings settings) => Settings = settings;

        public DogSettings Settings { get; }
        public bool Enabled => Settings != null && Settings.Enabled;
        public Dog Dog { get; private set; }
        public DogHouse House { get; private set; }
        public RobotYard Yard { get; private set; }
        // Spots it trots over to at home (the robots' spots, the gate, the house door…).
        public IReadOnlyList<Transform> Visits => _visits;
        // Fence crossings the dog scrambles over (DogScramble).
        public IReadOnlyList<DogLinkInfo> Crossings => _crossings;
        public bool PlayerAiming { get; set; }
        public bool Night { get; set; }
        public bool Raid { get; set; }
        public bool HasPlayer { get; set; }
        public Vector3 PlayerPosition { get; set; }
        public Vector3 PlayerForward { get; set; }
        public Vector3 PlayerVelocity { get; set; }
        // Seconds the player has stood still (0 while moving).
        public float PlayerStillFor { get; set; }

        public void Bind(DogHouse house, RobotYard yard, IEnumerable<Transform> visits, IEnumerable<DogLinkInfo> crossings = null)
        {
            House = house;
            Yard = yard;
            _visits.Clear();
            if (visits != null)
                foreach (Transform visit in visits)
                    if (visit != null)
                        _visits.Add(visit);
            _crossings.Clear();
            if (crossings != null)
                foreach (DogLinkInfo crossing in crossings)
                    if (crossing != null)
                        _crossings.Add(crossing);
        }

        public void Add(Dog dog) => Dog = dog;

        public void Remove(Dog dog)
        {
            if (Dog == dog)
                Dog = null;
        }

        public bool InYard(Vector3 point) => Yard != null && Yard.Contains(point);

        // The fence crossing to scramble over from `from` to `to`, or null: nothing standing, or (`canWalk`: there's a path)
        // a gap no more than Settings.GapDetour m longer way round, walked through instead.
        public DogLinkInfo ScrambleCrossing(Vector3 from, Vector3 to, bool canWalk)
        {
            bool inside = InYard(from);
            _ways.Clear();
            foreach (DogLinkInfo crossing in _crossings)
                _ways.Add(inside ? (crossing.Inside, crossing.Outside, crossing.Standing) : (crossing.Outside, crossing.Inside, crossing.Standing));
            int index = ScrambleOver(from, to, _ways, canWalk, Settings != null ? Settings.GapDetour : 0f);
            return index >= 0 ? _crossings[index] : null;
        }

        // Index of the shortest way over a standing fence (near end, far end), or -1: none, or (`canWalk`) a gap at most
        // `gapDetour` m longer.
        public static int ScrambleOver(Vector3 from, Vector3 to, IReadOnlyList<(Vector3 Near, Vector3 Far, bool Standing)> ways,
            bool canWalk, float gapDetour)
        {
            int best = -1;
            float bestCost = float.MaxValue, gapCost = float.MaxValue;
            for (int i = 0; i < ways.Count; i++)
            {
                float cost = FlatMath.Length(ways[i].Near - from) + FlatMath.Length(to - ways[i].Far);
                if (!ways[i].Standing)
                    gapCost = Mathf.Min(gapCost, cost);
                else if (cost < bestCost)
                {
                    bestCost = cost;
                    best = i;
                }
            }
            return best >= 0 && canWalk && gapCost <= bestCost + gapDetour ? -1 : best;
        }

        // Where "home" is: the house door, else where it started.
        public Vector3 Home(Dog dog) => House != null ? House.Door : dog.HomePosition;
    }
}
