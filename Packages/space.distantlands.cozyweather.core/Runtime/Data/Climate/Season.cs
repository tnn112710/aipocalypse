//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine;

namespace DistantLands.Cozy
{
    [Serializable]
    public struct DailyRange<T, D> where T : IClimateProperty<T, D> where D : IClimatePropertyDelta<T>
    {
        public T high;
        public T low;
        public D variationAmplitude;
        [Tooltip("Modulates the value throughout the day. the x-axis is the time in a 0-1 range (midnight to midnight) and the y-axis is the value that lerps between the daily low and high (0 being low and 1 being high).")]
        public AnimationCurve diurnalCurve;
        public float variationPeriod;
        public enum VariationType
        {
            Sine,
            Noise,
            None
        }
        public VariationType variationType;

        public readonly float GetVariation(MeridiemDate date, int seed)
        {
            switch (variationType)
            {
                case VariationType.Noise:
                    return Mathf.PerlinNoise(date.AbsoluteDay / variationPeriod, seed + CozyWeather.Seed) * 2 - 1;
                case VariationType.Sine:
                    return Mathf.Sin(date.AbsoluteDay / variationPeriod * Mathf.PI + ((seed + CozyWeather.Seed) / Mathf.PI));
                default: return 0;
            }
        }
    }
    /// <summary>
    /// Holds climate range information for a given period of time. 
    /// </summary>
    [Serializable]
    public struct ClimateRanges
    {
        /// <summary>
        /// Sets the daily range for temperature this season.
        /// </summary>
        public DailyRange<Temperature, TemperatureDelta> temperature;
        /// <summary>
        /// Sets the daily range for humidity this season.
        /// </summary>
        public DailyRange<Humidity, HumidityDelta> humidity;
        /// <summary>
        /// Sets the daily range for pressure this season.
        /// </summary>
        public DailyRange<Pressure, PressureDelta> pressure;
        /// <summary>
        /// Sets the daily range for the UV index this season.
        /// </summary>
        public DailyRange<UVIndex, UVIndexDelta> uvIndex;
        /// <summary>
        /// Sets the daily range for the air quality this season.
        /// </summary>
        public DailyRange<AirQuality, AirQualityDelta> airQuality;
        /// <summary>
        /// Sets the stratiform (lighter, longer storms) rain chance for this seasons. Only used in seasonal rain chance calculation mode (see ClimateModule).
        /// </summary>
        public PrecipitationChance stratiformRainChance;
        /// <summary>
        /// Sets the convective (heavier, faster storms) rain chance for this seasons. Only used in seasonal rain chance calculation mode (see ClimateModule).
        /// </summary>
        public PrecipitationChance convectiveRainChance;
        /// <summary>
        /// The name of the season for this range.
        /// </summary>
        public SeasonName season;
    }

    public struct ClimateSnapshot
    {
        /// <summary>
        /// The current temperature based on this snapshot
        /// </summary>
        public Temperature temperature;
        /// <summary>
        /// The current humidity based on this snapshot
        /// </summary>
        public Humidity humidity;
        /// <summary>
        /// The current pressure based on this snapshot
        /// </summary>
        public Pressure pressure;
        /// <summary>
        /// The current UV index based on this snapshot
        /// </summary>
        public UVIndex uvIndex;
        /// <summary>
        /// The current air quality based on this snapshot
        /// </summary>
        public AirQuality airQuality;
        /// <summary>
        /// The current stratiform rain chance based on this snapshot
        /// </summary>
        public PrecipitationChance stratiformRainChance;
        /// <summary>
        /// The current convective rain chance based on this snapshot
        /// </summary>
        public PrecipitationChance convectiveRainChance;
        public SeasonName season;
    }
    [Serializable]
    public struct Season
    {
        /// <summary>
        /// Sets the starting day for the season. 
        /// </summary>
        [DayOfYear]
        public MeridiemDate startDay;
        public ClimateRanges climateInformation;
    }

    public enum SeasonName
    {
        Spring,
        Summer,
        Fall,
        Winter
    }
}