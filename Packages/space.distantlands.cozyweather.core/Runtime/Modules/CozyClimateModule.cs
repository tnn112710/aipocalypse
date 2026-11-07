//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEngine.Serialization;
using System;
using TemperatureType = DistantLands.Cozy.Temperature;
using HumidityType = DistantLands.Cozy.Humidity;
using PressureType = DistantLands.Cozy.Pressure;
using UVIndexType = DistantLands.Cozy.UVIndex;
using AirQualityType = DistantLands.Cozy.AirQuality;

namespace DistantLands.Cozy
{
    [ExecuteAlways]
    [AddComponentMenu("")]
    [CozyModule("Climate", "Climate", "Control temperature and humidity throughout the year.", "Ecosystem", true, priority = 2)]
    public class CozyClimateModule : CozyBiomeModule<CozyClimateModule>, IClimateModule, ISeasonModule, IPrecipitationModule
    {
        [CozySearchable(true, "wet", "precipitation", "hot", "cold", "humidity", "temperature")]
        public ClimateProfile climateProfile;
        public ControlMethod controlMethod = ControlMethod.Profile;
        public enum ControlMethod
        {
            Profile,
            Native
        }

        [FormerlySerializedAs("m_SnowMeltSpeed")]
        [CozySearchable]
        public float snowMeltSpeed = 0.35f;
        [CozySearchable]
        [FormerlySerializedAs("m_DryingSpeed")]
        public float dryingSpeed = 0.5f;
        private float snowAccumulationSpeed;
        private float rainAccumulationSpeed;

        public ClimateProperty<Temperature, TemperatureDelta> Temperature => temperature;

        [CozySearchable]
        [SerializeField]
        private ClimateProperty<Temperature, TemperatureDelta> temperature = new();

        public ClimateProperty<Humidity, HumidityDelta> Humidity => humidity;
        [CozySearchable]
        [SerializeField]
        private ClimateProperty<Humidity, HumidityDelta> humidity = new();

        public ClimateProperty<Pressure, PressureDelta> Pressure => pressure;
        [CozySearchable]
        [SerializeField]
        private ClimateProperty<Pressure, PressureDelta> pressure = new();

        public ClimateProperty<UVIndex, UVIndexDelta> UVIndex => uvIndex;
        [CozySearchable]
        [SerializeField]
        private ClimateProperty<UVIndex, UVIndexDelta> uvIndex = new();
        public ClimateProperty<AirQuality, AirQualityDelta> AirQuality => airQuality;
        [CozySearchable]
        [SerializeField]
        private ClimateProperty<AirQuality, AirQualityDelta> airQuality = new();

        [Range(0, 1)]
        [CozySearchable]
        public float snowAmount;
        public Accumulation AccumulatedSnow { get => snowAmount; set => snowAmount = value; }
        [Range(0, 1)]
        [CozySearchable]
        [FormerlySerializedAs("wetness")]
        public float groundwaterAmount;
        public Accumulation AccumulatedRain { get => groundwaterAmount; set => groundwaterAmount = value; }

        public float SnowAccumulationSpeed { get => snowAccumulationSpeed; set => snowAccumulationSpeed = value; }

        public float RainAccumulationSpeed { get => rainAccumulationSpeed; set => rainAccumulationSpeed = value; }

        public PrecipitationChance ConvectiveRainChance => convectiveRainChance + convectiveChanceOffset;
        public PrecipitationChance StratiformRainChance => stratiformRainChance + stratiformChanceOffset;

        [SerializeField]
        private PrecipitationChance convectiveRainChance;
        [SerializeField]
        private PrecipitationChance stratiformRainChance;
        public enum RainChanceCalculationMode { Calculated, Seasonal, Manual }
        public RainChanceCalculationMode rainChanceCalculationMode;

        public float convectiveChanceOffset = 0;
        public float stratiformChanceOffset = 0;

        #region Biome Settings 

        [SerializeField] private TemperatureDelta temperatureOffset = 0;
        [SerializeField] private HumidityDelta humidityOffset = 0;
        [SerializeField] private PressureDelta pressureOffset = 0;
        [SerializeField] private UVIndexDelta UVIndexOffset = 0;
        [SerializeField] private AirQualityDelta AQIOffset = 0;

