//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace DistantLands.Cozy
{

    public enum ChanceConditionType
    {
        Temperature,
        Humidity,
        Pressure,
        UVIndex,
        AirQuality,
        AccumulatedSnow,
        AccumulatedRain,
        ConvectiveRainChance,
        StratiformRainChance,
        Season,
        YearPercentage,
        Time,
        WindSpeed,
        CustomCondition,
    }

    [Flags]
    public enum WeatherTag
    {
        Sunny = 1,
        Cloudy = 2,
        Stormy = 4,
        Rainy = 8,
        Snowy = 16,
        Windy = 32,
        Fog = 64,
        Chain = 128,
    }

    [Serializable]
    public struct WeatherChangeCondition
    {

        public enum CompareCondition
        {
            LessThan,
            EqualTo,
            GreaterThan
        }

        public CompareCondition compare;

        public ChanceConditionType conditionType;
        public Temperature temperature;
        public Humidity humidity;
        public Pressure pressure;
        public UVIndex uvIndex;
        public AirQuality airQuality;
        public float accumulatedSnow;
        public float accumulatedRain;
        public float convectiveRainChance;
        public float stratiformRainChance;
        public SeasonName season;
        public MeridiemTime time;
        public MeridiemDate day;
        public float windSpeed;
        public WeightedRandomChance weightedRandomChance;


        public bool CheckCondition(CozyWeather cozyWeather)
        {
            // Climate-based numeric conditions
            if (cozyWeather.Climate != null)
            {
                double? current = conditionType switch
                {
                    ChanceConditionType.Temperature => cozyWeather.Climate.Temperature.Current,
                    ChanceConditionType.Humidity => cozyWeather.Climate.Humidity.Current,
                    ChanceConditionType.Pressure => cozyWeather.Climate.Pressure.Current,
                    ChanceConditionType.UVIndex => cozyWeather.Climate.UVIndex.Current,
                    ChanceConditionType.AirQuality => cozyWeather.Climate.AirQuality.Current,
                    // ChanceConditionType.WindSpeed => cozyWeather.Climate.WindSpeed.Current,
                    ChanceConditionType.AccumulatedSnow => cozyWeather.Precipitation.AccumulatedSnow,
                    ChanceConditionType.AccumulatedRain => cozyWeather.Precipitation.AccumulatedRain,
                    ChanceConditionType.ConvectiveRainChance => cozyWeather.Climate.ConvectiveRainChance,
                    ChanceConditionType.StratiformRainChance => cozyWeather.Climate.StratiformRainChance,
                    _ => null
                };

                double? threshold = conditionType switch
                {
                    ChanceConditionType.Temperature => temperature,
                    ChanceConditionType.Humidity => humidity,
                    ChanceConditionType.Pressure => pressure,
                    ChanceConditionType.UVIndex => uvIndex,
                    ChanceConditionType.AirQuality => airQuality,
                    ChanceConditionType.WindSpeed => windSpeed,
                    ChanceConditionType.AccumulatedSnow => accumulatedSnow,
                    ChanceConditionType.AccumulatedRain => accumulatedRain,
                    ChanceConditionType.ConvectiveRainChance => convectiveRainChance,
                    ChanceConditionType.StratiformRainChance => stratiformRainChance,
                    _ => null
                };

                if (current.HasValue && threshold.HasValue)
                    return Compare(current.Value, threshold.Value);
            }

            // Time condition
            if (conditionType == ChanceConditionType.Time && cozyWeather.Time != null)
                return Compare(cozyWeather.Time.CurrentTime, time);

            if (conditionType == ChanceConditionType.YearPercentage && cozyWeather.Calendar != null)
                return Compare(cozyWeather.Calendar.CurrentDate.YearPercentage, day.YearPercentage);

            // Season condition
            if (conditionType == ChanceConditionType.Season && cozyWeather.Seasons != null)
                return Compare((int)cozyWeather.Seasons.CurrentSeason, (int)season);

            return true;
        }

        private bool Compare<T>(T current, T threshold) where T : IComparable<T>
        {
            return compare switch
            {
                CompareCondition.LessThan => current.CompareTo(threshold) < 0,
                CompareCondition.EqualTo => current.CompareTo(threshold) == 0,
                CompareCondition.GreaterThan => current.CompareTo(threshold) > 0,
                _ => true
            };
        }

    }

    [Serializable]
    public struct LateralWeatherShift
    {
        public WeatherProfile lateralProfile;
        public WeatherChangeCondition changeCondition;
    }

    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/Weather Profile", order = 361)]
    public class WeatherProfile : CozyProfile, IStateMachineProfile
    {
        [Tooltip("Specifies the minimum length for this weather profile in in-game hours and minutes.")]
        [FormerlySerializedAs("minWeatherTime")]
        [SerializeField]
        private MeridiemTimeDelta minTime = new(3, 00);
        [Tooltip("Specifies the maximum length for this weather profile in in-game hours and minutes.")]
        [FormerlySerializedAs("maxWeatherTime")]
        [SerializeField]
        private MeridiemTimeDelta maxTime = new(8, 00);

        public MeridiemTimeDelta MinTime => minTime;

        public MeridiemTimeDelta MaxTime => maxTime;

        public float Chance => chance;

        public WeatherTag Tag => tag;
        [Tooltip("Allow only these weather profiles to immediately follow this weather profile in a forecast.")]
        public WeatherProfile[] forecastNext;
        public enum ForecastModifierMethod { forecastNext, DontForecastNext, forecastAnyProfileNext }
        public ForecastModifierMethod forecastModifierMethod = ForecastModifierMethod.forecastAnyProfileNext;

        [FX]
        [SerializeReference]
        private CozyFXProfile[] FX;

        [SerializeField]
        private WeightedRandomChance chance;
        public string description;
        public Texture2D icon;
        [SerializeField]
        private WeatherTag tag;
        public LateralWeatherShift[] lateralShifts;

        public float GetChance(ConditionSnapshot conditions) => chance.GetChance(conditions);
        public void Play(float weightVal)
        {
            if (FX == null) return;

            int i = 0;

            foreach (CozyFXProfile fx in FX)
                if (fx != null)
                {
                    fx.PlayEffect(weightVal);
                    i++;
                }
        }
    }

}