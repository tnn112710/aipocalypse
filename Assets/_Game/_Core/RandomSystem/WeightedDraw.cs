using System;

namespace MB13.Aipocalypse.Core.RandomSystem
{
    // Per-user pick state over a table's weights: no repeats (the last entry sits out, unless it's the only one left)
    // and units (an entry with units > 0 runs out after that many picks; when nothing can be picked, every entry refills,
    // so units 19 + 1 is a deck of 20). One per user of a table, never on the shared table asset.
    public sealed class WeightedDraw
    {
        int[] _left = Array.Empty<int>();
        float[] _effective = Array.Empty<float>();

        public int Last { get; private set; } = -1;

        // Units left of entry `index`; -1 for an unlimited entry or before the first pick.
        public int LeftOf(int index) => index < _left.Length ? _left[index] : -1;

        public void Refill(ReadOnlySpan<int> units)
        {
            if (_left.Length != units.Length)
                _left = new int[units.Length];
            for (int i = 0; i < units.Length; i++)
                _left[i] = units[i] > 0 ? units[i] : -1;
        }

        // `weights` and `units` per entry (same length); -1 for nothing. A nothing pick keeps Last.
        public int Next(ReadOnlySpan<float> weights, ReadOnlySpan<int> units, float nothingWeight, bool noRepeat, float roll)
        {
            int count = weights.Length;
            if (_left.Length != count)
                Refill(units);
            if (_effective.Length != count)
                _effective = new float[count];

            int pickable = Fill(weights);
            if (pickable == 0 && HasLimited(units))
            {
                Refill(units);
                pickable = Fill(weights);
            }
            int skip = noRepeat && Last >= 0 && Last < count && _effective[Last] > 0f && pickable > 1 ? Last : -1;
            if (skip >= 0)
                _effective[skip] = 0f;

            int index = WeightedPick.Index(_effective, roll, nothingWeight);
            if (index < 0)
                return -1;
            if (_left[index] > 0)
                _left[index]--;
            Last = index;
            return index;
        }

        int Fill(ReadOnlySpan<float> weights)
        {
            int pickable = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                bool open = weights[i] > 0f && _left[i] != 0;
                _effective[i] = open ? weights[i] : 0f;
                if (open)
                    pickable++;
            }
            return pickable;
        }

        static bool HasLimited(ReadOnlySpan<int> units)
        {
            foreach (int unit in units)
                if (unit > 0)
                    return true;
            return false;
        }
    }
}
