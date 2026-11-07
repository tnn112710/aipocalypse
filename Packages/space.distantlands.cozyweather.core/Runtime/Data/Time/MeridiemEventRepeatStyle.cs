//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DistantLands.Cozy
{
    [Serializable]
    public class MeridiemEventRepeatStyle
    {
        private static List<Type> _MeridiemEventRepeatStyles;
        private static List<string> _MeridiemEventRepeatStyleNames;
        public static List<Type> MeridiemEventRepeatStyles
        {
            get
            {
                if (_MeridiemEventRepeatStyles == null || _MeridiemEventRepeatStyles.Count == 0 || _MeridiemEventRepeatStyleNames == null || _MeridiemEventRepeatStyleNames.Count == 0)
                {
                    _MeridiemEventRepeatStyles = new List<Type>();
                    _MeridiemEventRepeatStyleNames = new List<string>();

                    Assembly currentAssembly = Assembly.GetExecutingAssembly();

                    var repeatStyleTypes = currentAssembly.GetTypes()
                        .Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(MeridiemEventRepeatStyle)));

                    foreach (var type in repeatStyleTypes)
                    {
                        try
                        {
                            var instance = Activator.CreateInstance(type) as MeridiemEventRepeatStyle;

                            if (instance != null && !string.IsNullOrEmpty(instance.DisplayName))
                            {
                                _MeridiemEventRepeatStyleNames.Add(instance.DisplayName);
                            }

                            _MeridiemEventRepeatStyles.Add(type);
                        }
                        catch
                        {

                        }
                    }
                }

                return _MeridiemEventRepeatStyles;
            }
        }
        public static List<string> RepeatTypeNames
        {
            get
            {
                if (_MeridiemEventRepeatStyles == null || _MeridiemEventRepeatStyles.Count == 0 || _MeridiemEventRepeatStyleNames == null || _MeridiemEventRepeatStyleNames.Count == 0)
                {
                    _MeridiemEventRepeatStyles = new List<Type>();
                    _MeridiemEventRepeatStyleNames = new List<string>();

                    Assembly currentAssembly = Assembly.GetExecutingAssembly();

                    var repeatStyleTypes = currentAssembly.GetTypes()
                        .Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(MeridiemEventRepeatStyle)));

                    foreach (var type in repeatStyleTypes)
                    {
                        var instance = Activator.CreateInstance(type) as MeridiemEventRepeatStyle;

                        if (instance != null && !string.IsNullOrEmpty(instance.DisplayName))
                        {
                            _MeridiemEventRepeatStyleNames.Add(instance.DisplayName);
                        }

                        _MeridiemEventRepeatStyles.Add(type);
                    }
                }

                return _MeridiemEventRepeatStyleNames;
            }
        }


        /// <summary>
        /// The display name of this repeat style in the editor. Categories are separated with a slash ("/").
        /// </summary>
        public virtual string DisplayName => "";
        /// <summary>
        /// Should events be limited to 24 hours long? This is required for events that might repeat daily.
        /// </summary>
        public virtual bool LimitTo24Hour => false;
        /// <summary>
        /// Is this event linked to a calender day (such as January 1st), or is it linked to a something else (e.g. every day, every weekday, or every full moon)
        /// </summary>
        public virtual bool LinkedToDay => true;
        /// <summary>
        /// Returns the closest start and end date offsets based on an event and a date. For example, if an event occurs every January 1st, startDateOffset and endDateOffset are the number of days from date till the event.
        /// If start date offset is negative or 0 and end date offset is positive or 0, we can assume that the event occurs on the current day.
        /// </summary>
        public virtual void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            startDateOffset = 0;
            endDateOffset = 0;
        }
    }

    [Serializable]
    public class RepeatDaily : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Interval/Daily";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            startDateOffset = 0;
            endDateOffset = 0;
        }
    }

    [Serializable]
    public class RepeatNever : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Never";
        public override bool LimitTo24Hour => false;
        public override bool LinkedToDay => true;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            startDateOffset = evt.StartDate - date;
            endDateOffset = evt.EndDate - date;
        }
    }

    [Serializable]
    public class RepeatWeekly : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Interval/Weekly";
        public override bool LimitTo24Hour => false;
        public override bool LinkedToDay => true;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            startDateOffset = (date - evt.StartDate) % 7;
            endDateOffset = (date - evt.EndDate) % 7;
        }
    }

    [Serializable]
    public class RepeatMonthly : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Interval/Monthly";
        public override bool LimitTo24Hour => false;
        public override bool LinkedToDay => true;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            MeridiemDate relativeStartDate = new(date.month, evt.StartDate.day, 0);
            MeridiemDate relativeEndDate = new(date.month, evt.EndDate.day, 0);
            startDateOffset = relativeStartDate - date;
            endDateOffset = relativeEndDate - date;
        }
    }

    [Serializable]
    public class RepeatAnnually : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Interval/Annually";
        public override bool LimitTo24Hour => false;
        public override bool LinkedToDay => true;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            MeridiemDate relativeStartDate = new(evt.StartDate.month, evt.StartDate.day, date.year);
            MeridiemDate relativeEndDate = new(evt.EndDate.month, evt.EndDate.day, date.year);
            startDateOffset = relativeStartDate - date;
            endDateOffset = relativeEndDate - date;
        }
    }

    [Serializable]
    public class RepeatDailyOnWeekdays : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Day Type/Daily on Weekdays";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            switch (date.Weekday)
            {
                case DayOfWeek.Sunday:
                    startDateOffset = 1;
                    endDateOffset = 1;
                    break;
                case DayOfWeek.Saturday:
                    startDateOffset = -1;
                    endDateOffset = evt.Overnight ? 0 : -1;
                    break;
                case DayOfWeek.Monday:
                    startDateOffset = 0;
                    endDateOffset = evt.Overnight ? 1 : 0;
                    break;
                default:
                    startDateOffset = 0;
                    endDateOffset = 0;
                    break;
            }
        }
    }
    [Serializable]
    public class RepeatDailyOnWeekends : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Day Type/Daily on Weekends";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            int wrappedWeekday = ((int)date.Weekday - 1) % 7;
            switch (date.Weekday)
            {
                case DayOfWeek.Sunday:
                case DayOfWeek.Saturday:
                    startDateOffset = 0;
                    endDateOffset = 0;
                    break;
                case DayOfWeek.Monday:
                    startDateOffset = evt.Overnight ? -1 : wrappedWeekday - 5;
                    endDateOffset = evt.Overnight ? 0 : wrappedWeekday - 5;
                    break;
                default:
                    startDateOffset = wrappedWeekday - 5;
                    endDateOffset = wrappedWeekday - (evt.Overnight ? 5 : 6);
                    break;
            }
        }
    }

    [Serializable]
    public class RepeatEveryXWeekdayOfTheMonth : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Day Type/Every X Weekday of the Month";
        public override bool LimitTo24Hour => false;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {

            int weekdayOffset = (int)new MeridiemDate(date.month, 0, date.year).Weekday;
            int targetDay = -weekdayOffset + (int)dayOfWeek + ((week + ((int)dayOfWeek < weekdayOffset ? 1 : 0)) * 7);

            startDateOffset = targetDay - date.day;
            endDateOffset = targetDay + eventLengthInDays - 1 + (evt.Overnight ? 1 : 0) - date.day;

        }

        public int week;
        public int eventLengthInDays = 1;
        public DayOfWeek dayOfWeek;
    }

    [Serializable]
    public class RepeatLastDayOfTheMonth : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Day Type/Last Day of the Month";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            startDateOffset = CozyPreferences.Months[date.month - 1].days - date.day;
            endDateOffset = evt.Overnight && date.day == 0 ? 0 : startDateOffset;

        }
    }

    [Serializable]
    public class RepeatFirstDayOfTheMonth : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Day Type/First Day of the Month";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            startDateOffset = 1 - date.day;
            endDateOffset = evt.Overnight && date.day == 1 ? 0 : startDateOffset;

        }
    }

    [Serializable]
    public class RepeatEveryFullMoon : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Celestial/By Moon Phase";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            if (CozyWeather.Instance &&
                CozyWeather.Instance.GetModule(out CozyAtmosphereModule atmosphereModule) &&
                CozyWeather.Instance.GetModule(out CozyTimeModule timeModule))
            {
                float orbitPeriod = atmosphereModule.Parameters.skyMoonDiskOrbitPeriod;
                if (CozyPreferences.CurrentStyle.UseSkyMoonDisk &&
                    atmosphereModule.Parameters.skyMoonDiskPhaseMode != MoonPhaseMode.None &&
                    !CozyPreferences.StaticLighting &&
                    orbitPeriod > 0f && !float.IsNaN(orbitPeriod) && !float.IsInfinity(orbitPeriod))
                {
                    float eventTime = timeModule.GetModifiedTime(evt.StartTime);
                    float currentPhase = Mathf.Repeat(
                        date.AbsoluteDay + eventTime + atmosphereModule.Parameters.skyMoonDiskOrbitOffset,
                        orbitPeriod) / orbitPeriod;
                    float targetPhase = (int)moonPhase / 8f;
                    float daysToPhase = Mathf.Repeat(targetPhase - currentPhase, 1f) * orbitPeriod;
                    startDateOffset = Mathf.RoundToInt(daysToPhase);
                    endDateOffset = startDateOffset + (evt.Overnight ? 1 : 0);

                    return;
                }
            }

            startDateOffset = -1;
            endDateOffset = -1;
        }

        public MoonPhase moonPhase;
    }

    [Serializable]
    public class RepeatEverySolarEclipse : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Celestial/Eclipse";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            if (CozyWeather.Instance &&
                CozyWeather.Instance.GetModule(out CozyAtmosphereModule atmosphereModule) &&
                CozyWeather.Instance.GetModule(out CozyTimeModule timeModule))
            {
                AtmosphereParameters parameters = atmosphereModule.Parameters;
                float orbitPeriod = parameters.skyMoonDiskOrbitPeriod;
                float declinationPeriod = parameters.skyMoonDiskDeclinationPeriod;
                if (CozyPreferences.CurrentStyle.UseSkyMoonDisk &&
                    parameters.skyMoonDiskPlacementMode == MoonPlacementMode.Realistic &&
                    parameters.skyMoonDiskPhaseMode != MoonPhaseMode.None &&
                    !CozyPreferences.StaticLighting &&
                    orbitPeriod > 0f && !float.IsNaN(orbitPeriod) && !float.IsInfinity(orbitPeriod) &&
                    declinationPeriod > 0f && !float.IsNaN(declinationPeriod) && !float.IsInfinity(declinationPeriod))
                {
                    int searchDays = Mathf.CeilToInt(Mathf.Min(3650f, declinationPeriod * 2f + orbitPeriod));
                    float eventTime = timeModule.GetModifiedTime(evt.StartTime);
                    for (int dayOffset = 0; dayOffset <= searchDays; dayOffset++)
                    {
                        int day = date.AbsoluteDay + dayOffset;
                        atmosphereModule.GetCelestialDirections(day, eventTime, out Vector3 sunDirection, out Vector3 moonDirection);
                        float alignmentAngle = Vector3.Angle(sunDirection, moonDirection);
                        float daylight = Mathf.Clamp01(1f - sunDirection.y * 5f);
                        if (alignmentAngle <= 5.7f && daylight > 0f)
                        {
                            startDateOffset = dayOffset;
                            endDateOffset = startDateOffset + (evt.Overnight ? 1 : 0);
                            return;
                        }
                    }
                }
            }

            startDateOffset = -1;
            endDateOffset = -1;
        }
    }

    [Serializable]
    public class RepeatEveryQuarter : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Interval/Quarterly";
        public override bool LimitTo24Hour => false;
        public override bool LinkedToDay => true;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {

            MeridiemDate relativeStartDate = new(evt.StartDate.month % (CozyPreferences.MonthsPerYear / 4), evt.StartDate.day, 0);
            MeridiemDate relativeEndDate = new(evt.EndDate.month % (CozyPreferences.MonthsPerYear / 4), evt.EndDate.day, 0);
            MeridiemDate modifiedDate = new(date.month % (CozyPreferences.MonthsPerYear / 4), date.day, 0);


            startDateOffset = relativeStartDate - modifiedDate;
            endDateOffset = relativeEndDate - modifiedDate;

        }
    }

    [Serializable]
    public class RepeatOnTheHottestDay : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Seasonal/Hottest Day of the Year";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            if (CozyWeather.Instance && CozyWeather.Instance.GetModule(out CozyClimateModule climateModule))
            {
                if (climateModule.climateProfile)
                {

                    startDateOffset = climateModule.HottestDayOfTheYear - date.AbsoluteDayInYear;
                    endDateOffset = startDateOffset + (evt.Overnight ? 1 : 0);

                    return;
                }
            }

            startDateOffset = -1;
            endDateOffset = -1;
        }
    }

    [Serializable]
    public class RepeatOnTheColdestDay : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Seasonal/Coldest Day of the Year";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            if (CozyWeather.Instance && CozyWeather.Instance.GetModule(out CozyClimateModule climateModule))
            {
                if (climateModule.climateProfile)
                {
                    startDateOffset = climateModule.ColdestDayOfTheYear - date.AbsoluteDayInYear;
                    endDateOffset = startDateOffset + (evt.Overnight ? 1 : 0);
                    return;
                }
            }

            startDateOffset = -1;
            endDateOffset = -1;
        }
    }

    [Serializable]
    public class RepeatOnFirstDayOfSpring : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Seasonal/Spring/First Day";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            if (CozyWeather.Instance && CozyWeather.Instance.GetModule(out CozyClimateModule climateModule))
            {
                if (climateModule.climateProfile)
                {

                    startDateOffset = climateModule.climateProfile.spring.startDay - date.AbsoluteDayInYear;
                    endDateOffset = startDateOffset + (evt.Overnight ? 1 : 0);

                    return;
                }
            }

            startDateOffset = -1;
            endDateOffset = -1;
        }
    }
    [Serializable]
    public class RepeatDailyInSpring : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Seasonal/Spring/Daily";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            if (CozyWeather.Instance && CozyWeather.Instance.GetModule(out CozyClimateModule climateModule))
            {
                if (climateModule.climateProfile)
                {
                    if (date.AbsoluteDayInYear < climateModule.climateProfile.spring.startDay || date.AbsoluteDayInYear >= climateModule.climateProfile.summer.startDay)
                    {
                        startDateOffset = climateModule.climateProfile.spring.startDay - date.AbsoluteDayInYear;
                        endDateOffset = startDateOffset + (evt.Overnight ? 1 : 0);
                    }
                    else
                    {
                        startDateOffset = 0;
                        endDateOffset = 0;
                    }

                    return;
                }
            }

            startDateOffset = -1;
            endDateOffset = -1;
        }
    }
    [Serializable]
    public class RepeatOnFirstDayOfSummer : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Seasonal/Summer/First Day";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            if (CozyWeather.Instance && CozyWeather.Instance.GetModule(out CozyClimateModule climateModule))
            {
                if (climateModule.climateProfile)
                {

                    startDateOffset = climateModule.climateProfile.summer.startDay - date.AbsoluteDayInYear;
                    endDateOffset = startDateOffset + (evt.Overnight ? 1 : 0);

                    return;
                }
            }

            startDateOffset = -1;
            endDateOffset = -1;
        }
    }
    [Serializable]
    public class RepeatDailyInSummer : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Seasonal/Summer/Daily";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            if (CozyWeather.Instance && CozyWeather.Instance.GetModule(out CozyClimateModule climateModule))
            {
                if (climateModule.climateProfile)
                {
                    if (date.AbsoluteDayInYear < climateModule.climateProfile.summer.startDay || date.AbsoluteDayInYear >= climateModule.climateProfile.fall.startDay)
                    {
                        startDateOffset = climateModule.climateProfile.summer.startDay - date.AbsoluteDayInYear;
                        endDateOffset = startDateOffset + (evt.Overnight ? 1 : 0);
                    }
                    else
                    {
                        startDateOffset = 0;
                        endDateOffset = 0;
                    }

                    return;
                }
            }

            startDateOffset = -1;
            endDateOffset = -1;
        }
    }
    [Serializable]
    public class RepeatOnFirstDayOfFall : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Seasonal/Fall/First Day";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            if (CozyWeather.Instance && CozyWeather.Instance.GetModule(out CozyClimateModule climateModule))
            {
                if (climateModule.climateProfile)
                {

                    startDateOffset = climateModule.climateProfile.fall.startDay - date.AbsoluteDayInYear;
                    endDateOffset = startDateOffset + (evt.Overnight ? 1 : 0);

                    return;
                }
            }

            startDateOffset = -1;
            endDateOffset = -1;
        }
    }
    [Serializable]
    public class RepeatDailyInFall : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Seasonal/Fall/Daily";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            if (CozyWeather.Instance && CozyWeather.Instance.GetModule(out CozyClimateModule climateModule))
            {
                if (climateModule.climateProfile)
                {
                    if (date.AbsoluteDayInYear < climateModule.climateProfile.fall.startDay || date.AbsoluteDayInYear >= climateModule.climateProfile.winter.startDay)
                    {
                        startDateOffset = climateModule.climateProfile.fall.startDay - date.AbsoluteDayInYear;
                        endDateOffset = startDateOffset + (evt.Overnight ? 1 : 0);
                    }
                    else
                    {
                        startDateOffset = 0;
                        endDateOffset = 0;
                    }

                    return;
                }
            }

            startDateOffset = -1;
            endDateOffset = -1;
        }
    }
    [Serializable]
    public class RepeatOnFirstDayOfWinter : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Seasonal/Winter/First Day";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            if (CozyWeather.Instance && CozyWeather.Instance.GetModule(out CozyClimateModule climateModule))
            {
                if (climateModule.climateProfile)
                {

                    startDateOffset = climateModule.climateProfile.winter.startDay - date.AbsoluteDayInYear;
                    endDateOffset = startDateOffset + (evt.Overnight ? 1 : 0);

                    return;
                }
            }

            startDateOffset = -1;
            endDateOffset = -1;
        }
    }

    [Serializable]
    public class RepeatDailyInWinter : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Seasonal/Winter/Daily";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            if (CozyWeather.Instance && CozyWeather.Instance.GetModule(out CozyClimateModule climateModule))
            {
                if (climateModule.climateProfile)
                {
                    if (date.AbsoluteDayInYear < climateModule.climateProfile.spring.startDay || date.AbsoluteDayInYear >= climateModule.climateProfile.winter.startDay)
                    {
                        startDateOffset = 0;
                        endDateOffset = 0;
                    }
                    else
                    {
                        startDateOffset = climateModule.climateProfile.winter.startDay - date.AbsoluteDayInYear;
                        endDateOffset = startDateOffset + (evt.Overnight ? 1 : 0);
                    }

                    return;
                }
            }

            startDateOffset = -1;
            endDateOffset = -1;
        }
    }

    [Serializable]
    public class RepeatOnTheShortestDay : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Celestial/Shortest Day of the Year";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            if (CozyWeather.Instance && CozyWeather.Instance.GetModule(out CozyTransitModule transitModule))
            {
                startDateOffset = transitModule.WinterSolstice - date.AbsoluteDayInYear;
                endDateOffset = startDateOffset + (evt.Overnight ? 1 : 0);
                return;
            }

            startDateOffset = -1;
            endDateOffset = -1;
        }
    }

    [Serializable]
    public class RepeatOnTheLongestDay : MeridiemEventRepeatStyle
    {
        public override string DisplayName => "Celestial/Longest Day of the Year";
        public override bool LimitTo24Hour => true;
        public override bool LinkedToDay => false;
        public override void GetRelativeStartDate(MeridiemDate date, IMeridiemEvent evt, out int startDateOffset, out int endDateOffset)
        {
            if (CozyWeather.Instance && CozyWeather.Instance.GetModule(out CozyTransitModule transitModule))
            {
                startDateOffset = transitModule.SummerSolstice - date.AbsoluteDayInYear;
                endDateOffset = startDateOffset + (evt.Overnight ? 1 : 0);
                return;
            }

            startDateOffset = -1;
            endDateOffset = -1;
        }
    }

}