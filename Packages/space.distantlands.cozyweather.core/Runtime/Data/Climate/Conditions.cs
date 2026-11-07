//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

namespace DistantLands.Cozy
{
    public struct ConditionType<T>
    {
        private T value;
        public T Value
        {
            get
            {
                return value;
            }
            set
            {
                this.value = value;
                enabled = true;
            }
        }
        public bool enabled;
        public static implicit operator bool(ConditionType<T> conditionType) => conditionType.enabled;
    }
    /// <summary>
    /// Holds data for the conditions across various modules in COZY.
    /// </summary>
    public struct ConditionSnapshot
    {
        public ConditionType<Temperature> temperature;
        public ConditionType<Humidity> humidity;
        public ConditionType<Pressure> pressure;
        public ConditionType<AirQuality> airQuality;
        public ConditionType<UVIndex> uvIndex;
        public ConditionType<Accumulation> accumulatedRain;
        public ConditionType<Accumulation> accumulatedSnow;
        public ConditionType<PrecipitationChance> stratiformRainChance;
        public ConditionType<PrecipitationChance> convectiveRainChance;
        public ConditionType<MeridiemTime> time;
        public ConditionType<MeridiemDate> date;
        public ConditionType<SeasonName> season;
        public ConditionType<float> windSpeed;

        public static ConditionSnapshot GetConditions(CozySystem weather, MeridiemDate date, MeridiemTime time)
        {
            ConditionSnapshot conditions = new();

            if (weather.GetModule(out IClimateModule climate))
            {
                var snapshot = climate.GetRangesAtDayAndTime(date, time);
                conditions.temperature.Value = snapshot.temperature;
                conditions.humidity.Value = snapshot.humidity;
                conditions.pressure.Value = snapshot.pressure;
                conditions.airQuality.Value = snapshot.airQuality;
                conditions.uvIndex.Value = snapshot.uvIndex;
                conditions.convectiveRainChance.Value = snapshot.convectiveRainChance;
                conditions.stratiformRainChance.Value = snapshot.stratiformRainChance;
                conditions.temperature.Value = snapshot.temperature;
                conditions.season.Value = snapshot.season;
            }

            if (weather.GetModule(out IPrecipitationModule precipitation))
            {
                conditions.accumulatedRain.Value = precipitation.AccumulatedRain;
                conditions.accumulatedSnow.Value = precipitation.AccumulatedSnow;
            }

            if (weather.GetModule(out IWindModule wind))
            {
                // TODO: Add wind support
            }

            if (weather.GetModule(out ITimeModule timeModule))
            {
                conditions.time.Value = time;
            }

            if (weather.GetModule(out ICalendarModule calendarModule))
            {
                conditions.date.Value = date;
            }

            return conditions;
        }
        public static ConditionSnapshot GetConditions(CozySystem weather)
        {
            MeridiemTime time = MeridiemTime.Noon;
            MeridiemDate date = new(0);
            if (weather.GetModule(out ITimeModule timeModule))
            {
                time = timeModule.CurrentTime;
            }
            if (weather.GetModule(out ICalendarModule calendarModule))
            {
                date = calendarModule.CurrentDate;
            }
            return GetConditions(weather, date, time);
        }

    }
}