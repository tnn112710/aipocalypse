using System;

namespace MB13.Aipocalypse.Core.Stats
{
    // One change to one stat in a data row (a need's tier): value = (base + Add) × Multiply.
    [Serializable]
    public struct StatChange
    {
        public StatId Stat;
        public float Add;
        public float Multiply;

        public StatChange(StatId stat, float add = 0f, float multiply = 1f)
        {
            Stat = stat;
            Add = add;
            Multiply = multiply;
        }
    }
}
