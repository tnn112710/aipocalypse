namespace MB13.Aipocalypse.Core.EnemySystem
{
    // A* pathfinding tags (AstarPath's tag names). RobotsOnly marks ground only robots walk: the house, and the link through
    // its door. Every other walker leaves it out.
    public static class NavTags
    {
        public const int RobotsOnly = 1;
        public const int All = -1;
        public const int Walkers = ~(1 << RobotsOnly);

        public static bool Allows(int traversableTags, uint tag) => ((traversableTags >> (int)tag) & 1) != 0;
    }
}
