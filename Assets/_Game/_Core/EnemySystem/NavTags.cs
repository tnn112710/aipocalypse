namespace MB13.Aipocalypse.Core.EnemySystem
{
    // A* pathfinding tags (AstarPath's tag names). RobotsOnly marks ground only robots walk: the house, and the link through
    // its door. Every other walker leaves it out, except the dog, which walks every tag.
    public static class NavTags
    {
        public const int RobotsOnly = 1;
        // The dog's links over the fence: no one else plans through them.
        public const int DogOnly = 2;
        public const int All = -1;
        public const int Walkers = ~((1 << RobotsOnly) | (1 << DogOnly));
        public const int Robots = ~(1 << DogOnly);

        public static bool Allows(int traversableTags, uint tag) => ((traversableTags >> (int)tag) & 1) != 0;
    }
}
