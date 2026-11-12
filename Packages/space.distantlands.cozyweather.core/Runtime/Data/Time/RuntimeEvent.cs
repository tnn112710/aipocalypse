//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine;

namespace DistantLands.Cozy
{


    [Serializable]
    public class RuntimeEvent : IMeridiemEvent
    {
        public MeridiemDate StartDate => RepeatStyle.LimitTo24Hour ? 0 : startDate;
        public MeridiemDate EndDate => (dateRange ? endDate : StartDate) + (StartTime > EndTime ? 1 : 0);
        public MeridiemTime StartTime => allDay ? 0 : startTime;
        public MeridiemTime EndTime => allDay ? 1 : endTime;
        public bool AllDay => allDay;
        public bool DateRange => dateRange;
        public MeridiemEventRepeatStyle RepeatStyle { get { return repeatStyle ?? new RepeatNever(); } set { repeatStyle = value; } }
        public bool Overnight => StartDate < EndDate;
        public MeridiemTimeDelta Duration => EndTime > StartTime ? EndTime - StartTime : StartTime - EndTime;
        public int DurationInDays => EndDate - StartDate;
        public bool IsEventRunning { get { return eventRunning; } set { eventRunning = value; } }
        public Action OnEventStart { get { return onEventStart; } set { onEventStart = value; } }
        public Action OnEventRunning { get { return onEventRunning; } set { onEventRunning = value; } }
        public Action OnEventEnd { get { return onEventEnd; } set { onEventEnd = value; } }


        [SerializeField]
        private Action onEventStart;
        [SerializeField]
        private Action onEventRunning;
        [SerializeField]
        private Action onEventEnd;

        public string EventName => eventName;
        public string EventDescription => eventDescription;
        public MeridiemEventType EventType => eventType;


        [SerializeField] private MeridiemDate startDate = new(0, 0, 0000);
        [SerializeField] private MeridiemDate endDate = new(0, 0, 0000);
        [SerializeField] private MeridiemTime startTime = new(9, 00, Meridiem.AM);
        [SerializeField] private MeridiemTime endTime = new(10, 00, Meridiem.AM);
        [SerializeReference] private MeridiemEventRepeatStyle repeatStyle = new RepeatNever();

        [SerializeField] private bool allDay = false;
        [SerializeField] private bool dateRange = false;
        [SerializeField] private string eventName = "New Meridiem Event";
        [SerializeField] private string eventDescription = "";
        [SerializeField] private MeridiemEventType eventType = MeridiemEventType.None;
        [SerializeField] private bool eventRunning = false;


        public RuntimeEvent(MeridiemDate date, MeridiemTime startTime, MeridiemTime endTime, MeridiemEventRepeatStyle repeatStyle, string eventName = "New Event", string eventDescription = "", MeridiemEventType eventType = MeridiemEventType.None)
        {
            this.startDate = date;
            this.endDate = date;
            this.startTime = startTime;
            this.endTime = endTime;
            this.repeatStyle = repeatStyle;
            this.eventName = eventName;
            this.eventDescription = eventDescription;
            this.eventType = eventType;
        }

        public RuntimeEvent(MeridiemDate startDate, MeridiemDate endDate, MeridiemTime startTime, MeridiemTime endTime, MeridiemEventRepeatStyle repeatStyle, string eventName = "New Event", string eventDescription = "", MeridiemEventType eventType = MeridiemEventType.None)
        {
            this.startDate = startDate;
            this.endDate = endDate;
            this.startTime = startTime;
            this.endTime = endTime;
            this.repeatStyle = repeatStyle;
            this.eventName = eventName;
            this.eventDescription = eventDescription;
            this.eventType = eventType;
        }
    }

}