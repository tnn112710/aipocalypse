using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Aipocalypse.Core.WorldTimeSystem
{
    [Serializable]
    public struct PhaseTiming
    {
        [Range(0f, 24f), Tooltip("Hour this phase starts; it runs until the next phase starts")]
        public float Start;
        [Min(0.01f), Tooltip("Clock speed during this phase, on top of Master Speed. 2 = this phase passes twice as fast")]
        public float Speed;

        public PhaseTiming(float start, float speed = 1f)
        {
            Start = start;
            Speed = speed;
        }
    }

    // How fast the world clock runs and where its phases fall. Play-mode edits apply immediately.
    [CreateAssetMenu(menuName = "Aipocalypse/World Time/Clock Settings", fileName = "ClockSettings")]
    public sealed class ClockSettings : ScriptableObject
    {
        [BoxGroup("Speed"), Min(0.1f), Tooltip("Real seconds for one in-game hour at speed 1")]
        public float SecondsPerGameHour = 30f;
        [BoxGroup("Speed"), Min(0f), Tooltip("Multiplies every phase. 2 = the whole day runs twice as fast")]
        public float MasterSpeed = 1f;

        [BoxGroup("Start"), Min(1)] public int StartDay = 1;
        [BoxGroup("Start"), Range(0f, 24f)] public float StartHour = 7f;

        [BoxGroup("Phases")] public PhaseTiming Dawn = new(6f);
        [BoxGroup("Phases"), FormerlySerializedAs("Day")] public PhaseTiming Morning = new(8f);
        [BoxGroup("Phases")] public PhaseTiming Afternoon = new(12f);
        [BoxGroup("Phases")] public PhaseTiming Dusk = new(16f);
        [BoxGroup("Phases"), FormerlySerializedAs("Night")] public PhaseTiming Evening = new(18f);
        [BoxGroup("Phases")] public PhaseTiming Midnight = new(0f);

        [BoxGroup("Moon"), Min(1), Tooltip("Days from one full moon to the next")]
        public int MoonCycleDays = 8;
        [BoxGroup("Moon"), Min(1)] public int FirstFullMoonDay = 1;

        static readonly DayPhase[] Order = { DayPhase.Dawn, DayPhase.Morning, DayPhase.Afternoon, DayPhase.Dusk, DayPhase.Evening, DayPhase.Midnight };

        public PhaseTiming Timing(DayPhase phase) => phase switch
        {
            DayPhase.Dawn => Dawn,
            DayPhase.Morning => Morning,
            DayPhase.Afternoon => Afternoon,
            DayPhase.Dusk => Dusk,
            DayPhase.Evening => Evening,
            _ => Midnight,
        };

        public static DayPhase Next(DayPhase phase) => Order[((int)phase + 1) % Order.Length];

        // The phase whose start is the latest one at or before the hour, wrapping past midnight.
        public DayPhase PhaseAt(float hour)
        {
            DayPhase best = DayPhase.Midnight;
            float bestStart = float.NegativeInfinity;
            DayPhase latest = DayPhase.Midnight;
            float latestStart = float.NegativeInfinity;
            foreach (DayPhase phase in Order)
            {
                float start = Timing(phase).Start;
                if (start <= hour && start > bestStart)
                {
                    best = phase;
                    bestStart = start;
                }
                if (start > latestStart)
                {
                    latest = phase;
                    latestStart = start;
                }
            }
            return float.IsNegativeInfinity(bestStart) ? latest : best;
        }

        // Hours from the given hour until a phase next starts (0 when it starts exactly now).
        public double HoursUntilStart(double hour, DayPhase phase)
        {
            double delta = Timing(phase).Start - hour;
            return delta < 0d ? delta + WorldTime.HoursPerDay : delta;
        }

        // Phases always follow Dawn → Morning → Afternoon → Dusk → Evening → Midnight; their starts must keep that order around the clock.
        public bool StartsInOrder()
        {
            int wraps = 0;
            foreach (DayPhase phase in Order)
                if (Timing(Next(phase)).Start <= Timing(phase).Start)
                    wraps++;
            return wraps <= 1;
        }

        // In-game hours per real second during a phase.
        public float HoursPerSecond(DayPhase phase) => MasterSpeed * Timing(phase).Speed / SecondsPerGameHour;

        public double MoonPhaseAt(WorldTime time)
        {
            double days = time.TotalHours / WorldTime.HoursPerDay - (FirstFullMoonDay - 1);
            double phase = 0.5d + days / MoonCycleDays;
            return phase - Math.Floor(phase);
        }

        // Real seconds the clock needs to run from one moment to a later one, at a given extra speed scale.
        public double RealSecondsBetween(WorldTime from, WorldTime to, float scale = 1f)
        {
            if (scale <= 0f || to.TotalHours <= from.TotalHours)
                return to.TotalHours <= from.TotalHours ? 0d : double.PositiveInfinity;

            double seconds = 0d;
            double now = from.TotalHours;
            DayPhase phase = PhaseAt(new WorldTime(now).Hour);
            for (int guard = 0; now < to.TotalHours && guard < 10000; guard++)
            {
                double hour = now - Math.Floor(now / WorldTime.HoursPerDay) * WorldTime.HoursPerDay;
                double step = Math.Min(HoursUntilStart(hour, Next(phase)), to.TotalHours - now);
                float rate = HoursPerSecond(phase) * scale;
                if (rate <= 0f)
                    return double.PositiveInfinity;
                seconds += step / rate;
                now += step;
                if (now < to.TotalHours)
                    phase = Next(phase);
            }
            return seconds;
        }

        public double RealSecondsPerDay => RealSecondsBetween(new WorldTime(0d), new WorldTime(WorldTime.HoursPerDay));

        [ShowInInspector, ReadOnly, BoxGroup("Real time"), HideLabel, MultiLineProperty(9)]
        string RealTimeSummary
        {
            get
            {
                double day = RealSecondsPerDay;
                var text = new System.Text.StringBuilder();
                text.AppendLine($"Full day: {day / 60d:0.##} real minutes ({day:0} s)");
                text.AppendLine($"1 game minute = {SecondsPerGameHour / 60f / Mathf.Max(MasterSpeed, 0.0001f):0.###} s at speed 1");
                foreach (DayPhase phase in Order)
                {
                    float start = Timing(phase).Start;
                    double length = HoursUntilStart(start, Next(phase));
                    double seconds = length / HoursPerSecond(phase);
                    text.AppendLine($"{phase}: {Clock(start)}–{Clock(Timing(Next(phase)).Start)} ({length:0.##} h) = {seconds / 60d:0.##} min");
                }
                if (!StartsInOrder())
                    text.AppendLine("Phase starts are out of order: keep Dawn → Morning → Afternoon → Dusk → Evening → Midnight around the clock");
                return text.ToString().TrimEnd();
            }
        }

        static string Clock(float hour) => $"{(int)hour:00}:{(int)Math.Round((hour - (int)hour) * 60f) % 60:00}";

        void OnValidate()
        {
            if (!StartsInOrder())
                Debug.LogWarning($"{name}: phase starts must follow Dawn → Morning → Afternoon → Dusk → Evening → Midnight around the clock", this);
        }
    }
}
