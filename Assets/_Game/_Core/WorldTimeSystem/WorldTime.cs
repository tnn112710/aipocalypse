using System;

namespace Aipocalypse.Core.WorldTimeSystem
{
    public enum DayPhase
    {
        Dawn,
        Morning,
        Afternoon,
        Dusk,
        Evening,
        Midnight,
    }

    // A moment on the world clock: hours since day 1, 00:00. Day, hour and minute are derived, so saving it is one number.
    public readonly struct WorldTime : IEquatable<WorldTime>
    {
        public const double HoursPerDay = 24d;

        public readonly double TotalHours;

        public WorldTime(double totalHours) => TotalHours = totalHours;

        public static WorldTime At(int day, float hour) => new((day - 1) * HoursPerDay + hour);

        // 1-based day number.
        public int Day => (int)Math.Floor(TotalHours / HoursPerDay) + 1;

        // 0 (midnight) to just under 24. Clamped: a moment within float precision of midnight would otherwise round up to 24.
        public float Hour => Math.Min((float)(TotalHours - Math.Floor(TotalHours / HoursPerDay) * HoursPerDay), LastHour);

        public int WholeHour => (int)(TotalMinutes / 60 % 24);
        public int Minute => (int)(TotalMinutes % 60);

        // The largest float below 24.
        const float LastHour = 23.999998f;

        long TotalMinutes => (long)Math.Floor(TotalHours * 60d);

        public WorldTime AddHours(double hours) => new(TotalHours + hours);

        public bool Equals(WorldTime other) => TotalHours.Equals(other.TotalHours);
        public override bool Equals(object obj) => obj is WorldTime other && Equals(other);
        public override int GetHashCode() => TotalHours.GetHashCode();
        public override string ToString() => $"Day {Day} {WholeHour:00}:{Minute:00}";
    }
}
