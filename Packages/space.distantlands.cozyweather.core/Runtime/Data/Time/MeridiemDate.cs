//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Defines a date
    /// </summary>
    [Serializable]
    public struct MeridiemDate
    {
        public int year;
        public int month;
        public int day;

        public readonly int AbsoluteDay
        {
            get
            {
                int totalDays = 0;
                totalDays += year * CozyPreferences.DaysPerYear;

                // Loop from 0 to (month - 2) to add preceding months
                for (int m = 0; m < month - 1; m++)
                {
                    totalDays += CozyPreferences.Months[m].days;
                }

                totalDays += (day - 1);
                return totalDays;
            }
        }

        public readonly int AbsoluteDayInYear
        {
            get
            {
                int totalDays = 0;

                for (int m = 0; m < month - 1; m++)
                {
                    totalDays += CozyPreferences.Months[m].days;
                }

                totalDays += (day - 1);
                return totalDays;
            }
        }

        public readonly DayOfWeek Weekday => (DayOfWeek)Mathf.Repeat((int)(CozyPreferences.StartingWeekday + AbsoluteDay), 7);
        public readonly float YearPercentage => (float)AbsoluteDayInYear / CozyPreferences.DaysPerYear;

        public static bool operator <(MeridiemDate t1, MeridiemDate t2)
        {
            return t1.year < t2.year || (t1.year == t2.year && (t1.month < t2.month || (t1.month == t2.month && t1.day < t2.day)));
        }
        public static bool operator >(MeridiemDate t1, MeridiemDate t2)
        {
            return t1.year > t2.year || (t1.year == t2.year && (t1.month > t2.month || (t1.month == t2.month && t1.day > t2.day)));
        }
        public static bool operator ==(MeridiemDate t1, MeridiemDate t2)
        {
            return t1.year == t2.year && t1.month == t2.month && t1.day == t2.day;
        }
        public static bool operator !=(MeridiemDate t1, MeridiemDate t2)
        {
            return t1.year != t2.year || t1.month != t2.month || t1.day != t2.day;
        }

        /// <summary>
        /// Creates a new MeridiemDate struct. month and day start at 1.
        /// </summary>
        public MeridiemDate(int month, int day, int year)
        {
            this.month = month;
            this.day = day;
            this.year = year;
        }

        /// <summary>
        /// Creates a new MeridiemDate struct. month and day start at 1.
        /// </summary>
        public MeridiemDate(int month, int day)
        {
            this.month = month;
            this.day = day;
            this.year = 0;
        }

        public MeridiemDate(int absoluteDay)
        {
            year = 0;
            month = 1;
            day = 1;

            if (!CozyPreferences.Setup)
            {
                return;
            }

            year = absoluteDay / CozyPreferences.DaysPerYear;
            int daysLeft = absoluteDay % CozyPreferences.DaysPerYear;

            // Handle negative absolute days
            if (daysLeft < 0)
            {
                daysLeft += CozyPreferences.DaysPerYear;
                year--;
            }

            int m = 0;
            while (daysLeft >= CozyPreferences.Months[m].days)
            {
                daysLeft -= CozyPreferences.Months[m].days;
                m++;
            }

            month = m + 1;
            day = daysLeft + 1;
        }

        public static int operator -(MeridiemDate t1, MeridiemDate t2)
        {
            return Mathf.Abs(t1.AbsoluteDay - t2.AbsoluteDay);
        }
        public static MeridiemDate operator +(MeridiemDate t1, int t2) => new(t1.AbsoluteDay + t2);
        public static MeridiemDate operator -(MeridiemDate t1, int t2) => new(t1.AbsoluteDay - t2);

        public static implicit operator MeridiemDate(int t1) => new(t1);
        public static implicit operator int(MeridiemDate t1) => t1.AbsoluteDay;

        public bool IsWeekend => Weekday == DayOfWeek.Saturday || Weekday == DayOfWeek.Sunday;

        public override string ToString()
        {
            switch (CozyPreferences.NumericDateFormat)
            {
                case CozyPreferences.DateFormat.MDY:
                    return $"{month}/{day}/{year}";
                case CozyPreferences.DateFormat.DMY:
                    return $"{day}/{month}/{year}";
                case CozyPreferences.DateFormat.YMD:
                    return $"{year}/{month}/{day}";
                default:
                    return $"{day}/{month}/{year}";
            }
        }

        public readonly int DaysInMonth
        {
            get
            {
                if (month > CozyPreferences.MonthsPerYear || month <= 0)
                    return 0;

                return CozyPreferences.Months[month - 1].days;
            }
        }


        public readonly string MonthName
        {
            get
            {
                if (month > CozyPreferences.MonthsPerYear || month < 0)
                    return "Invalid Month";

                return CozyPreferences.Months[month - 1].name;
            }
        }

        public MeridiemDate ToBalanced()
        {
            // Normalize months (ensure month is between 1 and MonthsPerYear)
            while (month > CozyPreferences.MonthsPerYear)
            {
                month -= CozyPreferences.MonthsPerYear;
                year++;
            }
            while (month < 1)
            {
                month += CozyPreferences.MonthsPerYear;
                year--;
            }

            // Normalize days
            while (true)
            {
                int daysInCurrentMonth = CozyPreferences.Months[month - 1].days;
                if (day > daysInCurrentMonth)
                {
                    day -= daysInCurrentMonth;
                    month++;
                    if (month > CozyPreferences.MonthsPerYear)
                    {
                        month = 1;
                        year++;
                    }
                }
                else if (day < 1)
                {
                    month--;
                    if (month < 1)
                    {
                        month = CozyPreferences.MonthsPerYear;
                        year--;
                    }
                    day += CozyPreferences.Months[month - 1].days;
                }
                else
                {
                    break;
                }
            }

            return this;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is MeridiemDate)) return false;
            return this == (MeridiemDate)obj;
        }

        public override int GetHashCode() => HashCode.Combine(year, month, day);

        public MeridiemDate Yearless => new(month, day);
    }
    public class DayOfYearAttribute : PropertyAttribute { }
}