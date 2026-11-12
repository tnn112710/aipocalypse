using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // The scene's farm animals and what they were told (HerdOrders): animals add themselves while enabled; the pen gate,
    // the bell and the behaviour tasks read and change it. Each animal's own order (sent home) lives on the Animal.
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
        // The living player's feet, for animals that notice them (set by AnimalManager every frame).
        public bool HasPlayer { get; set; }
        public Vector3 PlayerPosition { get; set; }
        public bool Enabled => Settings.Enabled && Pen != null;

        public void Bind(AnimalPen pen, AnimalPasture pasture)
        {
            Pen = pen;
            Pasture = pasture;
        }

        // Kept in hierarchy order (animals wake in any order), so a flock's leader is the first of its kind placed: the rooster.
        public void Add(Animal animal)
        {
            if (_animals.Contains(animal))
                return;
            int index = _animals.Count;
            while (index > 0 && Before(animal.transform, _animals[index - 1].transform))
                index--;
            _animals.Insert(index, animal);
        }

        static bool Before(Transform a, Transform b) =>
            a.parent == b.parent ? a.GetSiblingIndex() < b.GetSiblingIndex() : string.CompareOrdinal(a.name, b.name) < 0;

        public void Remove(Animal animal) => _animals.Remove(animal);

        // The first of a kind not sent home: the one its flock grazes round.
        public Animal LeaderOf(AnimalDefinition definition)
        {
            foreach (Animal animal in _animals)
                if (animal.Definition == definition && !animal.SentHome)
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
    }
}
