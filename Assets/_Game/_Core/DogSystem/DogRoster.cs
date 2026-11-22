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

        public DogRoster(DogSettings settings) => Settings = settings;

        public DogSettings Settings { get; }
        public bool Enabled => Settings != null && Settings.Enabled;
        public Dog Dog { get; private set; }
        public DogHouse House { get; private set; }
        public RobotYard Yard { get; private set; }
        // Spots it trots over to at home (the robots' spots, the gate, the house door…).
        public IReadOnlyList<Transform> Visits => _visits;
        public bool Night { get; set; }
        public bool Raid { get; set; }
        public bool HasPlayer { get; set; }
        public Vector3 PlayerPosition { get; set; }
        public Vector3 PlayerForward { get; set; }
        public Vector3 PlayerVelocity { get; set; }
        // Seconds the player has stood still (0 while moving).
        public float PlayerStillFor { get; set; }

        public void Bind(DogHouse house, RobotYard yard, IEnumerable<Transform> visits)
        {
            House = house;
            Yard = yard;
            _visits.Clear();
            if (visits != null)
                foreach (Transform visit in visits)
                    if (visit != null)
                        _visits.Add(visit);
        }

        public void Add(Dog dog) => Dog = dog;

        public void Remove(Dog dog)
        {
            if (Dog == dog)
                Dog = null;
        }

        public bool InYard(Vector3 point) => Yard != null && Yard.Contains(point);

        // Where "home" is: the house door, else where it started.
        public Vector3 Home(Dog dog) => House != null ? House.Door : dog.HomePosition;
    }
}
