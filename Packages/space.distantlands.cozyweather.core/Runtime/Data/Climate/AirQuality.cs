//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Stores a UV Index.
    /// </summary>
    [Serializable]
    public struct AirQuality : IClimateProperty<AirQuality, AirQualityDelta>
    {
        [SerializeField]
        private float value;
        private static float MinValue => 0;
        private static float MaxValue => 500;

        /// <summary>
        /// Get a value between 0 and 1 based on the minimum and maximum values
        /// </summary>
        public readonly float Normalized => Mathf.Clamp01(Mathf.InverseLerp(MinValue, MaxValue, value));

        public readonly float GetAirQuality()
        {
            return Mathf.Clamp(value, 0, 500);
        }

        public AirQuality(float v)
        {
            value = Mathf.Clamp(v, 0, 500);
        }

        public static implicit operator AirQuality(float v)
        {
            return new AirQuality(Mathf.Clamp(v, 0, 500));
        }

        public static implicit operator float(AirQuality humidity)
        {
            return humidity.GetAirQuality();
        }

        public override string ToString()
        {
            return $"{Mathf.Round(GetAirQuality())} AQI";
        }

        /// <summary>
        /// Creates a new UV Index based on a string value.
        /// </summary>
        public static AirQuality Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Input string is null or empty.");

            s = s.Trim().ToLowerInvariant();

            Match match = Regex.Match(s, @"([-+]?\d*\.?\d+)");

            if (match.Success &&
                float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                return new AirQuality(value);
            }

            // Fallback
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float fallbackValue))
            {
                return new AirQuality(fallbackValue);
            }

            throw new FormatException($"Could not parse air quality input: '{s}'");
        }
        public AirQuality Lerp(AirQuality lerpTo, float t)
        {
            return new AirQuality(Mathf.Lerp(value, lerpTo.value, t));
        }
        public static AirQuality Lerp(AirQuality a, AirQuality b, float t)
        {
            return new AirQuality(Mathf.Lerp(a.value, b.value, t));
        }

        public void Add(AirQualityDelta val)
        {
            value += val.value;
        }
        public void Subtract(AirQualityDelta val)
        {
            value -= val.value;
        }
    }

    [Serializable]
    public struct AirQualityDelta : IClimatePropertyDelta<AirQuality>
    {
        [SerializeField]
        internal float value;
        public AirQualityDelta(float v)
        {
            value = v;
        }
        public static implicit operator AirQualityDelta(float v)
        {
            return new AirQualityDelta(v);
        }

        public static implicit operator float(AirQualityDelta airQuality)
        {
            return airQuality.value;
        }

        public override string ToString()
        {
            return $"{Mathf.Round(value)} AQI";
        }

        /// <summary>
        /// Creates a new air quality delta based on a string value.
        /// </summary>
        public static AirQualityDelta Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Input string is null or empty.");

            s = s.Trim().ToLowerInvariant();

            Match match = Regex.Match(s, @"([-+]?\d*\.?\d+)");

            if (match.Success &&
                float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                return new AirQualityDelta(value);
            }

            // Fallback
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float fallbackValue))
            {
                return new AirQualityDelta(fallbackValue);
            }

            throw new FormatException($"Could not parse air quality input: '{s}'");
        }
    }
}