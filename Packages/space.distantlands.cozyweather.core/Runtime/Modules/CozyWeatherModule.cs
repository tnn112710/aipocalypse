//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace DistantLands.Cozy
{
    [ExecuteAlways]
    [AddComponentMenu("")]
    [CozyModule("Weather", "Partly Cloudy", "Control the weather patterns", "Ecosystem", true)]
    public class CozyWeatherModule : CozyStateMachineModule<WeatherProfile>, IWeatherModule
    {
        public ForecastProfile ForecastProfile
        {
            get
            {
                return forecastProfile;
            }
            set
            {
                forecastProfile = value;
            }
        }
        [SerializeField]
        private ForecastProfile forecastProfile;

        public WeatherChangeMode WeatherChangeMode { get => weatherChangeMode; set => weatherChangeMode = value; }
        [SerializeField]
        private WeatherChangeMode weatherChangeMode = WeatherChangeMode.ChangeWhenWeatherFinishes;

        public WeatherInitializationBehavior WeatherInitializationBehavior => weatherInitializationBehavior;
        [SerializeField]
        private WeatherInitializationBehavior weatherInitializationBehavior = WeatherInitializationBehavior.None;

        public NextWeatherSelectionMode NextWeatherSelectionMode => nextWeatherSelectionMode;
        [SerializeField]
        private NextWeatherSelectionMode nextWeatherSelectionMode = NextWeatherSelectionMode.Forecast;

        public MeridiemTime WeatherChangeTime => weatherChangeTime;
        [SerializeField]
        private MeridiemTime weatherChangeTime;

        public WeatherProfile StartingWeather => startingWeather;
        [SerializeReference]
        private WeatherProfile startingWeather;
        [SerializeField, HideInInspector]
        private WeatherProfile editorPreviewProfile;
        public WeatherProfile EditorPreviewProfile
        {
            get => editorPreviewProfile;
            set => editorPreviewProfile = value;
        }
        public WeatherProfile[] StartingForecast => startingForecast;
        [SerializeReference]
        private WeatherProfile[] startingForecast;

        public List<WeatherPattern> CurrentForecast => currentForecast;
        private List<WeatherPattern> currentForecast;

        public override bool UpdateStateMachineTimer => weatherChangeMode == WeatherChangeMode.ChangeWhenWeatherFinishes;

        public override List<WeatherProfile> GetAllProfiles()
        {
            var profiles = new List<WeatherProfile>();
            if (forecastProfile != null && forecastProfile.profiles != null)
                foreach (var forecastable in forecastProfile.profiles)
                    if (forecastable.profile) profiles.Add(forecastable.profile);
            if (startingWeather) profiles.Add(startingWeather);
            if (startingForecast != null)
                foreach (var profile in startingForecast)
                    if (profile) profiles.Add(profile);
            return profiles;
        }

        private ITimeModule timeModule;
        private ICalendarModule calendarModule;
        private long previousDailyTime;
        private bool hasDailyTime;
        private bool warnedAboutDailyTime;

        public override void InitializeModule()
        {
            hasDailyTime = false;
            warnedAboutDailyTime = false;
            currentForecast = new List<WeatherPattern>();
            base.InitializeModule();

            WeatherSphere.GetModule(out timeModule);
            WeatherSphere.GetModule(out calendarModule);
        }

        public override void OnStateMachineUpdate()
        {
            if (!Application.isPlaying || weatherChangeMode != WeatherChangeMode.ChangeDaily)
            {
                hasDailyTime = false;
                return;
            }

            if (timeModule == null || calendarModule == null)
            {
                hasDailyTime = false;
                if (!warnedAboutDailyTime)
                {
                    Debug.LogWarning("Change Daily requires both a time module and a calendar module.", this);
                    warnedAboutDailyTime = true;
                }
                return;
            }

            warnedAboutDailyTime = false;
            const long millisecondsPerDay = 86400000;
            long now = calendarModule.CurrentDate.AbsoluteDay * millisecondsPerDay + timeModule.CurrentTime.ToTotalMilliseconds();
            if (hasDailyTime && now > previousDailyTime)
            {
                long changeTime = weatherChangeTime.ToTotalMilliseconds();
                if (Math.Floor((double)(now - changeTime) / millisecondsPerDay) >
                    Math.Floor((double)(previousDailyTime - changeTime) / millisecondsPerDay))
                    ChangeProfile();
            }

            previousDailyTime = now;
            hasDailyTime = true;
        }

        public override void SetupStateMachine()
        {
            if (!Application.isPlaying)
            {
                if (editorPreviewProfile)
                    SetProfile(editorPreviewProfile, resetTimer: false, transition: false);
                return;
            }

            switch (weatherInitializationBehavior)
            {
                case WeatherInitializationBehavior.SetForecast:
                    foreach (WeatherProfile weather in startingForecast)
                        QueueWeather(weather);
                    break;
                case WeatherInitializationBehavior.SetWeather:
                    if (startingWeather)
                        SetProfile(startingWeather, resetTimer: true, transition: false);
                    else
                    {
                        Debug.LogWarning("Starting weather is null. Forecasting random profile instead.");
                        SetProfile(forecastProfile.RandomWeatherProfile, resetTimer: true, transition: false);
                    }
                    break;
                case WeatherInitializationBehavior.SetRandomWeather:
                    SetProfile(forecastProfile.RandomWeatherProfile, resetTimer: true, transition: false);
                    break;
                case WeatherInitializationBehavior.None:
                    if (editorPreviewProfile)
                        SetProfile(editorPreviewProfile, resetTimer: true, transition: false);
                    else
                    {
                        Debug.LogWarning("Editor preview weather is null. Forecasting random profile instead.");
                        SetProfile(forecastProfile.RandomWeatherProfile, resetTimer: true, transition: false);
                    }
                    break;
                default:
                    break;
            }
        }

        #region Forecasting and Weather setters 

        public override WeatherProfile GetNextProfile(WeatherProfile lastProfile, ConditionSnapshot conditionSnapshot)
        {
            switch (nextWeatherSelectionMode)
            {
                case NextWeatherSelectionMode.Forecast:
                    PopulateForecast();
                    if (currentForecast.Count > 0)
                    {
                        var nextProfile = currentForecast[0].profile;
                        currentForecast.RemoveAt(0);
                        return nextProfile;
                    }
                    if (forecastProfile == null || forecastProfile.profiles.Count == 0)
                    {
                        Debug.LogWarning("Cannot change weather: the forecast has no profiles.", this);
                        return null;
                    }
                    return forecastProfile.profiles[Random.Range(0, forecastProfile.profiles.Count)].profile;
                case NextWeatherSelectionMode.Random:
                    if (forecastProfile == null || forecastProfile.profiles.Count == 0)
                    {
                        Debug.LogWarning("Cannot change weather: the forecast has no profiles.", this);
                        return null;
                    }
                    return forecastProfile.profiles[Random.Range(0, forecastProfile.profiles.Count)].profile;
                case NextWeatherSelectionMode.WeightedRandom:
                    if (forecastProfile == null || forecastProfile.profiles.Count == 0)
                    {
                        Debug.LogWarning("Cannot change weather: the forecast has no profiles.", this);
                        return null;
                    }
                    return GetNextWeatherProfile(lastProfile, conditionSnapshot);
                case NextWeatherSelectionMode.ChangeViaScripting:
                    Debug.LogWarning("Cannot automatically select weather when selection is controlled via scripting.", this);
                    return null;
            }
            Debug.LogWarning("Cannot change weather: the selection mode is not recognized.", this);
            return null;
        }

        public void PopulateForecast()
        {
            currentForecast ??= new List<WeatherPattern>();
            if (forecastProfile == null) return;

            int iterations = 0;

            while (currentForecast.Count < forecastProfile.forecastAheadAmount && iterations < forecastProfile.forecastAheadAmount + 20)
            {
                WeatherProfile lastProfile = CurrentLocalProfile;
                ConditionSnapshot conditionSnapshot = new();
                if (CurrentForecast != null && CurrentForecast.Count > 0)
                {
                    var lastEntry = CurrentForecast[^1];
                    lastProfile = lastEntry.profile;
                    conditionSnapshot = ConditionSnapshot.GetConditions(WeatherSphere, lastEntry.endTime.date, lastEntry.endTime.time);
                }
                else
                {
                    if (WeatherSphere.Time != null && WeatherSphere.Calendar != null)
                        conditionSnapshot = ConditionSnapshot.GetConditions(WeatherSphere, WeatherSphere.Calendar.CurrentDate, WeatherSphere.Time.CurrentTime);
                }
                QueueWeather(GetNextWeatherProfile(lastProfile, conditionSnapshot));

                iterations++;
            }
        }

        WeatherProfile GetNextWeatherProfile(WeatherProfile lastProfile, ConditionSnapshot conditionSnapshot)
        {
            var potentialNextProfiles = ForecastProfile.PotentialNextProfiles(lastProfile);
            if (potentialNextProfiles == null || potentialNextProfiles.Count == 0)
                return forecastProfile.RandomWeatherProfile;

            float totalWeight = 0f;
            List<(WeatherProfile profile, float chance)> validProfiles = new();

            foreach (var weather in potentialNextProfiles)
            {
                float chance = weather.profile.GetChance(conditionSnapshot);
                if (chance > 0)
                {
                    validProfiles.Add((weather.profile, chance));
                    totalWeight += chance;
                }
            }

            if (totalWeight <= 0f) return forecastProfile.RandomWeatherProfile;

            float randomVal = Random.Range(0f, totalWeight);
            foreach (var (profile, chance) in validProfiles)
            {
                randomVal -= chance;
                if (randomVal <= 0f) return profile;
            }

            return validProfiles[^1].profile;
        }

        public bool QueueWeather(WeatherProfile profile)
        {
            if (profile == null)
            {
                Debug.LogWarning("Could not forecast null profile");
                return false;
            }

            MeridiemTimeDelta duration = Random.Range(profile.MinTime, profile.MaxTime);

            return QueueWeather(profile, duration);
        }

        public bool QueueWeather(WeatherProfile profile, MeridiemTimeDelta duration)
        {
            if (duration == 0 || profile == null) return false;

            (MeridiemDate date, MeridiemTime time) start = (0, 0);
            if (currentForecast.Count > 0)
                start = currentForecast[^1].endTime;
            else
            {
                if (WeatherSphere.Calendar != null)
                    start.date = WeatherSphere.Calendar.CurrentDate;
                if (WeatherSphere.Time != null)
                    start.time = WeatherSphere.Time.CurrentTime;
            }

            (MeridiemDate date, MeridiemTime time) end = start;

            float totalEndDuration = start.time + (float)duration;
            end.date += Mathf.FloorToInt(totalEndDuration);
            end.time = totalEndDuration % 1.0f;

            currentForecast.Add(new WeatherPattern(profile, duration, start, end));
            return true;
        }

        #endregion

    }
}