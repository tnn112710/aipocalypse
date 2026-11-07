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
    public struct PrecipitationChance
    {
        [SerializeField]
        private float value;
        private static float MinValue => 0f;
        private static float MaxValue => 100f;

        /// <summary>
        /// Get a value between 0 and 1 based on the minimum and maximum values
        /// </summary>
        public readonly float Normalized => Mathf.Clamp01(Mathf.InverseLerp(MinValue, MaxValue, value));

        public readonly float GetPrecipitationChance()
        {
            return Mathf.Clamp(value, MinValue, MaxValue);
        }

        public PrecipitationChance(float v)
        {
            value = Mathf.Clamp(v, MinValue, MaxValue);
        }

        public static implicit operator PrecipitationChance(float v)
        {
            return new PrecipitationChance(Mathf.Clamp(v, MinValue, MaxValue));
        }

        public static implicit operator float(PrecipitationChance humidity)
        {
            return humidity.GetPrecipitationChance();
        }

        public override string ToString()
        {
            return $"{Mathf.Round(GetPrecipitationChance() * 100) / 100}%";
        }

        /// <summary>
        /// Creates a new temperature based on a string value. Denote the unit with F, C, or K
        /// </summary>
        public static PrecipitationChance Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Input string is null or empty.");

            s = s.Trim().ToLowerInvariant();

            Match match = Regex.Match(s, @"([-+]?\d*\.?\d+)");

            if (match.Success &&
                float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                return new PrecipitationChance(value);
            }

            // Fallback
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float fallbackValue))
            {
                return new PrecipitationChance(fallbackValue);
            }

            throw new FormatException($"Could not parse input: '{s}'");
        }
        public static PrecipitationChance Lerp(PrecipitationChance a, PrecipitationChance b, float t)
        {
            return new PrecipitationChance(Mathf.Lerp(a.value, b.value, t));
        }
    }
}