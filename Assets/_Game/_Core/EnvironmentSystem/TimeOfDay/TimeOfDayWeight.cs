namespace MB13.Aipocalypse.Core.EnvironmentSystem
{
    // A time-of-day weight for other code to read (the player's light in the dark). Weight is -1 until the director first ticks it.
    public sealed class TimeOfDayWeight : TimeOfDayReactor
    {
        protected override void Apply(float weight) { }
    }
}
