//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Interface for all calendar events modules. Plays and stores meridiem events.
    /// </summary>
    public interface ICalendarEventsModule : ICozyModule
    {
        /// <summary>
        /// Collection of events added at runtime to the calendar system
        /// </summary>
        public List<RuntimeEvent> RuntimeEvents { get; set; }
        /// <summary>
        /// Collection of static events added in the editor to the calendar system
        /// </summary>
        public List<StaticEvent> StaticEvents { get; set; }

        /// <summary>
        /// All events that occur today.
        /// </summary>
        /// <returns>
        /// A list of tuples containing:
        /// <list type="bullet">
        /// <item><description><b>eventReference</b>: a reference to the IMeridiemEvent.</description></item>
        /// <item><description><b>allDay</b>: a boolean detailing if the event happens all day on this day.</description></item>
        /// <item><description><b>startTime</b>: Determines the time of day that this event starts today.</description></item>
        /// <item><description><b>endTime</b>: Determines the time of day that this event ends today.</description></item>
        /// </list>
        /// If the start time is after the end time, the event occurs overnight between midnight and the end time <b>and</b> between the start time and midnight.
        /// </returns>
        public List<(IMeridiemEvent eventReference, bool allDay, MeridiemTime startTime, MeridiemTime endTime)> EventsToday { get; set; }

        /// <summary>
        /// Add a new runtime event to the calendar
        /// </summary>
        public void AddEvent(RuntimeEvent meridiemEvent);
        /// <summary>
        /// Remove an existing runtime event from the calendar
        /// </summary>
        public void RemoveEvent(RuntimeEvent meridiemEvent);

        /// <summary>
        /// Get a list of all events that occur on a specific day
        /// </summary>
        public List<IMeridiemEvent> GetEventsOnDay(MeridiemDate date);


    }
}