        #endregion

        private MeridiemDate hottestDayOfTheYear;
        private MeridiemDate coldestDayOfTheYear;
        /// <summary>
        /// Get the hottest day of the year
        /// </summary>
        public MeridiemDate HottestDayOfTheYear
        {
            get
            {
                if (hottestDayOfTheYear == null)
                {
                    UpdateExtremeDays();
                }

                return hottestDayOfTheYear;
            }
        }

        /// <summary>
        /// Get the coldest day of the year
        /// </summary>
        public MeridiemDate ColdestDayOfTheYear
        {
            get
            {
                if (coldestDayOfTheYear == null)
                {
                    UpdateExtremeDays();
                }

                return coldestDayOfTheYear;
            }
        }

        private void UpdateExtremeDays()
        {
            Temperature high = new(-1000);
            Temperature low = new(1000);
            hottestDayOfTheYear = new MeridiemDate(1, 1);
            coldestDayOfTheYear = new MeridiemDate(1, 1);
            for (int day = 1; day < CozyPreferences.DaysPerYear + 1; day++)
            {
                climateProfile.GetClimateRanges(new MeridiemDate(1, day, CozyWeather.Instance.Calendar.CurrentDate.year), out ClimateRanges range);
                if (range.temperature.high > high)
                {
                    high = range.temperature.high;
                    hottestDayOfTheYear = new MeridiemDate(day);
                }
                if (range.temperature.low < low)
                {
                    low = range.temperature.low;
                    coldestDayOfTheYear = new MeridiemDate(day);
                }
            }
        }


        ClimateRanges climateRanges = new();
        /// <summary>
        /// Grabs a SeasonName <see langword="enum"/> value of the current season
        /// </summary>
        public SeasonName CurrentSeason
        {
            get
            {
                return cachedSeason;
            }
        }

        private SeasonName cachedSeason;
        public event ChangeSeasonEvent OnChangeSeason;

        public override void InitializeModule()
        {

            UpdateDiurnal();
            UpdateCurrentSeason();

            if (WeatherSphere.Calendar != null)
            {
                WeatherSphere.Calendar.OnChangeDay += UpdateDiurnal;
                WeatherSphere.Calendar.OnChangeDay += UpdateCurrentSeason;
                WeatherSphere.Calendar.OnChangeYear += UpdateExtremeDays;
            }
            else
            {
                climateRanges = new ClimateRanges();
                controlMethod = ControlMethod.Native;
            }

        }

        private void UpdateCurrentSeason()
        {
            if (climateProfile == null) return;

            var calendar = WeatherSphere.Calendar;

            if (calendar == null)
            {
                cachedSeason = SeasonName.Winter;
                return;
            }

            int date = calendar.CurrentDate;

            if (date >= climateProfile.winter.startDay)
            {
                if (cachedSeason != SeasonName.Winter)
                {
                    cachedSeason = SeasonName.Winter;
                    OnChangeSeason?.Invoke(SeasonName.Winter);
                }
                return;
            }
            if (date >= climateProfile.fall.startDay)
            {
                if (cachedSeason != SeasonName.Fall)
                {
                    cachedSeason = SeasonName.Fall;
                    OnChangeSeason?.Invoke(SeasonName.Fall);
                }
                return;
            }
            if (date >= climateProfile.summer.startDay)
            {
                if (cachedSeason != SeasonName.Summer)
                {
                    cachedSeason = SeasonName.Summer;
                    OnChangeSeason?.Invoke(SeasonName.Summer);
                }
                return;
            }
            if (date >= climateProfile.spring.startDay)
            {
                if (cachedSeason != SeasonName.Spring)
                {
                    cachedSeason = SeasonName.Spring;
                    OnChangeSeason?.Invoke(SeasonName.Spring);
                }
                return;
            }

            if (cachedSeason != SeasonName.Winter)
            {
                cachedSeason = SeasonName.Winter;
                OnChangeSeason?.Invoke(SeasonName.Winter);
            }
        }

