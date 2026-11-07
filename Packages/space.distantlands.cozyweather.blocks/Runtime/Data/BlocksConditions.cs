using System;

namespace DistantLands.Cozy.Blocks
{
    /// <summary>
    /// Stores a reference to a Blocks profile and chance information
    /// </summary>
    [Serializable]
    public struct BlocksConditions
    {
        public BlocksProfile blocksProfile;
        public WeightedRandomChance chance;
    }
}