//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Determine how and when weather should change.
    /// </summary>
    public enum WeatherChangeMode
    {
        ChangeWhenWeatherFinishes,
        ChangeDaily,
        ChangeViaScripting,
    }
    /// <summary>
    /// Determine how and when weather should change.
    /// </summary>
    public enum NextWeatherSelectionMode
    {
        Forecast,
        WeightedRandom,
        Random,
        ChangeViaScripting,
    }
    /// <summary>
    /// Determine the weather behavior when the game starts
    /// </summary>
    public enum WeatherInitializationBehavior
    {
        SetWeather,
        SetRandomWeather,
        SetForecast,
    }

    /// <summary>
    /// Contains a forecastable and a duration that it will play for.
    /// </summary>
    [Serializable]
    public struct WeatherPattern
    {
        public WeatherProfile profile;
        public MeridiemTimeDelta duration;
        [NonSerialized]
        public (MeridiemDate date, MeridiemTime time) startTime;
        [NonSerialized]
        public (MeridiemDate date, MeridiemTime time) endTime;
        public WeatherPattern(WeatherProfile profile, MeridiemTimeDelta duration, (MeridiemDate date, MeridiemTime time) startTime, (MeridiemDate date, MeridiemTime time) endTime)
        {
            this.profile = profile;
            this.duration = duration;
            this.startTime = startTime;
            this.endTime = endTime;
        }
    }

    public interface IWeatherModule : IStateMachineModule<WeatherProfile>
    {
        /// <summary>
        /// Main profile that controls the forecast management
        /// </summary>
        public ForecastProfile ForecastProfile { get; set; }
        /// <summary>
        /// Determine the mode used for when the system changes to the next weather profile
        /// </summary>
        public WeatherChangeMode WeatherChangeMode { get; set; }
        /// <summary>
        /// Determine how weather will initialize when the game starts
        /// </summary>
        public WeatherInitializationBehavior WeatherInitializationBehavior { get; }
        /// <summary>
        /// Determine the starting weather for the simulation
        /// </summary>
        public WeatherProfile StartingWeather { get; }
        /// <summary>
        /// Determine the starting forecast for the simulation
        /// </summary>
        public WeatherProfile[] StartingForecast { get; }
        /// <summary>
        /// Determine the mode used for selecting the next weather profile
        /// </summary>
        public NextWeatherSelectionMode NextWeatherSelectionMode { get; }

        /// <summary>
        /// The forecast is the list of weather profiles that will be played in sequence over time
        /// </summary>
        public List<WeatherPattern> CurrentForecast { get; }

        /// <summary>
        /// Adds this profile to the weather queue to be played after the current weather finishes
        /// </summary>
        /// <param name="profile">The profile that will be set as the next profile.</param>
        /// <returns>Returns true if the operation completes successfully</returns>
        public bool QueueWeather(WeatherProfile profile);
        /// <summary>
        /// Adds this profile to the weather queue to be played after the current weather finishes
        /// </summary>
        /// <param name="profile">The profile that will be set as the next profile.</param>
        /// <param name="duration">Sets how long this profile will be scheduled for.</param>
        /// <returns>Returns true if the operation completes successfully</returns>
        public bool QueueWeather(WeatherProfile profile, MeridiemTimeDelta duration);

    }
}