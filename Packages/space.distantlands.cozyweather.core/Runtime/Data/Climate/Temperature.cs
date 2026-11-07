//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DistantLands.Cozy
{
    public enum TemperatureUnit
    {
        Fahrenheit,
        Celsius,
        Kelvin
    }

    [Serializable]
    public struct Temperature : IClimateProperty<Temperature, TemperatureDelta>, IComparable<Temperature>
    {
        [SerializeField]
        internal float value; // Internal storage always in Fahrenheit
        private static float MinValue => -30f;
        private static float MaxValue => 120f;

        public readonly float Normalized
        {
            get
            {
                return Mathf.Clamp01(Mathf.InverseLerp(MinValue, MaxValue, value));
            }
        }
        public readonly float InRange(float min, float max)
        {
            return Mathf.Clamp01(Mathf.InverseLerp(min, max, value));
        }

        public static float ConvertToUnit(float v, TemperatureUnit unit)
        {
            return unit switch
            {
                TemperatureUnit.Celsius => (v - 32f) * 5f / 9f,
                TemperatureUnit.Kelvin => (v - 32f) * 5f / 9f + 273.15f,
                TemperatureUnit.Fahrenheit => v,
                _ => v
            };
        }

        public static float ConvertFromUnit(float v, TemperatureUnit unit)
        {
            return unit switch
            {
                TemperatureUnit.Celsius => v * 9f / 5f + 32f,
                TemperatureUnit.Kelvin => (v - 273.15f) * 9f / 5f + 32f,
                TemperatureUnit.Fahrenheit => v,
                _ => v
            };
        }

        public Temperature(float v, TemperatureUnit unit)
        {
            value = ConvertFromUnit(v, unit);
        }

        public Temperature(float rawValue)
        {
            value = ConvertFromUnit(rawValue, TemperatureUnit.Fahrenheit);
        }

        public static implicit operator Temperature(float v)
        {
            return new Temperature(v, TemperatureUnit.Fahrenheit);
        }

        public static implicit operator float(Temperature temperature)
        {
            return temperature.value;
        }

        public override string ToString() => ToString(CozyPreferences.TemperatureUnit);

        public string ToString(TemperatureUnit unit)
        {
            string unitDenoter = unit switch
            {
                TemperatureUnit.Celsius => "°C",
                TemperatureUnit.Kelvin => "K",
                _ => "°F"
            };

            return $"{GetTemperature(unit):F1}{unitDenoter}";
        }

        public static Temperature Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return new Temperature(0);

            s = s.Trim().ToLowerInvariant();

            Match match = Regex.Match(s, @"([-+]?[0-9]*\.?[0-9]+)\s*(°?\s*(f|fahrenheit|c|celsius|k|kelvin)?)");

            if (!match.Success)
            {
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float fallbackValue))
                {
                    return new Temperature(fallbackValue, CozyPreferences.TemperatureUnit);
                }
                throw new FormatException("Could not parse temperature input.");
            }

            float val = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            string unitStr = match.Groups[3].Value;

            TemperatureUnit unit = unitStr switch
            {
                "c" or "celsius" => TemperatureUnit.Celsius,
                "k" or "kelvin" => TemperatureUnit.Kelvin,
                "f" or "fahrenheit" => TemperatureUnit.Fahrenheit,
                _ => CozyPreferences.TemperatureUnit
            };

            return new Temperature(val, unit);
        }

        public float GetTemperature(TemperatureUnit unit) => ConvertToUnit(value, unit);
        public float GetTemperature() => ConvertToUnit(value, CozyPreferences.TemperatureUnit);

        public static Temperature Lerp(Temperature a, Temperature b, float t)
        {
            return new Temperature(Mathf.Lerp(a.value, b.value, t), TemperatureUnit.Fahrenheit);
        }
        public Temperature Lerp(Temperature lerpTo, float t)
        {
            return new Temperature(Mathf.Lerp(value, lerpTo.value, t), TemperatureUnit.Fahrenheit);
        }

        public void Add(TemperatureDelta delta)
        {
            value += delta.value;
        }
        public void Subtract(TemperatureDelta delta)
        {
            value -= delta.value;
        }

        public readonly int CompareTo(Temperature other)
        {
            return value.CompareTo(other.value);
        }
    }

    [Serializable]
    public struct TemperatureDelta : IClimatePropertyDelta<Temperature>
    {
        [SerializeField]
        internal float value; // Internal storage always in Fahrenheit

        public TemperatureDelta(float v, TemperatureUnit unit)
        {
            value = ConvertFromUnit(v, unit);
        }

        public TemperatureDelta(float rawValue)
        {
            value = ConvertFromUnit(rawValue, TemperatureUnit.Fahrenheit);
        }

        public static implicit operator TemperatureDelta(float v)
        {
            return new TemperatureDelta(v, TemperatureUnit.Fahrenheit);
        }

        public static implicit operator float(TemperatureDelta temperature)
        {
            return temperature.value;
        }

        public static float ConvertToUnit(float v, TemperatureUnit unit)
        {
            return unit switch
            {
                TemperatureUnit.Celsius => (v) * 5f / 9f,
                TemperatureUnit.Kelvin => (v) * 5f / 9f,
                TemperatureUnit.Fahrenheit => v,
                _ => v
            };
        }

        public static float ConvertFromUnit(float v, TemperatureUnit unit)
        {
            return unit switch
            {
                TemperatureUnit.Celsius => v * 9f / 5f,
                TemperatureUnit.Kelvin => (v) * 9f / 5f,
                TemperatureUnit.Fahrenheit => v,
                _ => v
            };
        }

        public float GetTemperature(TemperatureUnit unit) => ConvertToUnit(value, unit);
        public float GetTemperature() => ConvertToUnit(value, CozyPreferences.TemperatureUnit);

        public override string ToString()
        {
            return ToString(CozyPreferences.TemperatureUnit);
        }

        public string ToString(TemperatureUnit unit)
        {
            string unitDenoter = unit switch
            {
                TemperatureUnit.Celsius => "°C",
                TemperatureUnit.Kelvin => "K",
                _ => "°F"
            };

            return $"{GetTemperature(unit):F1}{unitDenoter}";
        }

        public static TemperatureDelta Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return new TemperatureDelta(0);

            s = s.Trim().ToLowerInvariant();

            Match match = Regex.Match(s, @"([-+]?[0-9]*\.?[0-9]+)\s*(°?\s*(f|fahrenheit|c|celsius|k|kelvin)?)");

            if (!match.Success)
            {
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float fallbackValue))
                {
                    Debug.Log("this");
                    return new TemperatureDelta(fallbackValue, CozyPreferences.TemperatureUnit);
                }
                throw new FormatException("Could not parse temperature input.");
            }

            float val = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            string unitStr = match.Groups[3].Value;

            TemperatureUnit unit = unitStr switch
            {
                "c" or "celsius" => TemperatureUnit.Celsius,
                "k" or "kelvin" => TemperatureUnit.Kelvin,
                "f" or "fahrenheit" => TemperatureUnit.Fahrenheit,
                _ => CozyPreferences.TemperatureUnit
            };

            return new TemperatureDelta(val, unit);
        }

        public static TemperatureDelta operator *(TemperatureDelta delta, float a)
        {
            return new TemperatureDelta(delta.value * a);
        }
    }
}