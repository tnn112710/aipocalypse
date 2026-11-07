//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Specifies a duration of time using meridiem time architecture.
    /// </summary>
    [Serializable]
    public struct MeridiemTimeDelta : IEquatable<MeridiemTimeDelta>
    {
        private const float MillisecondsPerDay = 86400000f;

        public int hours;
        public int minutes;
        public int seconds;
        public int milliseconds;

        public MeridiemTimeDelta(int hour, int minute, int second = 0, int millisecond = 0)
        {
            // Convert to long to prevent overflow during intermediate calculations
            long totalMs = ((hour * 60L + minute) * 60L + second) * 1000L + millisecond;
            
            this = FromTotalMilliseconds(totalMs);
        }

        private MeridiemTimeDelta(int h, int m, int s, int ms, bool isBalanced)
        {
            hours = h;
            minutes = m;
            seconds = s;
            milliseconds = ms;
        }

        internal long ToTotalMilliseconds()
        {
            return ((hours * 60L + minutes) * 60L + seconds) * 1000L + milliseconds;
        }

        internal static MeridiemTimeDelta FromTotalMilliseconds(long ms)
        {
            long totalSeconds = ms / 1000;
            int outMs = (int)(ms % 1000);
            int outSec = (int)(totalSeconds % 60);
            
            long totalMinutes = totalSeconds / 60;
            int outMin = (int)(totalMinutes % 60);
            int outHour = (int)(totalMinutes / 60);
            
            return new MeridiemTimeDelta(outHour, outMin, outSec, outMs, true);
        }

        // Implicit Conversions
        public static implicit operator MeridiemTimeDelta(float days) => FromTotalMilliseconds((long)(days * MillisecondsPerDay));
        public static implicit operator MeridiemTimeDelta(double days) => FromTotalMilliseconds((long)(days * MillisecondsPerDay));
        public static implicit operator float(MeridiemTimeDelta time) => time.ToTotalMilliseconds() / MillisecondsPerDay;
        public static implicit operator double(MeridiemTimeDelta time) => time.ToTotalMilliseconds() / (double)MillisecondsPerDay;
        public static implicit operator string(MeridiemTimeDelta time) => time.ToString();

        // Math Operators
        public static MeridiemTimeDelta operator +(MeridiemTimeDelta t1, MeridiemTimeDelta t2) => FromTotalMilliseconds(t1.ToTotalMilliseconds() + t2.ToTotalMilliseconds());
        public static MeridiemTimeDelta operator -(MeridiemTimeDelta t1, MeridiemTimeDelta t2) => FromTotalMilliseconds(t1.ToTotalMilliseconds() - t2.ToTotalMilliseconds());
        
        // Equality
        public static bool operator ==(MeridiemTimeDelta t1, MeridiemTimeDelta t2) => t1.ToTotalMilliseconds() == t2.ToTotalMilliseconds();
        public static bool operator !=(MeridiemTimeDelta t1, MeridiemTimeDelta t2) => !(t1 == t2);
        public static bool operator <(MeridiemTimeDelta t1, MeridiemTimeDelta t2) => t1.ToTotalMilliseconds() < t2.ToTotalMilliseconds();
        public static bool operator >(MeridiemTimeDelta t1, MeridiemTimeDelta t2) => t1.ToTotalMilliseconds() > t2.ToTotalMilliseconds();

        public override bool Equals(object obj) => obj is MeridiemTimeDelta other && Equals(other);
        public bool Equals(MeridiemTimeDelta other) => this == other;

        public override int GetHashCode() => ToTotalMilliseconds().GetHashCode();

        // Formatting
        public override string ToString() => $"{hours:D2}:{minutes:D2}:{seconds:D2}:{milliseconds:D3}";
        
        // Convenience
        public MeridiemTimeDelta FloorToMinute => new(hours, minutes);
        public MeridiemTimeDelta FloorToHour => new(hours, 0);
    }
}