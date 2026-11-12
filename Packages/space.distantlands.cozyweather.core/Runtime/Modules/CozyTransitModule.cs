//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine;

namespace DistantLands.Cozy
{
    [ExecuteAlways]
    [AddComponentMenu("")]
    [CozyModule("Transit", "Transit", "Control the sun's movement through the sky.", "Time")]
    public class CozyTransitModule : CozyModule
    {

        [Serializable]
        public struct TimeWeightRelation
        {
            public MeridiemTime time; [Range(0, 360)] public float sunHeight; [Range(0, 1)] public float weight;

            public TimeWeightRelation(MeridiemTime time, float sunHeight, float weight)
            {
                this.time = time;
                this.sunHeight = sunHeight;
                this.weight = weight;
            }
        }
        [HideInInspector]
        public AnimationCurve sunMovementCurve;

        [Tooltip("Specifies the default weight of the sunrise.")]
        [CozySearchable]
        public TimeWeightRelation sunriseWeight = new(new MeridiemTime(8, 00, Meridiem.AM), 90, 0.2f);
        [Tooltip("Specifies the default weight of the day.")]
        [CozySearchable]
        public TimeWeightRelation dayWeight = new(new MeridiemTime(1, 00, Meridiem.PM), 180, 0.2f);
        [Tooltip("Specifies the default weight of the sunset.")]
        [CozySearchable]
        public TimeWeightRelation sunsetWeight = new(new MeridiemTime(7, 00, Meridiem.PM), 270, 0.2f);
        [Tooltip("Specifies the default weight of the night.")]
        [CozySearchable]
        public TimeWeightRelation nightWeight = new(1, 360, 0.2f);

        [Tooltip("Specifies the day length multiplier in the spring.")]
        [Range(-1, 1)]
        [CozySearchable]
        public float springDayLengthOffset = 0;
        [Tooltip("Specifies the day length multiplier in the summer.")]
        [Range(-1, 1)]
        [CozySearchable]
        public float summerDayLengthOffset = 0.3f;
        [Tooltip("Specifies the day length multiplier in the fall.")]
        [Range(-1, 1)]
        [CozySearchable]
        public float fallDayLengthOffset = 0;
        [Tooltip("Specifies the day length multiplier in the winter.")]
        [Range(-1, 1)]
        [CozySearchable]
        public float winterDayLengthOffset = -0.3f;


        public AnimationCurve yearWeightsCurve;
        public enum TimeCurveSettings { linearDay, simpleCurve, advancedCurve }
        public TimeCurveSettings timeCurveSettings = TimeCurveSettings.advancedCurve;

        /// <summary>
        /// Get the sunrise time today. Updates when day changes
        /// </summary>
        public MeridiemTime SunriseTime => sunriseTime;
        /// <summary>
        /// Get the sunset time today. Updates when day changes
        /// </summary>
        public MeridiemTime SunsetTime => sunsetTime;
        private MeridiemTime sunriseTime;
        private MeridiemTime sunsetTime;

        [Serializable]
        public class TimeBlock
        {
            public MeridiemTime start;
            public MeridiemTime end;
            public TimeBlock(float startDayPercentage, float endDayPercentage)
            {
                start = startDayPercentage;
                end = endDayPercentage;
            }

        }

