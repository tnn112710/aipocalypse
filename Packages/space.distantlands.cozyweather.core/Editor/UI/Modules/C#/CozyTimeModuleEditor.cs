//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    [CustomEditor(typeof(CozyTimeModule))]
    public class CozyTimeModuleEditor : CozyModuleEditor
    {

        public delegate void UpdateUI();
        public static event UpdateUI ExecuteUpdateUI;

        CozyTimeModule timeModule;
        public VisualElement TimeSettingsContainer => root.Q<VisualElement>("time-container");
        public VisualElement DateSettingsContainer => root.Q<VisualElement>("date-container");
        public VisualElement EventsContainer => root.Q<VisualElement>("events-container");
        public Label MonthSelector => root.Q<Label>("month-selector");
        public VisualElement MonthView => root.Q<VisualElement>("month-view");
        public VisualElement MonthViewMask => root.Q<VisualElement>("month-view-mask");
        public VisualElement PreviousMonth => root.Q<VisualElement>("back");
        public VisualElement NextMonth => root.Q<VisualElement>("forward");
        public VisualElement DayView => root.Q<VisualElement>("day-view");
        public VisualElement PreviousDay => DayView.Q<VisualElement>("back");
        public VisualElement NextDay => DayView.Q<VisualElement>("forward");
        public VisualElement AllDayEvents => root.Q<VisualElement>("all-day-events");
        public VisualElement DailyEventContainer => root.Q<VisualElement>("daily-event-container");
        PropertyField currentDay;
        public Label DayTitle => root.Q<Label>("date");
        public Label WeekdayTitle => root.Q<Label>("weekday");


        VisualElement root;

        public static void UpdateCalendarUI()
        {
            ExecuteUpdateUI?.Invoke();
        }

        void OnEnable()
        {
            if (!target)
                return;

            timeModule = (CozyTimeModule)target;
        }

        void OnDisable()
        {
            ExecuteUpdateUI -= UpdateMonthCalendar;
            ExecuteUpdateUI -= UpdateDaySchedule;
        }


        public override ModuleWidget[] ModuleWidgets => new ModuleWidget[]
        {
            new(
                ModuleWidget.WidgetSize.Small, "Current Date", true,
                () => {
                    DraggableElement draggable = new DraggableElement();
                    draggable.pixelThreshold = 20;
                    draggable.OnDrag += (v) => {
                        MeridiemDate newDate = (timeModule.CurrentDate + (int)v).ToBalanced();
                        timeModule.CurrentDate = newDate;
                        weatherEditor.UpdateWidgets();
                    };
                    VisualElement container = draggable.NewContainer();
                    container.style.paddingBottom = 8;
                    container.style.paddingLeft = 8;
                    container.style.paddingRight = 8;
                    container.style.paddingTop = 8;
                    container.style.alignItems = Align.Center;
                    container.style.justifyContent = Justify.SpaceBetween;
                    container.style.flexGrow = 1;

                    Label weekdayLabel = new Label();
                    weekdayLabel.pickingMode = PickingMode.Ignore;
                    weekdayLabel.name = "weekdayLabel";
                    weekdayLabel.text = timeModule.CurrentDate.Weekday.ToString()[..3];
                    container.Add(weekdayLabel);

                    Label dateLabel = new Label();
                    dateLabel.name = "dateLabel";
                    dateLabel.pickingMode = PickingMode.Ignore;
                    dateLabel.text = (timeModule.CurrentDate.day).ToString();
                    dateLabel.style.fontSize = 28;
                    dateLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    dateLabel.style.color = Branding.blue;
                    container.Add(dateLabel);

                    Label monthLabel = new Label();
                    monthLabel.pickingMode = PickingMode.Ignore;
                    monthLabel.name = "monthLabel";
                    monthLabel.text = timeModule.CurrentDate.MonthName[..3];
                    container.Add(monthLabel);

                    return container;
                },
                (widget) =>
                {
                    widget.container.Q<Label>("weekdayLabel").text = timeModule.CurrentDate.Weekday.ToString()[..3];
                    widget.container.Q<Label>("dateLabel").text = (timeModule.CurrentDate.day).ToString();
                    widget.container.Q<Label>("monthLabel").text = timeModule.CurrentDate.MonthName[..3];
                }
            ),
            new(
                ModuleWidget.WidgetSize.Small, "Current Time", true,
                () => {
                    DraggableElement draggable = new DraggableElement();
                    draggable.pixelThreshold = 1;
                    draggable.OnDrag += (v) => {
                        MeridiemTime time = (timeModule.CurrentTime + (v / 1000f));
                        timeModule.CurrentTime = time;
                        weatherEditor.UpdateWidgets();
                    };
                    VisualElement container = draggable.NewContainer();
                    container.style.paddingBottom = 8;
                    container.style.paddingLeft = 8;
                    container.style.paddingRight = 8;
                    container.style.paddingTop = 8;
                    container.style.alignItems = Align.Center;
                    container.style.justifyContent = Justify.Center;
                    container.style.flexGrow = 1;

                    Label clockLabel = new Label();
                    clockLabel.pickingMode = PickingMode.Ignore;
                    clockLabel.name = "clockLabel";
                    clockLabel.text = timeModule.CurrentTime.ToString();
                    clockLabel.style.fontSize = 16;
                    clockLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    clockLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    clockLabel.style.color = Branding.blue;
                    container.Add(clockLabel);

                    Label fuzzyTimeLabel = new Label();
                    fuzzyTimeLabel.pickingMode = PickingMode.Ignore;
                    fuzzyTimeLabel.name = "fuzzyTimeLabel";
                    fuzzyTimeLabel.style.marginTop = 2;
                    fuzzyTimeLabel.text = timeModule.CurrentBlock.ToString();
                    container.Add(fuzzyTimeLabel);

                    return container;
                },
                (widget) =>
                {
                    widget.container.Q<Label>("clockLabel").text = timeModule.CurrentTime.ToString();
                    widget.container.Q<Label>("fuzzyTimeLabel").text = timeModule.CurrentBlock.ToString();
                }
            ),
            new(
                ModuleWidget.WidgetSize.Medium, "Today's Events", false,
                () => {
                    VisualElement container = new();
                    container.style.paddingBottom = 0;
                    container.style.paddingLeft = 0;
                    container.style.paddingRight = 0;
                    container.style.paddingTop = 0;
                    container.style.flexDirection = FlexDirection.Row;
                    container.style.flexGrow = 1;
                    VisualElement leftContainer = new();
                    leftContainer.style.paddingBottom = 8;
                    leftContainer.style.paddingLeft = 8;
                    leftContainer.style.paddingRight = 2;
                    leftContainer.style.paddingTop = 8;
                    leftContainer.style.width = new Length(50, LengthUnit.Percent);
                    ScrollView rightContainer = new();
                    rightContainer.name = "rightContainer";
                    rightContainer.style.width = new Length(50, LengthUnit.Percent);
                    rightContainer.style.backgroundColor = new Color(.14f,.14f,.14f);
                    container.Add(leftContainer);
                    container.Add(rightContainer);

                    Label weekdayLabel = new Label();
                    weekdayLabel.name = "weekdayLabel";
                    weekdayLabel.text = timeModule.CurrentDate.Weekday.ToString();
                    leftContainer.Add(weekdayLabel);

                    Label dateLabel = new Label();
                    dateLabel.name = "dateLabel";
                    dateLabel.text = timeModule.CurrentDate.MonthName[..3] + " " + (timeModule.CurrentDate.day).ToString();
                    leftContainer.Add(dateLabel);

                    Label eventCountLabel = new Label();
                    eventCountLabel.name = "eventCountLabel";
                    eventCountLabel.text = $"{timeModule.EventsToday.Count} events today";
                    leftContainer.Add(eventCountLabel);

                    foreach (var evt in timeModule.EventsToday)
                    {
                        VisualElement eventContainer = new VisualElement();
                        eventContainer.AddToClassList("event");
                        eventContainer.AddToClassList(evt.eventReference.EventType.ToString().ToLower());
                        eventContainer.style.position = Position.Relative;
                        eventContainer.style.flexShrink = 0;
                        eventContainer.style.paddingBottom = 2;
                        eventContainer.style.paddingLeft = 4;
                        eventContainer.style.paddingRight = 2;
                        eventContainer.style.paddingTop = 2;
                        eventContainer.style.borderTopLeftRadius = 4;
                        eventContainer.style.borderTopRightRadius = 4;
                        eventContainer.style.borderBottomLeftRadius = 4;
                        eventContainer.style.borderBottomRightRadius = 4;
                        eventContainer.style.flexDirection = FlexDirection.Row;

                        eventContainer.RegisterCallback<ClickEvent>(e =>
                        {
                            if (evt.eventReference is StaticEvent)
                                Selection.activeObject = (StaticEvent)evt.eventReference;
                        });

                        VisualElement eventDot = new VisualElement();
                        eventContainer.Add(eventDot);

                        Label eventLabel = new Label();
                        eventLabel.style.fontSize = 8;
                        eventLabel.text = $"{evt.eventReference.EventName}";
                        eventContainer.Add(eventLabel);

                        rightContainer.Add(eventContainer);
                    }
                    return container;
                },
                (widget) =>
                {
                    ((ICalendarEventsModule)timeModule).GetEventsOnDay(timeModule.CurrentDate);

                    widget.container.Q<Label>("weekdayLabel").text = timeModule.CurrentDate.Weekday.ToString();
                    widget.container.Q<Label>("dateLabel").text = timeModule.CurrentDate.MonthName[..3] + " " + (timeModule.CurrentDate.day).ToString();
                    widget.container.Q<Label>("eventCountLabel").text = $"{timeModule.EventsToday.Count} events today";

                    widget.container.Q<VisualElement>("rightContainer").Clear();

                    foreach (var evt in timeModule.EventsToday)
                    {
                        VisualElement eventContainer = new VisualElement();
                        eventContainer.AddToClassList("event");
                        eventContainer.AddToClassList(evt.eventReference.EventType.ToString().ToLower());
                        eventContainer.style.position = Position.Relative;
                        eventContainer.style.flexShrink = 0;
                        eventContainer.style.paddingBottom = 2;
                        eventContainer.style.paddingLeft = 4;
                        eventContainer.style.paddingRight = 2;
                        eventContainer.style.paddingTop = 2;
                        eventContainer.style.borderTopLeftRadius = 4;
                        eventContainer.style.borderTopRightRadius = 4;
                        eventContainer.style.borderBottomLeftRadius = 4;
                        eventContainer.style.borderBottomRightRadius = 4;
                        eventContainer.style.flexDirection = FlexDirection.Row;

                        eventContainer.RegisterCallback<ClickEvent>(e =>
                        {
                            if (evt.eventReference is StaticEvent)
                                Selection.activeObject = (StaticEvent)evt.eventReference;
                        });

                        VisualElement eventDot = new VisualElement();
                        eventContainer.Add(eventDot);

                        Label eventLabel = new Label();
                        eventLabel.style.fontSize = 8;
                        eventLabel.text = $"{evt.eventReference.EventName}";
                        eventContainer.Add(eventLabel);

                        widget.container.Q<VisualElement>("rightContainer").Add(eventContainer);
                    }

                }
            ),
        };

        public override VisualElement DisplayUI()
        {
            root = new VisualElement();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Modules/UXML/time-module-editor.uxml"
            );

            ExecuteUpdateUI += UpdateMonthCalendar;
            ExecuteUpdateUI += UpdateDaySchedule;

            asset.CloneTree(root);

            MonthViewMask.style.height = 45;
            UpdateMonthCalendar();

            PropertyField dayPercentage = new PropertyField();
            dayPercentage.AddToClassList("line-below");
            dayPercentage.BindProperty(serializedObject.FindProperty("currentTime"));

            int ConvertTo24Hour(int hours, bool PM)
            {
                int hour24 = hours % 12;
                if (PM) hour24 += 12;

                return hour24;
            }

            int hours = ConvertTo24Hour(serializedObject.FindProperty("currentTime").FindPropertyRelative("hours").intValue, serializedObject.FindProperty("currentTime").FindPropertyRelative("meridiem").intValue == 1);
            int minutes = serializedObject.FindProperty("currentTime").FindPropertyRelative("minutes").intValue;
            dayPercentage.RegisterValueChangeCallback((evt) =>
            {
                int newHours = ConvertTo24Hour(serializedObject.FindProperty("currentTime").FindPropertyRelative("hours").intValue, serializedObject.FindProperty("currentTime").FindPropertyRelative("meridiem").intValue == 1);
                int newMinutes = serializedObject.FindProperty("currentTime").FindPropertyRelative("minutes").intValue;
                if (hours != newHours)
                {
                    timeModule.RunOnNewHour(hours, newHours);
                    hours = newHours;
                }
                if (minutes != newMinutes)
                {
                    timeModule.RunOnNewMinute(minutes, newMinutes);
                    minutes = newMinutes;
                }
            });
            dayPercentage.label = "Time";
            TimeSettingsContainer.Add(dayPercentage);

            VisualElement resetTimeContainer = new();
            TimeSettingsContainer.Add(resetTimeContainer);
            resetTimeContainer.AddToClassList("line-below");

            PropertyField resetTimeOnStart = new PropertyField();
            resetTimeOnStart.BindProperty(serializedObject.FindProperty("resetTimeOnStart"));
            resetTimeContainer.Add(resetTimeOnStart);

            PropertyField startTime = new PropertyField();
            startTime.AddToClassList("indented");
            startTime.style.display = serializedObject.FindProperty("resetTimeOnStart").boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            resetTimeOnStart.RegisterCallback<ChangeEvent<bool>>(evt => startTime.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None);
            startTime.BindProperty(serializedObject.FindProperty("startTime"));
            resetTimeContainer.Add(startTime);

            PropertyField pauseTime = new PropertyField();
            pauseTime.BindProperty(serializedObject.FindProperty("pauseTime"));
            TimeSettingsContainer.Add(pauseTime);

            PropertyField timeMovementSpeed = new PropertyField();
            timeMovementSpeed.BindProperty(serializedObject.FindProperty("timeMovementSpeed"));
            TimeSettingsContainer.Add(timeMovementSpeed);

            PropertyField modulateTimeSpeed = new PropertyField();
            modulateTimeSpeed.BindProperty(serializedObject.FindProperty("modulateTimeSpeed"));
            TimeSettingsContainer.Add(modulateTimeSpeed);

            PropertyField timeSpeedMultiplier = new PropertyField();
            timeSpeedMultiplier.AddToClassList("indented");
            timeSpeedMultiplier.style.display = serializedObject.FindProperty("modulateTimeSpeed").boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            modulateTimeSpeed.RegisterCallback<ChangeEvent<bool>>(evt => timeSpeedMultiplier.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None);
            timeSpeedMultiplier.BindProperty(serializedObject.FindProperty("timeSpeedMultiplier"));
            TimeSettingsContainer.Add(timeSpeedMultiplier);


            currentDay = new PropertyField();
            currentDay.BindProperty(serializedObject.FindProperty("currentDate"));
            currentDay.RegisterValueChangeCallback(evt =>
            {
                timeModule.RunChangeDayEvents();
                UpdateCalendarUI();
            });
            DateSettingsContainer.Add(currentDay);

            PropertyField progressDay = new PropertyField();
            progressDay.BindProperty(serializedObject.FindProperty("progressDay"));
            DateSettingsContainer.Add(progressDay);


            PropertyField staticEvents = new PropertyField();
            staticEvents.BindProperty(serializedObject.FindProperty("staticEvents"));
            EventsContainer.Add(staticEvents);

            PreviousMonth.RegisterCallback<ClickEvent>(evt =>
            {
                SelectDay(new MeridiemDate(timeModule.CurrentDate.month - 1, 1, timeModule.CurrentDate.year).ToBalanced());

            });
            NextMonth.RegisterCallback<ClickEvent>(evt =>
            {
                SelectDay(new MeridiemDate(timeModule.CurrentDate.month + 1, 1, timeModule.CurrentDate.year).ToBalanced());
            });
            PreviousDay.RegisterCallback<ClickEvent>(evt =>
            {
                SelectDay(new MeridiemDate(timeModule.CurrentDate.month, timeModule.CurrentDate.day - 1, timeModule.CurrentDate.year).ToBalanced());

            });
            NextDay.RegisterCallback<ClickEvent>(evt =>
            {
                SelectDay(new MeridiemDate(timeModule.CurrentDate.month, timeModule.CurrentDate.day + 1, timeModule.CurrentDate.year).ToBalanced());
            });

            UpdateDaySchedule();


            return root;

        }

        public void UpdateCurrentSettingsContainer()
        {
            TimeSettingsContainer.Clear();
            DateSettingsContainer.Clear();

        }

        public void UpdateMonthCalendar()
        {
            MonthView.Clear();
            MeridiemDate CurrentDate = timeModule.CurrentDate;

            var firstDayOfMonth = new MeridiemDate(CurrentDate.month, 1, CurrentDate.year);
            int daysInViewedMonth = CozyPreferences.Months[CurrentDate.month - 1].days;

            MonthSelector.text = $"{CozyPreferences.Months[CurrentDate.month - 1].name} {CurrentDate.year:D4}";

            GenericMenu menu = new GenericMenu();

            for (int i = 0; i < CozyPreferences.MonthsPerYear; i++)
            {
                int monthID = i;
                CozyPreferences.MonthData month = CozyPreferences.Months[i];
                menu.AddItem(new GUIContent(month.name), monthID == CurrentDate.month - 1, () =>
                {
                    SelectDay(new MeridiemDate(monthID + 1, 1, CurrentDate.year));
                    UpdateMonthCalendar();
                });
            }

            MonthSelector.RegisterCallback<ClickEvent>(evt =>
            {
                menu.ShowAsContext();
            });


            DayOfWeek firstWeekday = (DayOfWeek)(((int)CozyPreferences.StartingWeekday + firstDayOfMonth.AbsoluteDay) % 7);

            int weekdayOffset = (int)firstWeekday;

            for (int i = 0; i < weekdayOffset; i++)
            {
                MonthView.Add(new VisualElement());
            }

            if (MonthViewMask.style.height.value == 45)
                MonthView.style.top = new StyleLength(new Length(-40 * ((timeModule.CurrentDate.day - 1 + weekdayOffset) / 7), LengthUnit.Pixel));

            MonthViewMask.RegisterCallback<MouseEnterEvent>(evt =>
            {
                MonthView.style.top = 0;
                MonthViewMask.style.height = 40 * ((weekdayOffset + daysInViewedMonth + 7) / 7);
            });
            MonthViewMask.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                MonthView.style.top = new StyleLength(new Length(-40 * ((timeModule.CurrentDate.day - 1 + weekdayOffset) / 7), LengthUnit.Pixel));
                MonthViewMask.style.height = 45;
            });

            for (int i = 1; i < daysInViewedMonth + 1; i++)
            {
                MeridiemDate dateReference = new MeridiemDate(CurrentDate.month, i, CurrentDate.year);
                VisualElement container = new VisualElement();
                VisualElement date = new VisualElement();
                date.AddToClassList("date");
                if (timeModule.CurrentDate == dateReference)
                {
                    date.AddToClassList("selected");
                }

                Label label = new Label();
                label.text = $"{i}";

                VisualElement indicator = new VisualElement();
                indicator.name = "indicator";

                List<IMeridiemEvent> events = timeModule.GetEventsOnDay(dateReference);
                int usedEvents = 0;

                for (int eventIndex = 0; usedEvents < 3 && eventIndex < events.Count; eventIndex++)
                {
                    IMeridiemEvent meridiemEvent = events[eventIndex];

                    VisualElement dot = new VisualElement();
                    dot.AddToClassList("event-dot");
                    switch (meridiemEvent.EventType)
                    {
                        case MeridiemEventType.Weather:
                            if (!CozyPreferences.DisplayWeatherEvents)
                                continue;
                            dot.AddToClassList("weather");
                            break;
                        case MeridiemEventType.Ambient:
                            if (!CozyPreferences.DisplayAmbientEvents)
                                continue;
                            dot.AddToClassList("ambient");
                            break;
                        case MeridiemEventType.Holiday:
                            if (!CozyPreferences.DisplayHolidayEvents)
                                continue;
                            dot.AddToClassList("holiday");
                            break;
                        case MeridiemEventType.NPC:
                            if (!CozyPreferences.DisplayNPCEvents)
                                continue;
                            dot.AddToClassList("npc");
                            break;
                        case MeridiemEventType.Enemy:
                            if (!CozyPreferences.DisplayEnemyEvents)
                                continue;
                            dot.AddToClassList("enemy");
                            break;
                        case MeridiemEventType.Player:
                            if (!CozyPreferences.DisplayPlayerEvents)
                                continue;
                            dot.AddToClassList("player");
                            break;
                        case MeridiemEventType.Status:
                            if (!CozyPreferences.DisplayStatusEvents)
                                continue;
                            dot.AddToClassList("status");
                            break;
                        case MeridiemEventType.WorldEvent:
                            if (!CozyPreferences.DisplayWorldEventEvents)
                                continue;
                            dot.AddToClassList("worldevent");
                            break;
                    }

                    indicator.Add(dot);
                    usedEvents++;
                }


                date.Add(label);
                date.Add(indicator);
                // Avoid closure allocation by capturing value
                MeridiemDate capturedDate = dateReference;
                date.RegisterCallback<ClickEvent>(evt => SelectDay(capturedDate));
                date.RegisterCallback<ContextClickEvent>(evt =>
                {
                    SelectDay(capturedDate);

                    GenericMenu dropdownMenu = new GenericMenu();
                    // dropdownMenu.AddItem(new GUIContent("Create New Event"), false, () => CozyEventCreatorWindow.ShowWindow(timeModule.CurrentTime.FloorToHour, capturedDate));
                    dropdownMenu.ShowAsContext();
                });

                container.Add(date);
                MonthView.Add(container);
            }

            for (int i = 0; i < 7 - (daysInViewedMonth + weekdayOffset) % 7; i++)
            {
                MonthView.Add(new VisualElement());
            }
        }

        public void UpdateDaySchedule()
        {
            const float horizontalMarginPercent = 1f;

            WeekdayTitle.text = timeModule.CurrentDate.Weekday.ToString();
            DayTitle.text = $"{timeModule.CurrentDate.MonthName} {timeModule.CurrentDate.day}";

            DailyEventContainer.Clear();
            AllDayEvents.Clear();
            timeModule.GetEventsOnDay(timeModule.CurrentDate);

            List<IMeridiemEvent> allDayEvents = new List<IMeridiemEvent>();
            List<(IMeridiemEvent evt, int startTime, int endTime)> eventsToday = new List<(IMeridiemEvent, int, int)>();

            var dailyEvents = timeModule.EventsToday;
            dailyEvents.OrderBy(x => x.eventReference.StartDate + x.eventReference.StartTime);

            foreach (var eventTuple in dailyEvents)
            {
                IMeridiemEvent meridiemEvent = eventTuple.eventReference;
                switch (meridiemEvent.EventType)
                {
                    case MeridiemEventType.Weather:
                        if (!CozyPreferences.DisplayWeatherEvents)
                            continue;
                        break;
                    case MeridiemEventType.Ambient:
                        if (!CozyPreferences.DisplayAmbientEvents)
                            continue;
                        break;
                    case MeridiemEventType.Holiday:
                        if (!CozyPreferences.DisplayHolidayEvents)
                            continue;
                        break;
                    case MeridiemEventType.NPC:
                        if (!CozyPreferences.DisplayNPCEvents)
                            continue;
                        break;
                    case MeridiemEventType.Enemy:
                        if (!CozyPreferences.DisplayEnemyEvents)
                            continue;
                        break;
                    case MeridiemEventType.Player:
                        if (!CozyPreferences.DisplayPlayerEvents)
                            continue;
                        break;
                    case MeridiemEventType.Status:
                        if (!CozyPreferences.DisplayStatusEvents)
                            continue;
                        break;
                    case MeridiemEventType.WorldEvent:
                        if (!CozyPreferences.DisplayWorldEventEvents)
                            continue;
                        break;
                    default:
                        if (!CozyPreferences.DisplayMiscEvents)
                            continue;
                        break;
                }

                if (meridiemEvent.DoesEventHappenAllDay(timeModule.CurrentDate))
                {
                    if (meridiemEvent.DoesEventHappenAllDay(timeModule.CurrentDate))
                    {
                        VisualElement eventElement = new VisualElement();
                        Label label = new Label();
                        label.text = meridiemEvent.EventName;
                        eventElement.Add(label);
                        UpdateEventClass(eventElement, meridiemEvent.EventType);
                        eventElement.tooltip = meridiemEvent.EventDescription;
                        eventElement.RegisterCallback<ClickEvent>(evt =>
                        {
                            if (meridiemEvent is StaticEvent)
                                Selection.activeObject = (StaticEvent)meridiemEvent;
                        });
                        AllDayEvents.Add(eventElement);
                    }
                }
                else
                {

                    if (meridiemEvent.Overnight)
                    {
                        meridiemEvent.GetRelativeStartDate(timeModule.CurrentDate, out int startDateOffset, out int endDateOffset);

                        if (startDateOffset == 0)
                            eventsToday.Add((meridiemEvent, meridiemEvent.StartTime.AbsoluteHour, 24));
                        if (endDateOffset == 0)
                            eventsToday.Add((meridiemEvent, 0, meridiemEvent.EndTime.AbsoluteHour));
                    }
                    else
                    {
                        eventsToday.Add((meridiemEvent, meridiemEvent.StartTime.AbsoluteHour, meridiemEvent.EndTime.AbsoluteHour));
                    }
                }
            }


            List<List<(IMeridiemEvent evt, int start, int end)>> groups = new List<List<(IMeridiemEvent, int, int)>>();
            foreach (var meridiemEvent in eventsToday)
            {

                bool foundGroup = false;
                foreach (var group in groups)
                {
                    bool overlapsWithGroup = false;
                    foreach (var groupEvent in group)
                    {
                        if (meridiemEvent.startTime < groupEvent.end &&
                            meridiemEvent.endTime > groupEvent.start)
                        {
                            overlapsWithGroup = true;
                            break;
                        }
                    }
                    if (overlapsWithGroup)
                    {
                        group.Add(meridiemEvent);
                        foundGroup = true;
                        break;
                    }
                }
                if (!foundGroup)
                {
                    groups.Add(new List<(IMeridiemEvent evt, int start, int end)> { meridiemEvent });
                }
            }

            var eventLayouts = new Dictionary<(IMeridiemEvent evt, int start, int end), (int track, int totalTracks)>();
            foreach (var group in groups)
            {
                group.Sort((a, b) =>
                {
                    int aDuration = a.end - a.start;
                    int bDuration = b.end - b.start;
                    return bDuration.CompareTo(aDuration);
                });

                var trackEndTimes = new List<int>();
                foreach (var meridiemEvent in group)
                {
                    int eventStart = meridiemEvent.start;
                    int eventEnd = meridiemEvent.end;
                    int assignedTrack = -1;

                    for (int i = 0; i < trackEndTimes.Count; i++)
                    {
                        if (eventStart >= trackEndTimes[i])
                        {
                            assignedTrack = i;
                            trackEndTimes[i] = eventEnd;
                            break;
                        }
                    }
                    if (assignedTrack == -1)
                    {
                        assignedTrack = trackEndTimes.Count;
                        trackEndTimes.Add(eventEnd);
                    }
                    eventLayouts[meridiemEvent] = (assignedTrack, 0); // Store track for now.
                }

                int totalTracks = trackEndTimes.Count;
                foreach (var meridiemEvent in group)
                {
                    var layout = eventLayouts[meridiemEvent];
                    eventLayouts[meridiemEvent] = (layout.track, totalTracks);
                }
            }

            foreach (var meridiemEvent in eventsToday)
            {
                if (meridiemEvent.evt == null || (meridiemEvent.evt is UnityEngine.Object uo && uo == null))
                {
                    continue;
                }

                var layout = eventLayouts[meridiemEvent];
                float trackIndex = layout.track;
                float totalTracks = layout.totalTracks;

                VisualElement eventElement = new VisualElement();
                eventElement.pickingMode = PickingMode.Ignore;
                Label label = new Label();
                label.tooltip = meridiemEvent.evt.EventDescription;
                label.RegisterCallback<ClickEvent>(evt =>
                {
                    if (meridiemEvent.evt is StaticEvent)
                        Selection.activeObject = (StaticEvent)meridiemEvent.evt;
                });
                eventElement.RegisterCallback<ClickEvent>(evt =>
                {
                    if (meridiemEvent.evt is StaticEvent)
                        Selection.activeObject = (StaticEvent)meridiemEvent.evt;
                });
                UpdateEventClass(eventElement, meridiemEvent.evt.EventType);
                eventElement.Add(label);

                // Calculate normalized start and end times
                float startTimeNormalized = meridiemEvent.startTime / 24f;
                float endTimeNormalized = meridiemEvent.endTime / 24f;

                float totalMargin = (totalTracks > 1) ? (totalTracks - 1) * horizontalMarginPercent : 0f;
                float eventWidth = (100f - totalMargin) / totalTracks;
                float eventLeft = trackIndex * (eventWidth + horizontalMarginPercent);

                eventElement.style.top = new Length(startTimeNormalized * 100f, LengthUnit.Percent);
                eventElement.style.height = new Length((endTimeNormalized - startTimeNormalized) * 100f - 1, LengthUnit.Percent);
                eventElement.style.left = new Length(eventLeft, LengthUnit.Percent);
                eventElement.style.width = new Length(eventWidth, LengthUnit.Percent);

                if (eventWidth > 35)
                    label.text = $"{meridiemEvent.evt.EventName} | {meridiemEvent.evt.StartTime.ToString()}";
                else
                    label.text = $"{meridiemEvent.evt.EventName}";

                DailyEventContainer.Add(eventElement);
            }
        }

        public void UpdateEventClass(VisualElement element, MeridiemEventType eventType)
        {
            element.AddToClassList("event");
            switch (eventType)
            {
                case MeridiemEventType.Weather:
                    element.AddToClassList("weather");
                    break;
                case MeridiemEventType.Ambient:
                    element.AddToClassList("ambient");
                    break;
                case MeridiemEventType.Holiday:
                    element.AddToClassList("holiday");
                    break;
                case MeridiemEventType.NPC:
                    element.AddToClassList("npc");
                    break;
                case MeridiemEventType.Enemy:
                    element.AddToClassList("enemy");
                    break;
                case MeridiemEventType.Player:
                    element.AddToClassList("player");
                    break;
                case MeridiemEventType.Status:
                    element.AddToClassList("status");
                    break;
                case MeridiemEventType.WorldEvent:
                    element.AddToClassList("worldevent");
                    break;
                default:
                    break;
            }
        }

        public void SelectDay(MeridiemDate date)
        {
            timeModule.CurrentDate = date;
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            timeModule.GetEventsOnDay(timeModule.CurrentDate);
            currentDay.BindProperty(serializedObject.FindProperty("currentDate"));
            UpdateMonthCalendar();
            UpdateDaySchedule();
        }

        public void SetTime(object time)
        {
            timeModule.CurrentTime = (float)time;
        }

        public override void AddContextMenuItems(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Set Time to Morning"), false, SetTime, 0.25f);
            menu.AddItem(new GUIContent("Set Time to Day"), false, SetTime, 0.5f);
            menu.AddItem(new GUIContent("Set Time to Evening"), false, SetTime, 0.75f);
            menu.AddItem(new GUIContent("Set Time to Night"), false, SetTime, 0f);
            menu.AddSeparator("");
        }

        public override void OpenDocumentationURL()
        {
            Application.OpenURL("https://distant-lands.gitbook.io/cozy-stylized-weather-documentation/how-it-works/modules/time-module");
        }

    }
}