        /// <summary>
        /// Updates the daily high and low for temperature and humidity
        /// </summary>
        public void UpdateDiurnal()
        {
            if (WeatherSphere.Calendar == null || climateProfile == null) return;

            climateProfile.GetClimateRanges(WeatherSphere.Calendar.CurrentDate, out climateRanges);

            temperature.High = climateRanges.temperature.high;
            temperature.Low = climateRanges.temperature.low;
            humidity.High = climateRanges.humidity.high;
            humidity.Low = climateRanges.humidity.low;
            pressure.High = climateRanges.pressure.high;
            pressure.Low = climateRanges.pressure.low;
            uvIndex.High = climateRanges.uvIndex.high;
            uvIndex.Low = climateRanges.uvIndex.low;
            airQuality.High = climateRanges.airQuality.high;
            airQuality.Low = climateRanges.airQuality.low;

            if (rainChanceCalculationMode == RainChanceCalculationMode.Seasonal)
            {
                convectiveRainChance = climateRanges.convectiveRainChance;
                stratiformRainChance = climateRanges.stratiformRainChance;
            }
        }
        /// <summary>
        /// Gets a snapshot of the climate at a certain day and time 
        /// </summary>
        public ClimateSnapshot GetRangesAtDayAndTime(MeridiemDate date, MeridiemTime time)
        {
            ClimateSnapshot snapshot = new()
            {
                temperature = temperature.Current,
                humidity = humidity.Current,
                pressure = pressure.Current,
                uvIndex = uvIndex.Current,
                airQuality = airQuality.Current,
                stratiformRainChance = stratiformRainChance,
                convectiveRainChance = convectiveRainChance,
            };

            if (WeatherSphere.Calendar == null) return snapshot;

            climateProfile.GetClimateRanges(date, out ClimateRanges ranges);
            float t = time;

            // Humidity modulatedHumidity = HumidityType.Lerp(humidity.Low, humidity.High, climateRanges.humidity.diurnalCurve.Evaluate(t));
            // Pressure modulatedPressure = PressureType.Lerp(pressure.Low, pressure.High, climateRanges.pressure.diurnalCurve.Evaluate(t));
            // UVIndex modulatedUVIndex = UVIndexType.Lerp(uvIndex.Low, uvIndex.High, climateRanges.uvIndex.diurnalCurve.Evaluate(t));
            // AirQuality modulatedAirQuality = AirQualityType.Lerp(airQuality.Low, airQuality.High, climateRanges.airQuality.diurnalCurve.Evaluate(t));

            snapshot.temperature = TemperatureType.Lerp(ranges.temperature.low, ranges.temperature.high, ranges.temperature.diurnalCurve.Evaluate(t)) + temperature.Offset;
            snapshot.humidity = HumidityType.Lerp(ranges.humidity.low, ranges.humidity.high, ranges.humidity.diurnalCurve.Evaluate(t)) + humidity.Offset;
            snapshot.pressure = PressureType.Lerp(ranges.pressure.low, ranges.pressure.high, ranges.pressure.diurnalCurve.Evaluate(t)) + pressure.Offset;
            snapshot.airQuality = AirQualityType.Lerp(ranges.airQuality.low, ranges.airQuality.high, ranges.airQuality.diurnalCurve.Evaluate(t)) + airQuality.Offset;
            snapshot.uvIndex = UVIndexType.Lerp(ranges.uvIndex.low, ranges.uvIndex.high, ranges.uvIndex.diurnalCurve.Evaluate(t)) + uvIndex.Offset;

            switch (rainChanceCalculationMode)
            {
                case RainChanceCalculationMode.Calculated:
                    CalculateRainChances(snapshot.temperature.Normalized,
                                        snapshot.humidity.Normalized,
                                        snapshot.pressure.Normalized,
                                        out snapshot.stratiformRainChance,
                                        out snapshot.convectiveRainChance);
                    break;
                case RainChanceCalculationMode.Seasonal:
                    snapshot.convectiveRainChance = ranges.convectiveRainChance;
                    snapshot.stratiformRainChance = ranges.stratiformRainChance;
                    break;
            }

            return snapshot;
        }

        public override void ApplyBiome(CozyClimateModule biome, float weight)
        {
            temperature.Offset += temperatureOffset * weight;
            humidity.Offset += humidityOffset * weight;
            pressure.Offset += pressureOffset * weight;
            airQuality.Offset += AQIOffset * weight;
            uvIndex.Offset += UVIndexOffset * weight;
        }

