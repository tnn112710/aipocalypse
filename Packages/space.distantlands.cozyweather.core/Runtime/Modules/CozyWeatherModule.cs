//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using System.Collections.Generic;

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
        private WeatherInitializationBehavior weatherInitializationBehavior = WeatherInitializationBehavior.SetRandomWeather;

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

        public override void InitializeModule()
        {
            base.InitializeModule();
            currentForecast = new List<WeatherPattern>();
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
                        currentForecast.RemoveAt(0);
                        return currentForecast[0].profile;
                    }
                    return forecastProfile.profiles[Random.Range(0, forecastProfile.profiles.Count)].profile;
                case NextWeatherSelectionMode.Random:
                    return forecastProfile.profiles[Random.Range(0, forecastProfile.profiles.Count)].profile;
                case NextWeatherSelectionMode.WeightedRandom:
                    return GetNextWeatherProfile(lastProfile, conditionSnapshot);
            }


            return forecastProfile.profiles[Random.Range(0, forecastProfile.profiles.Count)].profile;
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