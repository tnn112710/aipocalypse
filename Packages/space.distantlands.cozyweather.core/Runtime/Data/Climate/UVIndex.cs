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
    public struct UVIndex : IClimateProperty<UVIndex, UVIndexDelta>
    {
        [SerializeField]
        private float value;
        private static float MinValue => 0;
        private static float MaxValue => 15;

        /// <summary>
        /// Get a value between 0 and 1 based on the minimum and maximum values
        /// </summary>
        public readonly float Normalized => Mathf.Clamp01(Mathf.InverseLerp(MinValue, MaxValue, value));

        public readonly float GetUVIndex()
        {
            return Mathf.Clamp(value, MinValue, MaxValue);
        }

        public UVIndex(float v)
        {
            value = Mathf.Clamp(v, MinValue, MaxValue);
        }

        public static implicit operator UVIndex(float v)
        {
            return new UVIndex(Mathf.Clamp(v, MinValue, MaxValue));
        }

        public static implicit operator float(UVIndex humidity)
        {
            return humidity.GetUVIndex();
        }

        public override string ToString()
        {
            return $"{Mathf.Round(GetUVIndex())}";
        }

        /// <summary>
        /// Creates a new UV Index based on a string value.
        /// </summary>
        public static UVIndex Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Input string is null or empty.");

            s = s.Trim().ToLowerInvariant();

            Match match = Regex.Match(s, @"([-+]?\d*\.?\d+)");

            if (match.Success &&
                float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                return new UVIndex(value);
            }

            // Fallback
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float fallbackValue))
            {
                return new UVIndex(fallbackValue);
            }

            throw new FormatException($"Could not parse UV input: '{s}'");
        }
        public static UVIndex Lerp(UVIndex a, UVIndex b, float t)
        {
            return new UVIndex(Mathf.Lerp(a.value, b.value, t));
        }
        public UVIndex Lerp(UVIndex lerpTo, float t)
        {
            return new UVIndex(Mathf.Lerp(value, lerpTo.value, t));
        }
        public void Add(UVIndexDelta val)
        {
            value += val.value;
        }
        public void Subtract(UVIndexDelta val)
        {
            value -= val.value;
        }
    }

    [Serializable]
    public struct UVIndexDelta : IClimatePropertyDelta<UVIndex>
    {
        [SerializeField]
        internal float value;
        public UVIndexDelta(float v)
        {
            value = v;
        }
        public static implicit operator UVIndexDelta(float v)
        {
            return new UVIndexDelta(v);
        }

        public static implicit operator float(UVIndexDelta uvIndex)
        {
            return uvIndex.value;
        }

        public override string ToString()
        {
            return $"{Mathf.Round(value)}";
        }

        /// <summary>
        /// Creates a new UV index delta based on a string value.
        /// </summary>
        public static UVIndexDelta Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Input string is null or empty.");

            s = s.Trim().ToLowerInvariant();

            Match match = Regex.Match(s, @"([-+]?\d*\.?\d+)");

            if (match.Success &&
                float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                return new UVIndexDelta(value);
            }

            // Fallback
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float fallbackValue))
            {
                return new UVIndexDelta(fallbackValue);
            }

            throw new FormatException($"Could not parse UV input: '{s}'");
        }

        public static UVIndex Lerp(UVIndexDelta a, UVIndexDelta b, float t)
        {
            return new UVIndex(Mathf.Lerp(a.value, b.value, t));
        }
    }
}