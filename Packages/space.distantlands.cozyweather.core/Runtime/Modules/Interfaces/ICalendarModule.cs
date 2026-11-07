//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

namespace DistantLands.Cozy
{
    public delegate void ChangeDayEvent();
    public delegate void ChangeYearEvent();
    /// <summary>
    /// Interface for any module that controls the calendar
    /// </summary>
    public interface ICalendarModule : ICozyModule
    {
        /// <summary>
        /// The current date according to the calendar. Control the calendar for the year in the COZY Preferences
        /// </summary>
        public MeridiemDate CurrentDate { get; set; }

        /// <summary>
        /// Returns the index of the current day in the with the time added as a decimal. For example, 6 AM on March 3rd will return 62.25
        /// </summary>
        public float DayAndTime { get; }

        /// <summary>
        /// Should COZY automatically progress the day value at midnight? If false, days will repeat until otherwise specified
        /// </summary>
        public bool ProgressDay { get; set; }

        /// <summary>
        /// Event that runs when the current day changes
        /// </summary>
        public event ChangeDayEvent OnChangeDay;
        /// <summary>
        /// Event that runs when the current year changes
        /// </summary>
        public event ChangeYearEvent OnChangeYear;

    }
}