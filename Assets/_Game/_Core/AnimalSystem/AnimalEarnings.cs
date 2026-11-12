using System;
using MB13.Aipocalypse.Core.WorldTimeSystem;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // The Tokens one animal holds until the player collects them: earned over its waking hours at its day rate, never past
    // its cap.
    public sealed class AnimalEarnings
    {
        public float Held { get; private set; }
        // What a collection hands over.
        public int Whole => (int)Held;

        // `awakeHours` waking hours passed, of a waking day `dayLength` hours long that earns `perDay`.
        public void Earn(float awakeHours, float dayLength, float perDay, float cap)
        {
            if (awakeHours <= 0f || dayLength <= 0f || Held >= cap)
                return;
            Held = Math.Min(cap, Held + perDay * awakeHours / dayLength);
        }

        // Takes the whole Tokens out; the fraction keeps earning.
        public int Collect()
        {
            int whole = Whole;
            Held -= whole;
            return whole;
        }

        public void Set(float held) => Held = Math.Max(0f, held);

        // Hours of [from, to] (world-clock hours) that fall inside the waking window, which may wrap past midnight.
        public static double AwakeHours(double from, double to, TimeWindow awake)
        {
            if (to <= from)
                return 0d;
            double length = DayLength(awake);
            if (length >= WorldTime.HoursPerDay)
                return to - from;
            // Windows start at On on each day; the one that began the day before `from` may still reach into the span.
            double first = Math.Floor(from / WorldTime.HoursPerDay) - 1d;
            double last = Math.Floor(to / WorldTime.HoursPerDay);
            double total = 0d;
            for (double day = first; day <= last; day++)
            {
                double start = day * WorldTime.HoursPerDay + awake.On;
                double overlap = Math.Min(to, start + length) - Math.Max(from, start);
                if (overlap > 0d)
                    total += overlap;
            }
            return total;
        }

        // Length of the waking day in hours (24 when On and Off are equal).
        public static float DayLength(TimeWindow awake)
        {
            float length = awake.Off - awake.On;
            return length <= 0f ? length + (float)WorldTime.HoursPerDay : length;
        }
    }
}
