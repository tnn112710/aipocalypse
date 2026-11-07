//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine;

namespace DistantLands.Cozy
{

    [CreateAssetMenu(menuName = "Distant Lands/Cozy/Meridiem Event", order = 361)]
    public class StaticEvent : ScriptableObject, IMeridiemEvent
    {
        public MeridiemDate StartDate => RepeatStyle.LimitTo24Hour ? 0 : startDate;
        public MeridiemDate EndDate => (!RepeatStyle.LimitTo24Hour ? (dateRange ? endDate : StartDate) : StartDate) + (StartTime > EndTime ? 1 : 0);
        public MeridiemTime StartTime => allDay ? 0 : startTime;
        public MeridiemTime EndTime => allDay ? 1 : endTime;
        public bool AllDay => allDay;
        public bool DateRange => dateRange;
        public MeridiemEventRepeatStyle RepeatStyle { get { return repeatStyle ?? new RepeatNever(); } set { repeatStyle = value; } }
        public bool Overnight => StartDate < EndDate;
        public MeridiemTimeDelta Duration => EndTime > StartTime ? EndTime - StartTime : StartTime - EndTime;
        public int DurationInDays => EndDate - StartDate;

        public string EventName => name;
        public string EventDescription => eventDescription;
        public MeridiemEventType EventType => eventType;

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

        [SerializeField]
        [Tooltip("Specifies the start time of the event")]
        private MeridiemDate startDate = new(0, 0, 0000);
        [SerializeField]
        [Tooltip("Specifies the end time of the event")]
        private MeridiemDate endDate = new(0, 0, 0000);
        [SerializeField]
        private MeridiemTime startTime = new(9, 00, Meridiem.AM);
        [SerializeField]
        private MeridiemTime endTime = new(10, 00, Meridiem.AM);

        [SerializeField]
        private bool allDay = false;
        [SerializeField]
        private bool dateRange = false;
        [SerializeField]
        private string eventDescription = "";
        [SerializeField]
        private MeridiemEventType eventType = MeridiemEventType.None;
        private bool eventRunning = false;
        [SerializeField]
        [SerializeReference]
        public MeridiemEventRepeatStyle repeatStyle = new RepeatNever();

    }


}