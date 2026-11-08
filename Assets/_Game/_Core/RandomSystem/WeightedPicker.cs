using System;
using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RandomSystem
{
    // Picks from a WeightedTable with its NoRepeat and Units rules, keeping that state per user (one picker per chest,
    // per enemy kind...). Reads the table on every pick, so Inspector edits apply in Play. GC-free after the first pick.
    public sealed class WeightedPicker<T>
    {
        readonly WeightedTable<T> _table;
        readonly RandomStream _random;
        readonly WeightedDraw _draw = new();
        float[] _weights = Array.Empty<float>();
        int[] _units = Array.Empty<int>();

        public WeightedPicker(WeightedTable<T> table, RandomStream random)
        {
            _table = table;
            _random = random;
        }

        public WeightedTable<T> Table => _table;

        // Units left of entry `index`; -1 when unlimited.
        public int LeftOf(int index) => _draw.LeftOf(index);

        public bool TryPick(out T value)
        {
            Read();
            int index = _draw.Next(_weights, _units, _table.NothingWeight, _table.NoRepeat, _random.Value);
            value = index >= 0 ? _table.Entries[index].Value : default;
            return index >= 0;
        }

        // Rolls the table's Count, then picks that many, adding the non-nothing ones to `results`. Returns how many were added.
        public int Roll(List<T> results)
        {
            int count = Mathf.Max(0, _random.RangeInclusive(_table.Count));
            int added = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TryPick(out T value))
                    continue;
                results.Add(value);
                added++;
            }
            return added;
        }

        // Every entry back to its full units.
        public void Refill()
        {
            Read();
            _draw.Refill(_units);
        }

        void Read()
        {
            int count = _table.Entries.Count;
            if (_weights.Length != count)
            {
                _weights = new float[count];
                _units = new int[count];
            }
            for (int i = 0; i < count; i++)
            {
                _weights[i] = _table.WeightOf(i);
                _units[i] = _table.Entries[i].Units;
            }
        }
    }
}
