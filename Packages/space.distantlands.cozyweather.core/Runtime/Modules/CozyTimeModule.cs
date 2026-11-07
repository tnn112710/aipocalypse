//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEngine.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace DistantLands.Cozy
{
    public delegate void ChangeMeridiemBlockEvent(MeridiemBlockType block);

    [ExecuteAlways]
    [AddComponentMenu("")]
    [CozyModule("Time", "Time", "Setup time settings, simple calendars, and manage current settings.", "Time")]
    public class CozyTimeModule : CozyModule, ITimeModule, ICalendarModule, ICalendarEventsModule, ISaveable, ISkippable
    {

        #region Calendar

        [CozySearchable]
        [SerializeField]
        private MeridiemDate currentDate = new(0);
        public MeridiemDate CurrentDate
        {
            get => currentDate;
            set
            {
                int oldYear = currentDate.year;
                currentDate = value;
                RunChangeDayEvents();
                if (oldYear != value.year)
                {
                    OnChangeYear?.Invoke();
                }
            }
        }
        public event ChangeDayEvent OnChangeDay;
        public event ChangeYearEvent OnChangeYear;

        /// <summary>
        /// Will the day move to the next day at midnight?
        /// </summary>
        public bool ProgressDay { get => progressDay; set => progressDay = value; }

        [Tooltip("Will the day move to the next day at midnight?")]
        [SerializeField]
        [CozySearchable]
        private bool progressDay = true;

        #endregion

        #region Calendar Events
        public List<RuntimeEvent> RuntimeEvents { get => runtimeEvents; set => runtimeEvents = value; }
        public List<StaticEvent> StaticEvents { get => staticEvents; set => staticEvents = value; }
        private List<RuntimeEvent> runtimeEvents = new();
        [SerializeField]
        private List<StaticEvent> staticEvents = new();

        public List<(IMeridiemEvent eventReference, bool allDay, MeridiemTime startTime, MeridiemTime endTime)> EventsToday
        {
            get
            {
                if (eventsToday == null || eventsToday.Count == 0)
                    ((ICalendarEventsModule)this).GetEventsOnDay(CurrentDate);

                return eventsToday;
            }
            set => eventsToday = value;
        }
        private List<(IMeridiemEvent eventReference, bool allDay, MeridiemTime startTime, MeridiemTime endTime)> eventsToday = new();

        private List<IMeridiemEvent> _eventsBuffer = new(64);
        private List<(IMeridiemEvent eventReference, bool allDay, MeridiemTime startTime, MeridiemTime endTime)> _eventsTodayBuffer =
            new(64);

        #endregion

        #region Time

        [FormerlySerializedAs("m_DayPercentage")]
        [CozySearchable]
        [Tooltip("Access the current time of day.")]
        [SerializeField]
        private MeridiemTime currentTime = 0.5f;
        public MeridiemTime CurrentTime
        {
            get => currentTime;
            set
            {
                if (value.hours != currentTime.hours)
                    OnNewHour?.Invoke(currentTime.hours, value.hours);

                if (value.minutes != currentTime.minutes)
                    OnNewMinute?.Invoke(currentTime.minutes, value.minutes);

                currentTime = value;
            }
        }

        public float TimeMovementSpeed
        {
            get => timeMovementSpeed * (PauseTime ? 0 : 1) * (modulateTimeSpeed ? timeSpeedMultiplier.Evaluate(CurrentTime) : 1) / 1440;
            set => timeMovementSpeed = value;
        }
        [Tooltip("Specifies the amount of in-game minutes that pass in a real-world second.")]
        [SerializeField]
        [CozySearchable]
        private float timeMovementSpeed = 1;


        public float ModifiedTime
        {
            get => GetModifiedTime(CurrentTime);
        }

        public event ModifyTime OnModifyTime;
        public event NewHour OnNewHour;
        public event NewMinute OnNewMinute;

        /// <summary>
        /// Applies registered time modifications to a supplied time of day.
        /// </summary>
        public float GetModifiedTime(float time)
        {
            return OnModifyTime != null ? OnModifyTime.Invoke(time) : time;
        }

        public bool PauseTime { get => pauseTime; set => pauseTime = value; }
        [Tooltip("Pauses time and prevents time movement")]
        [SerializeField]
        [CozySearchable]
        private bool pauseTime;
        /// <summary>
        /// Should this system reset the time when it loads? (Scene change, application load, etc.)
        /// </summary>
        [Tooltip("Should this system reset the time when it loads?")]
        [SerializeField]
        [CozySearchable]
        private bool resetTimeOnStart = false;
        /// <summary>
        /// The time that this system should start at when the scene is loaded
        /// </summary>
        [Tooltip("The time that this system should start at when the scene is loaded.")]
        [SerializeField]
        [CozySearchable]
        private MeridiemTime startTime = new(9, 00, Meridiem.AM);
        /// <summary>
        /// Should time speed modulate based on the current time of day?
        /// </summary>
        [Tooltip("Should time speed modulate based on the current time of day?")]
        [SerializeField]
        [CozySearchable]
        private bool modulateTimeSpeed = true;
        /// <summary>
        /// Modulates the time speed based on the day percentage.
        /// </summary>
        [Tooltip("Modulates the time speed based on the day percentage.")]
        [SerializeField]
        [CozySearchable]
        private AnimationCurve timeSpeedMultiplier = new(new Keyframe(0, 1), new Keyframe(1, 1));

        #endregion

        #region Time Blocks

        /// <summary>
        /// Get the current time block
        /// </summary>
        public MeridiemBlockType CurrentBlock
        {
            get
            {
                return currentBlock;
            }
            set
            {
                if (value != currentBlock)
                {
                    ChangeBlockEvent?.Invoke(value);
                }

                currentBlock = value;
            }
        }
        private MeridiemBlockType currentBlock;
        /// <summary>
        /// Event that fires when the current time block changes
        /// </summary>
        public event ChangeMeridiemBlockEvent ChangeBlockEvent;
        #endregion

        public override void InitializeModule()
        {
            // WeatherSphere.TimeModule = this;

            SetupTime();

            OnNewMinute += UpdateTimeBlock;
            OnChangeDay += RefreshTodaysEvents;
        }

        public override void DeinitializeModule()
        {

            OnNewMinute -= UpdateTimeBlock;
            OnChangeDay -= RefreshTodaysEvents;
        }

        /// <summary>
        /// Refresh the events that are happening today.
        /// </summary>
        public void RefreshTodaysEvents()
        {
            ((ICalendarEventsModule)this).GetEventsOnDay(CurrentDate);
        }


        public override bool CheckIfModuleCanBeRemoved(out string warning)
        {
            if (WeatherSphere.HasModule<CozyTransitModule>())
            {
                warning = "Transit Module";
                return false;
            }
            warning = "";
            return true;
        }

        public override bool CheckIfModuleCanBeAdded(out string warning)
        {
            if (WeatherSphere.HasModule<ITimeModule>())
            {
                warning = "Time Module";
                return false;
            }
            if (WeatherSphere.HasModule<ICalendarModule>())
            {
                warning = "Calendar Module";
                return false;
            }
            if (WeatherSphere.HasModule<ICalendarEventsModule>())
            {
                warning = "Calendar Events Module";
                return false;
            }
            warning = "";
            return true;
        }

        void Update()
        {
            ManageTime();

            // Update Events
            if (eventsToday == null) ((ICalendarEventsModule)this).GetEventsOnDay(CurrentDate);

            foreach (var evtTuple in eventsToday)
            {
                IMeridiemEvent meridiemEvent = evtTuple.eventReference;
                if (evtTuple.allDay)
                {
                    meridiemEvent.UpdateEvent(true);
                    continue;
                }
                if (meridiemEvent.Overnight)
                {
                    if (evtTuple.startTime >= evtTuple.endTime)
                    {
                        meridiemEvent.UpdateEvent(CurrentTime <= evtTuple.endTime || CurrentTime >= evtTuple.startTime);
                        continue;
                    }
                }

                meridiemEvent.UpdateEvent(CurrentTime >= evtTuple.startTime && CurrentTime <= evtTuple.endTime);
            }

        }

        void SetupTime()
        {
            if (resetTimeOnStart)
                CurrentTime = startTime;

        }

        void UpdateTimeBlock(int oldMinute, int newMinute)
        {
            if (CurrentTime < CozyPreferences.DawnTimes.startTime || CurrentTime > CozyPreferences.NightTimes.startTime)
                CurrentBlock = MeridiemBlockType.Night;
            else if (CurrentTime < CozyPreferences.MorningTimes.startTime)
                CurrentBlock = MeridiemBlockType.Dawn;
            else if (CurrentTime < CozyPreferences.DayTimes.startTime)
                CurrentBlock = MeridiemBlockType.Morning;
            else if (CurrentTime < CozyPreferences.AfternoonTimes.startTime)
                CurrentBlock = MeridiemBlockType.Day;
            else if (CurrentTime < CozyPreferences.EveningTimes.startTime)
                CurrentBlock = MeridiemBlockType.Afternoon;
            else if (CurrentTime < CozyPreferences.TwilightTimes.startTime)
                CurrentBlock = MeridiemBlockType.Evening;
            else if (CurrentTime < CozyPreferences.NightTimes.startTime)
                CurrentBlock = MeridiemBlockType.Twilight;
        }

        public void RunChangeDayEvents()
        {
            OnChangeDay?.Invoke();
        }
        public void RunOnNewHour(int oldHour, int newHour)
        {
            OnNewHour?.Invoke(oldHour, newHour);
        }

        public void RunOnNewMinute(int oldMinute, int newMinute)
        {
            OnNewMinute?.Invoke(oldMinute, newMinute);
        }

        public float DayAndTime => CurrentDate.AbsoluteDayInYear + CurrentTime;

        /// <summary>
        /// Manages the movement of time in the scene.
        /// </summary> 
        public void ManageTime()
        {
            if (Application.isPlaying && !PauseTime)
            {
                float timeAsFloat = CurrentTime;
                timeAsFloat += TimeMovementSpeed * Time.deltaTime;
                ConstrainTime(ref timeAsFloat);
                CurrentTime = timeAsFloat;
            }
        }

        private void ConstrainTime(ref float time)
        {
            if (time >= 1)
            {
                time--;
                if (progressDay) CurrentDate++;
            }

            if (time < 0)
            {
                time++;
                if (progressDay) CurrentDate--;
            }
        }


        public void AddEvent(RuntimeEvent meridiemEvent)
        {
            runtimeEvents.Add(meridiemEvent);
            ((ICalendarEventsModule)this).GetEventsOnDay(CurrentDate);
        }

        public void RemoveEvent(RuntimeEvent meridiemEvent)
        {
            runtimeEvents.Remove(meridiemEvent);
            ((ICalendarEventsModule)this).GetEventsOnDay(CurrentDate);
        }

        public List<IMeridiemEvent> GetEventsOnDay(MeridiemDate date)
        {
            if (_eventsBuffer == null) _eventsBuffer = new List<IMeridiemEvent>();
            if (_eventsTodayBuffer == null) _eventsTodayBuffer = new List<(IMeridiemEvent eventReference, bool allDay, MeridiemTime startTime, MeridiemTime endTime)>();
            if (RuntimeEvents == null) RuntimeEvents = new List<RuntimeEvent>();

            _eventsBuffer.Clear();
            _eventsTodayBuffer.Clear();

            for (int i = 0; i < StaticEvents.Count; i++)
            {
                IMeridiemEvent ev = StaticEvents[i];
                if (ev != null)
                    if (ev.DoesEventHappenToday(date))
                    {
                        _eventsBuffer.Add(ev);
                    }
            }

            for (int i = 0; i < RuntimeEvents.Count; i++)
            {
                IMeridiemEvent ev = RuntimeEvents[i];
                if (ev != null)
                    if (ev.DoesEventHappenToday(date))
                    {
                        _eventsBuffer.Add(ev);
                    }
            }

            _eventsBuffer.Sort((x, y) =>
            {
                float valX = (float)x.StartTime + x.StartDate;
                float valY = (float)y.StartTime + y.StartDate;
                return valX.CompareTo(valY);
            });

            for (int i = 0; i < _eventsBuffer.Count; i++)
            {
                IMeridiemEvent eventReference = _eventsBuffer[i];

                if (eventReference.DoesEventHappenAllDay(date))
                {
                    _eventsTodayBuffer.Add((eventReference, true, 0, new MeridiemTime(11, 59, Meridiem.PM)));
                    continue;
                }

                if (eventReference.Overnight)
                {
                    eventReference.GetRelativeStartDate(date, out int startDateOffset, out int endDateOffset);

                    MeridiemTime start = (startDateOffset == 0) ? eventReference.StartTime : new MeridiemTime(12, 00, Meridiem.AM);
                    MeridiemTime end = (endDateOffset == 0) ? eventReference.EndTime : new MeridiemTime(11, 59, Meridiem.PM);

                    _eventsTodayBuffer.Add((eventReference, false, start, end));
                }
                else
                {
                    _eventsTodayBuffer.Add((eventReference, false, eventReference.StartTime, eventReference.EndTime));
                }
            }

            EventsToday = _eventsTodayBuffer;

            return _eventsBuffer;
        }

        /// <summary>
        /// Smoothly sets the time to a new value. Useful for skipping nights or other durations of time
        /// </summary>
        /// <param name="newTime">The target time that the system will set the time to.</param>
        /// <param name="duration">The time in seconds it takes for the system to get to the new time</param>
        /// <param name="moveTimeForward"><b>Optional:</b> Should the time move forward only or move backwards to </param>
        public void SmoothlySkipTime(MeridiemTime newTime, float duration, bool moveTimeForward = true)
        {
            if (timeSkipCoroutine != null) StopCoroutine(timeSkipCoroutine);

            timeSkipCoroutine = StartCoroutine(SmoothlySkipTimeRoutine(newTime, duration, moveTimeForward));
        }

        private Coroutine timeSkipCoroutine;

        private IEnumerator SmoothlySkipTimeRoutine(MeridiemTime newTime, float duration, bool moveTimeForward = true)
        {
            float timePassed = 0;
            float originalTime = CurrentTime;
            float newTimePercentage = newTime;

            if (moveTimeForward && newTimePercentage < originalTime)
                newTimePercentage++;
            else if (!moveTimeForward && newTimePercentage > originalTime)
                newTimePercentage--;

            while (timePassed < duration)
            {
                float current = Mathf.Lerp(originalTime, newTimePercentage, timePassed / duration);

                if (current > 1)
                {
                    current--;
                    originalTime--;
                    newTimePercentage--;
                    currentDate++;
                }
                if (current < 0)
                {
                    current++;
                    originalTime++;
                    newTimePercentage++;
                    currentDate--;
                }

                CurrentTime = current;

                timePassed += Time.deltaTime;
                yield return null;
            }

            CurrentTime = newTime;
            timeSkipCoroutine = null;
        }

        public SavedData Save()
        {
            List<string> saveString = new()
            {
                JsonUtility.ToJson(CurrentTime),
                JsonUtility.ToJson(CurrentDate),
                JsonUtility.ToJson(RuntimeEvents)
            };

            return new SavedData(GetType().Name, saveString);
        }

        public void Load(SavedData saveString)
        {
            CurrentTime = JsonUtility.FromJson<MeridiemTime>(saveString.properties[0]);
            CurrentDate = JsonUtility.FromJson<MeridiemDate>(saveString.properties[1]);
            RuntimeEvents = JsonUtility.FromJson<List<RuntimeEvent>>(saveString.properties[2]);
        }

        /// <summary>
        /// Use to change the time allowing for overflow that triggers date changes
        /// </summary>
        /// <param name="delta"></param>
        public void SkipTime(MeridiemTimeDelta delta)
        {
            float timeAsFloat = CurrentTime;
            timeAsFloat += delta;
            ConstrainTime(ref timeAsFloat);
            CurrentTime = timeAsFloat;
        }
    }

}