        public void GetModifiedDayPercent()
        {

            yearWeightsCurve = new AnimationCurve(new Keyframe[5]
            {
                new(0, winterDayLengthOffset, 0, 0),
                new(0.25f, springDayLengthOffset, 0, 0),
                new(0.5f, summerDayLengthOffset, 0, 0),
                new(0.75f, fallDayLengthOffset, 0, 0),
                new(1, winterDayLengthOffset, 0, 0)
            });

            float offset = 0;
            if (WeatherSphere.Calendar != null)
                offset = yearWeightsCurve.Evaluate(WeatherSphere.Calendar.CurrentDate.YearPercentage) / 5;

            switch (timeCurveSettings)
            {

                case TimeCurveSettings.advancedCurve:
                    sunMovementCurve = new AnimationCurve(new Keyframe[5]
                    {
                        new(0, 0, 0, 0, nightWeight.weight, nightWeight.weight),
                        new((float)sunriseWeight.time - offset, sunriseWeight.sunHeight, 0, 0, sunriseWeight.weight, sunriseWeight.weight),
                        new(dayWeight.time, dayWeight.sunHeight, 0, 0, dayWeight.weight, dayWeight.weight),
                        new((float)sunsetWeight.time + offset, sunsetWeight.sunHeight, 0, 0, sunsetWeight.weight, sunsetWeight.weight),
                        new(1, sunsetWeight.sunHeight > dayWeight.sunHeight ? 360 : 0, 0, 0, nightWeight.weight, nightWeight.weight)
                    });
                    break;

                case TimeCurveSettings.simpleCurve:
                    sunMovementCurve = new AnimationCurve(new Keyframe[5]
                    {
                new(0, 0, 0, 0, nightWeight.weight, nightWeight.weight),
                new((float)sunriseWeight.time - offset, 90f, 0, 0, sunriseWeight.weight, sunriseWeight.weight),
                new(dayWeight.time, 180f, 0, 0, dayWeight.weight, dayWeight.weight),
                new((float)sunsetWeight.time + offset, 270f, 0, 0, sunsetWeight.weight, sunsetWeight.weight),
                new(1, 360, 0, 0, nightWeight.weight, nightWeight.weight)
                    });
                    break;

                case TimeCurveSettings.linearDay:
                    sunMovementCurve = new AnimationCurve(new Keyframe[5]
                    {
                new(0, 0, 0, 0, 0, 0),
                new((float)sunriseWeight.time - offset, 90, 0, 0, 0, 0),
                new(dayWeight.time, 180, 0, 0, 0, 0),
                new((float)sunsetWeight.time + offset, 270, 0, 0, 0, 0),
                new(1, 360, 0, 0, 0, 0)
                    });
                    break;
            }
        }

        public void CalculateSunriseAndSunset()
        {
            yearWeightsCurve = new AnimationCurve(new Keyframe[5]
            {
                new(0, winterDayLengthOffset, 0, 0),
                new(0.25f, springDayLengthOffset, 0, 0),
                new(0.5f, summerDayLengthOffset, 0, 0),
                new(0.75f, fallDayLengthOffset, 0, 0),
                new(1, winterDayLengthOffset, 0, 0)
            });

            float offset = 0;

            if (WeatherSphere.Calendar != null)
                offset = yearWeightsCurve.Evaluate(WeatherSphere.Calendar.CurrentDate.YearPercentage) / 5;

            sunriseTime = 0.25f - offset;
            sunsetTime = 0.75f + offset;

            if (timeCurveSettings == TimeCurveSettings.advancedCurve)
            {
                sunriseTime = (float)sunriseWeight.time - offset;
                sunsetTime = sunsetWeight.time + offset;
            }
        }

        public MeridiemDate WinterSolstice
        {
            get
            {
                return new MeridiemDate(12, 21, 0000);
            }
        }
        public MeridiemDate SummerSolstice
        {
            get
            {
                return new MeridiemDate(6, 20, 0000);
            }
        }

        public override Type[] ModuleRequirements => DependsOn<CozyTimeModule>();

        private ITimeModule timeModule;
        private ICalendarModule calendarModule;

        public override void InitializeModule()
        {
            if (timeModule != null)
                timeModule.OnModifyTime -= ModifyDayPercentage;
            if (calendarModule != null)
                calendarModule.OnChangeDay -= OnDayChanged;
            timeModule = WeatherSphere.Time;
            calendarModule = WeatherSphere.Calendar;
            if (timeModule != null)
            {
                timeModule.OnModifyTime += ModifyDayPercentage;
            }

            if (calendarModule != null)
            {
                calendarModule.OnChangeDay += OnDayChanged;
            }

            CalculateSunriseAndSunset();
        }

        public override void DeinitializeModule()
        {

            if (timeModule != null)
                timeModule.OnModifyTime -= ModifyDayPercentage;
            if (calendarModule != null)
                calendarModule.OnChangeDay -= OnDayChanged;
            timeModule = null;
            calendarModule = null;
        }

        private void OnDayChanged()
        {
            GetModifiedDayPercent();
            CalculateSunriseAndSunset();
        }


        void Start()
        {
            GetModifiedDayPercent();
        }

        public float ModifyDayPercentage(float input)
        {
            return sunMovementCurve.Evaluate(input) / 360;
        }
    }
}