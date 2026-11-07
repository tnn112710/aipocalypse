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
    /// Stores a percentage chance for a rain-based weather profile. Set minimum and maximum humidity for weather profiles to play in the chance effector fro your weather profiles.
    /// </summary>
    [Serializable]
    public struct Humidity : IClimateProperty<Humidity, HumidityDelta>
    {
        [SerializeField]
        private float value;
        private static float MinValue => 0f;
        private static float MaxValue => 100f;

        /// <summary>
        /// Get a value between 0 and 1 based on the minimum and maximum values
        /// </summary>
        public readonly float Normalized => Mathf.Clamp01(Mathf.InverseLerp(MinValue, MaxValue, value));

        public readonly float GetHumidity()
        {
            return Mathf.Clamp(value, 0, 100);
        }

        public Humidity(float v)
        {
            value = Mathf.Clamp(v, 0, 100);
        }

        public static implicit operator Humidity(float v)
        {
            return new Humidity(Mathf.Clamp(v, 0, 100));
        }

        public static implicit operator float(Humidity humidity)
        {
            return humidity.GetHumidity();
        }

        public override string ToString()
        {
            return $"{Mathf.Round(GetHumidity() * 100) / 100}%";
        }

        /// <summary>
        /// Creates a new temperature based on a string value. Denote the unit with F, C, or K
        /// </summary>
        public static Humidity Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Input string is null or empty.");

            s = s.Trim().ToLowerInvariant();

            Match match = Regex.Match(s, @"([-+]?\d*\.?\d+)");

            if (match.Success &&
                float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                return new Humidity(value);
            }

            // Fallback
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float fallbackValue))
            {
                return new Humidity(fallbackValue);
            }

            throw new FormatException($"Could not parse humidity input: '{s}'");
        }
        public static Humidity Lerp(Humidity a, Humidity b, float t)
        {
            return new Humidity(Mathf.Lerp(a.value, b.value, t));
        }
        public Humidity Lerp(Humidity lerpTo, float t)
        {
            return new Humidity(Mathf.Lerp(value, lerpTo.value, t));
        }

        public void Add(HumidityDelta humidity)
        {
            value += humidity.value;
        }
        public void Subtract(HumidityDelta humidity)
        {
            value -= humidity.value;
        }
    }

    [Serializable]
    public struct HumidityDelta : IClimatePropertyDelta<Humidity>
    {
        [SerializeField]
        internal float value;
        public HumidityDelta(float v)
        {
            value = v;
        }
        public static implicit operator HumidityDelta(float v)
        {
            return new HumidityDelta(v);
        }

        public static implicit operator float(HumidityDelta humidity)
        {
            return humidity.value;
        }

        public override string ToString()
        {
            return $"{Mathf.Round(value * 100) / 100}%";
        }

        /// <summary>
        /// Creates a new humidity delta based on a string value.
        /// </summary>
        public static HumidityDelta Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Input string is null or empty.");

            s = s.Trim().ToLowerInvariant();

            Match match = Regex.Match(s, @"([-+]?\d*\.?\d+)");

            if (match.Success &&
                float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                return new HumidityDelta(value);
            }

            // Fallback
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float fallbackValue))
            {
                return new HumidityDelta(fallbackValue);
            }

            throw new FormatException($"Could not parse humidity input: '{s}'");
        }
    }
}