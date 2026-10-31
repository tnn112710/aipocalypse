namespace Aipocalypse.Core.Events
{
    public enum RaidEndReason
    {
        // Every raider of the plan was spawned and killed.
        Cleared,
        // The night ran out; leftovers despawn out of sight.
        Dawn,
        // The player died during the raid.
        Defeat,
        // Ended by hand (console).
        Stopped,
    }

    // A raid night began (RaidDirector). `Night` is the day number the night starts on.
    public readonly struct RaidStarted
    {
        public readonly int Night;
        public RaidStarted(int night) => Night = night;
    }

    // A wave of the night's plan began spawning. `Wave` counts from 0; `Count` is its enemies.
    public readonly struct RaidWaveStarted
    {
        public readonly int Night;
        public readonly int Wave;
        public readonly int Count;

        public RaidWaveStarted(int night, int wave, int count)
        {
            Night = night;
            Wave = wave;
            Count = count;
        }
    }

    public readonly struct RaidEnded
    {
        public readonly int Night;
        public readonly RaidEndReason Reason;

        public RaidEnded(int night, RaidEndReason reason)
        {
            Night = night;
            Reason = reason;
        }
    }
}
