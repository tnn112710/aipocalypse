using System;
using MB13.Aipocalypse.Core.WorldTimeSystem;

namespace MB13.Aipocalypse.Core.SleepSystem
{
    // When a sleep may end: whole clock hours from Earliest to Latest, or why not at all.
    public readonly struct SleepWindow
    {
        public readonly bool CanSleep;
        public readonly string Reason;
        public readonly WorldTime Earliest;
        public readonly WorldTime Latest;

        public SleepWindow(WorldTime earliest, WorldTime latest)
        {
            CanSleep = true;
            Reason = null;
            Earliest = earliest;
            Latest = latest;
        }

        public SleepWindow(string reason)
        {
            CanSleep = false;
            Reason = reason;
            Earliest = Latest = default;
        }

        public int Choices => CanSleep ? (int)Math.Round(Latest.TotalHours - Earliest.TotalHours) + 1 : 0;
    }

    // The raids a sleep must not skip: the night `now` falls in (0 by day) and whether a night still has a raid to run.
    public readonly struct SleepRaids
    {
        public readonly bool Running;
        public readonly float StartHour;
        public readonly float DawnHour;
        public readonly Func<int, bool> IsDue;

        public SleepRaids(bool running, float startHour, float dawnHour, Func<int, bool> isDue)
        {
            Running = running;
            StartHour = startHour;
            DawnHour = dawnHour;
            IsDue = isDue;
        }
    }

    public static class SleepRules
    {
        public const string RaidRunning = "Can't sleep during a raid";
        public const string RaidTonight = "Can't sleep: the raid is coming";
        public const string RaidTooClose = "Too close to tonight's raid to sleep";

        // Days ahead searched for the next raid (longer than any sleep).
        const int DaysAhead = 3;
        const double Epsilon = 1e-6;

        // `raids` null = nothing limits the sleep (a scene without raids).
        public static SleepWindow Window(WorldTime now, int minHours, int maxHours, float wakeBeforeRaid, SleepRaids? raids)
        {
            double earliest = Math.Ceiling(now.TotalHours + minHours - Epsilon);
            double latest = Math.Floor(now.TotalHours + maxHours + Epsilon);

            if (raids is SleepRaids r)
            {
                if (r.Running)
                    return new SleepWindow(RaidRunning);
                // Inside a night whose raid hasn't run: it starts any moment.
                int night = NightOf(now, r);
                if (night > 0 && r.IsDue(night))
                    return new SleepWindow(RaidTonight);

                // By day and before dawn the next raid night is today's; in the evening (tonight's is over), tomorrow's.
                int first = now.Hour < r.StartHour ? now.Day : now.Day + 1;
                for (int day = first; day < first + DaysAhead; day++)
                {
                    if (!r.IsDue(day))
                        continue;
                    double raidStart = WorldTime.At(day, r.StartHour).TotalHours;
                    latest = Math.Min(latest, Math.Floor(raidStart - wakeBeforeRaid + Epsilon));
                    break;
                }
                if (latest < earliest)
                    return new SleepWindow(RaidTooClose);
            }

            return new SleepWindow(new WorldTime(earliest), new WorldTime(Math.Max(earliest, latest)));
        }

        // The wake-up time offered first: the next `defaultHour` when it's inside the window, else the latest.
        public static WorldTime DefaultWake(in SleepWindow window, float defaultHour)
        {
            for (double t = window.Earliest.TotalHours; t <= window.Latest.TotalHours + Epsilon; t += 1d)
                if (Math.Abs(new WorldTime(t).Hour - defaultHour) < 0.01f)
                    return new WorldTime(t);
            return window.Latest;
        }

        // Same nights as RaidDirector.NightOf: the day a night started on, 0 by day.
        static int NightOf(WorldTime time, in SleepRaids raids)
        {
            float hour = time.Hour;
            if (hour >= raids.StartHour)
                return time.Day;
            return hour < raids.DawnHour ? time.Day - 1 : 0;
        }
    }
}
