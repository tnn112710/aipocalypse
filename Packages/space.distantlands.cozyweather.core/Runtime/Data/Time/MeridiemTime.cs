//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Denotes the meridiem of a MeridiemTime object (AM/PM)
    /// </summary>

    public enum Meridiem { AM, PM }
    /// <summary>
    /// Core time object for COZY
    /// </summary>
    [Serializable]
    public struct MeridiemTime : IComparable<MeridiemTime>, IEquatable<MeridiemTime>
    {
        private const long MillisecondsPerDay = 86400000;

        public int hours;         // Strictly 1-12
        public int minutes;       // Strictly 0-59
        public int seconds;       // Strictly 0-59
        public int milliseconds;  // Strictly 0-999
        public Meridiem meridiem;

        public enum Unit { Standard, Military }

        // Core Constructor: Everything routes through here to guarantee perfect balancing
        private MeridiemTime(long absoluteMs)
        {
            long ms = (absoluteMs % MillisecondsPerDay + MillisecondsPerDay) % MillisecondsPerDay;

            int totalSeconds = (int)(ms / 1000);
            milliseconds = (int)ms % 1000;
            seconds = totalSeconds % 60;

            int totalMinutes = totalSeconds / 60;
            minutes = totalMinutes % 60;

            int hour24 = totalMinutes / 60;
            meridiem = hour24 >= 12 ? Meridiem.PM : Meridiem.AM;

            int hour12 = hour24 % 12;
            hours = hour12 == 0 ? 12 : hour12;
        }

        // Public Constructors
        public MeridiemTime(int hour, int minute, Meridiem meridiem)
            : this(CalculateMs(hour, minute, 0, 0, meridiem)) { }

        public MeridiemTime(int hour, int minute, int second, int millisecond, Meridiem meridiem)
            : this(CalculateMs(hour, minute, second, millisecond, meridiem)) { }

        public static MeridiemTime From24Hour(int hour, int minute, int second = 0, int millisecond = 0)
        {
            return new MeridiemTime(((hour * 60 + minute) * 60 + second) * 1000 + millisecond);
        }

        public static MeridiemTime ToClamped(float value)
        {
            return new MeridiemTime((int)Mathf.Clamp(value * MillisecondsPerDay, 0f, MillisecondsPerDay - 1));
        }

        // Helper to convert 12-hour constructor inputs into raw MS before initialization
        private static long CalculateMs(int h, int m, int s, int ms, Meridiem mer)
        {
            int hour24 = (h % 12) + (mer == Meridiem.PM ? 12 : 0);
            return ((hour24 * 60 + m) * 60 + s) * 1000 + ms;
        }

        internal long ToTotalMilliseconds() => CalculateMs(hours, minutes, seconds, milliseconds, meridiem);

        public int AbsoluteHour => (hours % 12) + (meridiem == Meridiem.PM ? 12 : 0);
        public float AsPercentage() => ToTotalMilliseconds() / (float)MillisecondsPerDay;

        // Factory / Convenience Properties
        public MeridiemTime FloorToMinute => new(hours, minutes, Meridiem.AM); // Constructor handles actual balancing
        public MeridiemTime FloorToHour => new(hours, 0, Meridiem.AM);

        // Sun properties
        public static MeridiemTime Sunrise =>
            CozyWeather.Instance && CozyWeather.Instance.GetModule(out CozyTransitModule module)
            ? module.SunriseTime : new MeridiemTime(6, 0, Meridiem.AM);

        public static MeridiemTime Sunset =>
            CozyWeather.Instance && CozyWeather.Instance.GetModule(out CozyTransitModule module)
            ? module.SunsetTime : new MeridiemTime(6, 0, Meridiem.PM);

        // Formatting
        /// <summary>
        /// Gets the current time as a string in this format: h:mm AM/PM
        /// </summary>
        public string StandardTime() => $"{hours}:{minutes:D2} {meridiem}";
        // Formatting
        /// <summary>
        /// Gets the current time as a string in this format: h:mm:ss:mmm AM/PM
        /// </summary>
        public string FullString() => $"{hours}:{minutes:D2}:{seconds:D2}:{milliseconds:D3} {meridiem}";
        /// <summary>
        /// Gets the current time (as a 24 hour time) as a string in this format: h:mm
        /// </summary>
        public string MilitaryTime() => $"{AbsoluteHour:D2}:{minutes:D2}";
        /// <summary>
        /// Gets the current time (as a 24 hour time) as a string in this format: h:mm:ss:mmm
        /// </summary>
        public string FullMilitaryTime() => $"{AbsoluteHour:D2}:{minutes:D2}:{seconds:D2}:{milliseconds:D3}";

        public override string ToString() => CozyPreferences.TimeUnit == Unit.Standard ? StandardTime() : MilitaryTime();

        public static implicit operator MeridiemTime(float value) => new((int)(value * MillisecondsPerDay));
        public static implicit operator MeridiemTime(double value) => new((int)(value * MillisecondsPerDay));
        public static implicit operator float(MeridiemTime time) => time.AsPercentage();
        public static implicit operator double(MeridiemTime time) => time.AsPercentage();
        public static implicit operator string(MeridiemTime time) => time.ToString();

        public static implicit operator DateTime(MeridiemTime time) =>
            new(1, 1, 1, time.AbsoluteHour, time.minutes, time.seconds, time.milliseconds);

        public static MeridiemTime operator +(MeridiemTime t1, MeridiemTimeDelta t2) => new(t1.ToTotalMilliseconds() + t2.ToTotalMilliseconds());
        public static MeridiemTime operator -(MeridiemTime t1, MeridiemTimeDelta t2) => new(t1.ToTotalMilliseconds() - t2.ToTotalMilliseconds());
        public static MeridiemTimeDelta operator -(MeridiemTime t1, MeridiemTime t2) => MeridiemTimeDelta.FromTotalMilliseconds(t1.ToTotalMilliseconds() - t2.ToTotalMilliseconds());

        public static bool operator <(MeridiemTime t1, MeridiemTime t2) => t1.ToTotalMilliseconds() < t2.ToTotalMilliseconds();
        public static bool operator >(MeridiemTime t1, MeridiemTime t2) => t1.ToTotalMilliseconds() > t2.ToTotalMilliseconds();
        public static bool operator ==(MeridiemTime t1, MeridiemTime t2) => t1.ToTotalMilliseconds() == t2.ToTotalMilliseconds();
        public static bool operator !=(MeridiemTime t1, MeridiemTime t2) => !(t1 == t2);

        public int CompareTo(MeridiemTime other) => ToTotalMilliseconds().CompareTo(other.ToTotalMilliseconds());
        public override bool Equals(object obj) => obj is MeridiemTime other && Equals(other);
        public bool Equals(MeridiemTime other) => this == other;
        public override int GetHashCode() => ToTotalMilliseconds().GetHashCode();

        public static int WrapInt(int t, int length, bool allowZero)
        {
            if (length <= 0)
                throw new ArgumentException("Length must be greater than zero.");

            int wrapped = t % length;

            if (wrapped < 0)
                wrapped += length;

            if (!allowZero && wrapped == 0)
                return length;

            return wrapped;
        }

        public static MeridiemTime Noon => 0.5f;
        public static MeridiemTime Midnight => 0;
        public static MeridiemTime SixPM => new(6, 00, Meridiem.PM);
        public static MeridiemTime SixAM => new(6, 00, Meridiem.AM);
    }
}