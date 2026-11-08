using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RandomSystem
{
    // A seeded random number sequence (SplitMix64): the same seed always gives the same rolls. GC-free.
    // Get one per system from RandomService.Stream so systems don't shift each other's rolls.
    public sealed class RandomStream
    {
        const float FloatUnit = 1f / (1 << 24);

        ulong _state;

        public RandomStream(ulong seed) => _state = seed;

        public void Reset(ulong seed) => _state = seed;

        public ulong NextULong()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // [0, 1).
        public float Value => (NextULong() >> 40) * FloatUnit;

        // [min, max).
        public float Range(float min, float max) => min + (max - min) * Value;

        // [min, maxExclusive), like UnityEngine.Random.Range(int, int); min when the range is empty.
        public int Range(int min, int maxExclusive) =>
            maxExclusive <= min ? min : min + (int)(NextULong() % (ulong)((long)maxExclusive - min));

        // x to y, both inclusive (count ranges such as group sizes).
        public int RangeInclusive(Vector2Int range) => Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y) + 1);

        // True with `probability` (0–1).
        public bool Chance(float probability) => Value < probability;

        public float Sign => Value < 0.5f ? -1f : 1f;

        public Vector2 InsideUnitCircle
        {
            get
            {
                float angle = Value * 2f * Mathf.PI;
                float radius = Mathf.Sqrt(Value);
                return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }
        }

        public Vector3 OnUnitSphere
        {
            get
            {
                float z = Range(-1f, 1f);
                float angle = Value * 2f * Mathf.PI;
                float ring = Mathf.Sqrt(1f - z * z);
                return new Vector3(Mathf.Cos(angle) * ring, Mathf.Sin(angle) * ring, z);
            }
        }

        public Vector3 InsideUnitSphere => OnUnitSphere * Mathf.Pow(Value, 1f / 3f);

        // A uniformly random item; default when empty.
        public T Pick<T>(IReadOnlyList<T> items) => items == null || items.Count == 0 ? default : items[Range(0, items.Count)];

        // Fisher–Yates, in place.
        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
