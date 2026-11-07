//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DistantLands.Cozy
{
    [Serializable]
    public struct Accumulation
    {
        [SerializeField]
        private float value;

        /// <summary>
        /// Get a value between 0 and 1 based on the minimum and maximum values
        /// </summary>
        public readonly float Normalized => Mathf.Clamp01(Mathf.InverseLerp(0, 100, value));
        
        public Accumulation(float v)
        {
            value = Mathf.Clamp(v, 0, 100);
        }
        public static implicit operator Accumulation(float v)
        {
            return new Accumulation(Mathf.Clamp(v, 0, 100));
        }

        public static implicit operator float(Accumulation rainChance)
        {
            return rainChance.GetRainChance();
        }
        public readonly float GetRainChance()
        {
            return Mathf.Clamp(value, 0, 100);
        }
        public override string ToString()
        {
            return $"{Mathf.Round(GetRainChance() * 100) / 100}%";
        }
        /// <summary>
        /// Creates a new Rain Chance based on a string value.
        /// </summary>
        public static Accumulation Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Input string is null or empty.");

            s = s.Trim().ToLowerInvariant();

            Match match = Regex.Match(s, @"([-+]?\d*\.?\d+)");

            if (match.Success &&
                float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                return new Accumulation(value);
            }

            // Fallback
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float fallbackValue))
            {
                return new Accumulation(fallbackValue);
            }

            throw new FormatException($"Could not parse rain chance input: '{s}'");
        }

        public static Accumulation Lerp(Accumulation a, Accumulation b, float t)
        {
            return new Accumulation(Mathf.Lerp(a.value, b.value, t));
        }
        public Accumulation Lerp(Accumulation lerpTo, float t)
        {
            return new Accumulation(Mathf.Lerp(value, lerpTo.value, t));
        }

    }
}