        public override void OnCozyUpdateLoop()
        {
            UpdatePrecipitation();

            temperature.Offset = 0;
            humidity.Offset = 0;
            pressure.Offset = 0;
            airQuality.Offset = 0;
            uvIndex.Offset = 0;

            ApplyBiomes();

            if (controlMethod == ControlMethod.Profile && climateProfile)
            {
                float t = WeatherSphere.Time == null ? 0.5f : WeatherSphere.Time.CurrentTime;

                Temperature modulatedTemperature = TemperatureType.Lerp(temperature.Low, temperature.High, climateRanges.temperature.diurnalCurve.Evaluate(t));
                Humidity modulatedHumidity = HumidityType.Lerp(humidity.Low, humidity.High, climateRanges.humidity.diurnalCurve.Evaluate(t));
                Pressure modulatedPressure = PressureType.Lerp(pressure.Low, pressure.High, climateRanges.pressure.diurnalCurve.Evaluate(t));
                UVIndex modulatedUVIndex = UVIndexType.Lerp(uvIndex.Low, uvIndex.High, climateRanges.uvIndex.diurnalCurve.Evaluate(t));
                AirQuality modulatedAirQuality = AirQualityType.Lerp(airQuality.Low, airQuality.High, climateRanges.airQuality.diurnalCurve.Evaluate(t));

                temperature.Current = modulatedTemperature + temperature.Offset;
                humidity.Current = modulatedHumidity + humidity.Offset;
                pressure.Current = modulatedPressure + pressure.Offset;
                airQuality.Current = modulatedAirQuality + airQuality.Offset;
                uvIndex.Current = modulatedUVIndex + uvIndex.Offset;
            }

            if (rainChanceCalculationMode == RainChanceCalculationMode.Calculated)
            {
                CalculateRainChances(temperature.Current.Normalized,
                                    humidity.Current.Normalized,
                                    pressure.Current.Normalized,
                                    out stratiformRainChance,
                                    out convectiveRainChance);
            }

        }

        void UpdatePrecipitation()
        {
            Shader.SetGlobalFloat(CozyShaderIDs.baseWeatherSnow, snowAmount);
            Shader.SetGlobalFloat(CozyShaderIDs.baseWeatherWetness, groundwaterAmount);

            if (!Application.isPlaying) return;

            snowAmount += Time.deltaTime * snowAccumulationSpeed;

            if (snowAccumulationSpeed <= 0)
                if (temperature.Current > new Temperature(32, TemperatureUnit.Fahrenheit))
                    snowAmount -= Time.deltaTime * snowMeltSpeed * 0.03f;

            groundwaterAmount += (Time.deltaTime * rainAccumulationSpeed) + (-1 * Time.deltaTime * dryingSpeed * 0.001f);

            snowAmount = Mathf.Clamp01(snowAmount);
            groundwaterAmount = Mathf.Clamp01(groundwaterAmount);
        }

        private void CalculateRainChances(float t, float h, float p, out PrecipitationChance stratiform, out PrecipitationChance convective)
        {
            float tC = (t + 3) * 0.25f;

            float invP = 1f - p;
            float baseValC = tC * 1.5f * h;

            float result = baseValC * baseValC * (invP * invP);
            convective = Mathf.Clamp01(result) * 100f;

            float p2 = p * p;
            stratiform = Mathf.Clamp01(h * (1f - p2) - Mathf.Clamp01(result)) * 100f;
        }

        public override void FrameReset()
        {

            temperature.Offset = 0;
            humidity.Offset = 0;
            pressure.Offset = 0;
            airQuality.Offset = 0;
            uvIndex.Offset = 0;

            snowAccumulationSpeed = 0;
            rainAccumulationSpeed = 0;

        }



        public override void DeinitializeModule()
        {
            if (WeatherSphere && WeatherSphere.Calendar != null)
            {
                WeatherSphere.Calendar.OnChangeDay -= UpdateDiurnal;
                WeatherSphere.Calendar.OnChangeDay -= UpdateCurrentSeason;
                WeatherSphere.Calendar.OnChangeYear -= UpdateExtremeDays;
            }
        }
    }
}