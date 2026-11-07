//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;

namespace DistantLands.Cozy
{
    [Serializable]
    public struct CurveKey
    {
        public float time;
        public float value;

        public CurveKey(float time, float value)
        {
            this.time = time;
            this.value = value;
        }
    }

    [Serializable]
    public class WeightedChanceCurve
    {
        public ChanceConditionType curveType;
        public CurveKey[] keys;

        private int _lastIndex = 0;

        public WeightedChanceCurve(params CurveKey[] keys)
        {
            this.keys = keys;
        }

        public float EvaluateBasedOnConditions(ConditionSnapshot conditions)
        {
            switch (curveType)
            {
                case ChanceConditionType.Temperature:
                    if (conditions.temperature)
                        return Evaluate(conditions.temperature.Value.InRange(0, 100));
                    return 1;
                case ChanceConditionType.Humidity:
                    if (conditions.humidity)
                        return Evaluate(conditions.humidity.Value / 100f);
                    return 1;
                case ChanceConditionType.Pressure:
                    if (conditions.pressure)
                        return Evaluate(conditions.pressure.Value.Normalized);
                    return 1;
                case ChanceConditionType.UVIndex:
                    if (conditions.uvIndex)
                        return Evaluate(conditions.uvIndex.Value.Normalized);
                    return 1;
                case ChanceConditionType.AirQuality:
                    if (conditions.airQuality)
                        return Evaluate(conditions.airQuality.Value.Normalized);
                    return 1;
                case ChanceConditionType.ConvectiveRainChance:
                    if (conditions.convectiveRainChance)
                        return Evaluate(conditions.convectiveRainChance.Value.Normalized);
                    return 1;
                case ChanceConditionType.StratiformRainChance:
                    if (conditions.stratiformRainChance)
                        return Evaluate(conditions.stratiformRainChance.Value.Normalized);
                    return 1;
                case ChanceConditionType.Season:
                    if (conditions.season)
                        return Evaluate((float)conditions.season.Value / 4f);
                    return 1;
                case ChanceConditionType.YearPercentage:
                    if (conditions.date)
                        return Evaluate(conditions.date.Value.YearPercentage);
                    return 1;
                case ChanceConditionType.Time:
                    if (conditions.time)
                        return Evaluate(conditions.time.Value);
                    return 1;
                case ChanceConditionType.WindSpeed:
                    if (conditions.windSpeed)
                        return Evaluate(conditions.windSpeed.Value);
                    return 1;
                default:
                    return 1;
            }
        }


        public float Evaluate(float t)
        {
            if (keys == null || keys.Length == 0) return 0;

            int count = keys.Length;

            if (t <= keys[0].time) return keys[0].value;
            if (t >= keys[count - 1].time) return keys[count - 1].value;

            if (t >= keys[_lastIndex].time && t < keys[_lastIndex + 1].time)
            {
                return Interpolate(_lastIndex, t);
            }

            int low = 0;
            int high = count - 2;

            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (t < keys[mid].time) high = mid - 1;
                else if (t >= keys[mid + 1].time) low = mid + 1;
                else
                {
                    _lastIndex = mid;
                    return Interpolate(mid, t);
                }
            }

            return keys[0].value;
        }

        private float Interpolate(int index, float t)
        {
            var k0 = keys[index];
            var k1 = keys[index + 1];

            float timeDiff = k1.time - k0.time;
            if (timeDiff <= 0.0001f) return k1.value;

            float tBetween = (t - k0.time) / timeDiff;

            return k0.value + (k1.value - k0.value) * tBetween;
        }
    }

}