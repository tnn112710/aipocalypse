//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Lets you define an event based on the calendar
    /// </summary>
    public interface IMeridiemEvent
    {
        public MeridiemDate StartDate { get; }
        public MeridiemDate EndDate { get; }
        public MeridiemTime StartTime { get; }
        public MeridiemTime EndTime { get; }
        public MeridiemTimeDelta Duration { get; }
        public int DurationInDays { get; }
        public bool Overnight { get; }
        public bool AllDay { get; }
        public bool DateRange { get; }
        public MeridiemEventRepeatStyle RepeatStyle { get; set; }


        public string EventName { get; }
        public string EventDescription { get; }
        public MeridiemEventType EventType { get; }

        public bool IsEventRunning { get; set; }
        public Action OnEventStart { get; set; }
        public Action OnEventRunning { get; set; }
        public Action OnEventEnd { get; set; }


        public void UpdateEvent(bool running)
        {
            if (IsEventRunning)
            {
                if (!running)
                {
                    OnEventEnd?.Invoke();
                }
                else
                    OnEventRunning?.Invoke();
            }
            else
            {
                if (running)
                {
                    OnEventStart?.Invoke();
                }
            }

            IsEventRunning = running;

        }

        /// <summary>
        /// Returns the closest start and end date offsets based on an event and a date. For example, if an event occurs every January 1st, startDateOffset and endDateOffset are the number of days from date till the event.
        /// If start date offset is negative or 0 and end date offset is positive or 0, we can assume that the event occurs on the current day.
        /// </summary>
        public void GetRelativeStartDate(MeridiemDate date, out int startDateOffset, out int endDateOffset)
        {
            MeridiemEventRepeatStyle repeatStyle = RepeatStyle ?? new RepeatNever();
            repeatStyle.GetRelativeStartDate(date, this, out startDateOffset, out endDateOffset);
        }

        public bool DoesEventHappenToday(MeridiemDate date)
        {
            GetRelativeStartDate(date, out int startDate, out int endDate);
            return startDate <= 0 && endDate >= 0;
        }

        public bool DoesEventHappenAllDay(MeridiemDate date)
        {
            GetRelativeStartDate(date, out int startDate, out int endDate);
            return (startDate < 0 && endDate > 0) || (startDate <= 0 && endDate >= 0 && AllDay);
        }

    }

    public enum MeridiemEventType { None, Weather, Holiday, Status, WorldEvent, Player, NPC, Enemy, Ambient }
}