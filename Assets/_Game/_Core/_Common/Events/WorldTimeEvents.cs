using Aipocalypse.Core.WorldTimeSystem;

namespace Aipocalypse.Core.Events
{
    // The clock passed a whole hour (once per hour crossed, also while skipping). Not sent for instant jumps: see TimeJumped.
    public readonly struct HourChanged
    {
        public readonly WorldTime Now;
        public HourChanged(WorldTime now) => Now = now;
    }

    // Dawn / Morning / Afternoon / Dusk / Evening / Midnight changed. Read WorldClock.Phase for the current one.
    public readonly struct DayPhaseChanged
    {
        public readonly DayPhase Previous;
        public readonly DayPhase Current;
        public readonly WorldTime Now;

        public DayPhaseChanged(DayPhase previous, DayPhase current, WorldTime now)
        {
            Previous = previous;
            Current = current;
            Now = now;
        }
    }

    public readonly struct DayStarted
    {
        public readonly int Day;
        public DayStarted(int day) => Day = day;
    }

    // The clock was set instantly (cheat, JumpTo, load). Hours in between were not played; resync instead of replaying them.
    public readonly struct TimeJumped
    {
        public readonly WorldTime From;
        public readonly WorldTime To;

        public TimeJumped(WorldTime from, WorldTime to)
        {
            From = from;
            To = to;
        }
    }

    // A SkipTo fast-forward reached its target.
    public readonly struct TimeSkipEnded
    {
        public readonly WorldTime Now;
        public TimeSkipEnded(WorldTime now) => Now = now;
    }
}
