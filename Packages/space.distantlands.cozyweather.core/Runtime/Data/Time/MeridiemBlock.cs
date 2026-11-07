//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Define a block or section of time in a day. Corresponds directly to MeridiemBlockType
    /// </summary>
    [Serializable]
    public struct MeridiemBlock
    {
        /// <summary>
        /// The start time of the block. Think of this as the point that the block will start to impact the weather at minimum intensity
        /// </summary>
        public MeridiemTime startTime;
        /// <summary>
        /// The end time for the block transition. Think of this as the point that the block will impact the weather at maximum intensity. This is <i>not</i> the time that the block finishes playing
        /// </summary>
        public MeridiemTime endTime;
        public MeridiemBlock(MeridiemTime startTime, MeridiemTime endTime)
        {
            this.startTime = startTime;
            this.endTime = endTime;
        }

        public static bool operator ==(MeridiemBlock a, MeridiemBlock b)
        {
            return a.startTime == b.startTime && a.endTime == b.endTime;
        }
        public static bool operator !=(MeridiemBlock a, MeridiemBlock b)
        {
            return a.startTime != b.startTime || a.endTime != b.endTime;
        }
        public override readonly bool Equals(object o)
        {
            if (o is MeridiemBlock other)
            {
                return this == other;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return startTime.GetHashCode() ^ endTime.GetHashCode();
        }

        public override readonly string ToString()
        {
            return ((MeridiemBlockType)this).ToString();
        }

        public static implicit operator MeridiemBlockType(MeridiemBlock block)
        {
            if (block == CozyPreferences.DawnTimes)
            {
                return MeridiemBlockType.Dawn;
            }
            if (block == CozyPreferences.MorningTimes)
            {
                return MeridiemBlockType.Morning;
            }
            if (block == CozyPreferences.DayTimes)
            {
                return MeridiemBlockType.Day;
            }
            if (block == CozyPreferences.AfternoonTimes)
            {
                return MeridiemBlockType.Afternoon;
            }
            if (block == CozyPreferences.EveningTimes)
            {
                return MeridiemBlockType.Evening;
            }
            if (block == CozyPreferences.TwilightTimes)
            {
                return MeridiemBlockType.Twilight;
            }
            if (block == CozyPreferences.NightTimes)
            {
                return MeridiemBlockType.Night;
            }

            return MeridiemBlockType.None;
        }
        public static implicit operator MeridiemBlock(MeridiemBlockType block)
        {
            return block switch
            {
                MeridiemBlockType.Dawn => CozyPreferences.DawnTimes,
                MeridiemBlockType.Morning => CozyPreferences.MorningTimes,
                MeridiemBlockType.Day => CozyPreferences.DayTimes,
                MeridiemBlockType.Afternoon => CozyPreferences.AfternoonTimes,
                MeridiemBlockType.Evening => CozyPreferences.EveningTimes,
                MeridiemBlockType.Twilight => CozyPreferences.TwilightTimes,
                MeridiemBlockType.Night => CozyPreferences.NightTimes,
                _ => new MeridiemBlock(),
            };
        }
    }

    /// <summary>
    /// Enum describing specific times of day using fuzzy times (morning, afternoon, evening, etc.). Corresponds directly to MeridiemBlock
    /// </summary>
    public enum MeridiemBlockType
    {
        /// <summary>
        /// The time for the first block of the day. Should be from when the sun starts to impact the horizon up to the point where the sun is just below the horizon.
        /// </summary>
        Dawn,
        /// <summary>
        /// The time for the second block of the day. Should be from when the sun is just below the horizon to when the sun is just above the horizon.
        /// </summary>
        Morning,
        /// <summary>
        /// The time for the third block of the day. Should be from when the sun is just above the horizon to when the sun is directly overhead.
        /// </summary>
        Day,
        /// <summary>
        /// The time for the fourth block of the day. Should be from when the sun is directly overhead to when the sun is just starting to set.
        /// </summary>
        Afternoon,
        /// <summary>
        /// The time for the fifth block of the day. Should be from when the sun is just starting to set to when the sun is on the horizon line.
        /// </summary>
        Evening,
        /// <summary>
        /// The time for the sixth block of the day. Should be from when the sun is on the horizon line to when the sun is below the horizon line.
        /// </summary>
        Twilight,
        /// <summary>
        /// The time for the seventh and last block of the day. Should be from when the sun is below the horizon line to when the dawn block starts.
        /// </summary>
        Night,
        None = -1
    }

}