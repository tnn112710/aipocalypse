using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // The scene's farm animals and what they were told (HerdOrders): animals add themselves while enabled; the pen gate,
    // the bell and the behaviour tasks read and change it.
    public sealed class AnimalHerd
    {
        readonly List<Animal> _animals = new(16);

        public AnimalHerd(AnimalSettings settings) => Settings = settings;

        public AnimalSettings Settings { get; }
        public HerdOrders Orders { get; } = new();
        public IReadOnlyList<Animal> Animals => _animals;
        public AnimalPen Pen { get; private set; }
        public AnimalPasture Pasture { get; private set; }
        // Outside the waking hours (set by AnimalManager).
        public bool Asleep { get; set; }
        public bool Enabled => Settings.Enabled && Pen != null;

        public void Bind(AnimalPen pen, AnimalPasture pasture)
        {
            Pen = pen;
            Pasture = pasture;
        }

        public void Add(Animal animal)
        {
            if (!_animals.Contains(animal))
                _animals.Add(animal);
        }

        public void Remove(Animal animal) => _animals.Remove(animal);

        // The first of a kind: the one its flock grazes round.
        public Animal LeaderOf(AnimalDefinition definition)
        {
            foreach (Animal animal in _animals)
                if (animal.Definition == definition)
                    return animal;
            return null;
        }

        public int CountHome()
        {
            int home = 0;
            foreach (Animal animal in _animals)
                if (animal.IsHome)
                    home++;
            return home;
        }

        public bool AnyNear(Vector3 point, float radius)
        {
            foreach (Animal animal in _animals)
                if ((animal.transform.position - point).sqrMagnitude <= radius * radius)
                    return true;
            return false;
        }

        // Hurries `from`, every animal within HurryRadius of it, and the whole flock of any kind that follows a leader.
        public void Hurry(Animal from)
        {
            float radius = Settings.HurryRadius;
            Vector3 center = from.transform.position;
            foreach (Animal animal in _animals)
                if (animal == from || (animal.transform.position - center).sqrMagnitude <= radius * radius)
                    animal.Hurry();
            foreach (Animal animal in _animals)
            {
                if (animal.Hurrying || !animal.Definition.FollowsLeader)
                    continue;
                foreach (Animal other in _animals)
                {
                    if (!other.Hurrying || other.Definition != animal.Definition)
                        continue;
                    animal.Hurry();
                    break;
                }
            }
        }
    }
}
