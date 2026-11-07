using System.Collections.Generic;

namespace DistantLands.Cozy.Blocks
{
    /// <summary>
    /// Defines a block of time with a start time, end time, name, and profiles that occur during that time.
    /// </summary>
    public struct TimeBlock
    {
        public List<BlocksConditions> profileConditions;
        public MeridiemTime startTime;
        public MeridiemTime endTime;
        public string name;
    }
}