//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DistantLands.Cozy
{
    public enum PressureUnit
    {
        inHg,
        hPa,
        Atmosphere,
    }

    [Serializable]
    public struct Pressure : IClimateProperty<Pressure, PressureDelta>
    {
        [SerializeField]
        private float value; // Internal storage always in inHg
        private static float MinValue => 28.5f;
        private static float MaxValue => 31f;

        public readonly float Normalized => Mathf.Clamp01(Mathf.InverseLerp(MinValue, MaxValue, value));

        public static float ConvertToUnit(float v, PressureUnit unit)
        {
            switch (unit)
            {
                case PressureUnit.Atmosphere:
                    return v / 29.92126f;
                case PressureUnit.hPa:
                    return v * 33.86389f;
                case PressureUnit.inHg:
                    return v;
                default:
                    return v;
            }
        }

        public static float ConvertFromUnit(float v, PressureUnit unit)
        {
            switch (unit)
            {
                case PressureUnit.Atmosphere:
                    return v * 29.92126f;
                case PressureUnit.hPa:
                    return v / 33.86389f;
                case PressureUnit.inHg:
                    return v;
                default:
                    return v;
            }
        }

        public Pressure(float v, PressureUnit unit)
        {
            value = ConvertFromUnit(v, unit);
        }

        private Pressure(float rawValue, bool isInternal)
        {
            value = rawValue;
        }

        public static implicit operator Pressure(float v)
        {
            return new Pressure(v, CozyPreferences.PressureUnit);
        }

        public static implicit operator float(Pressure pressure)
        {
            return pressure.GetPressure();
        }

        public override string ToString() => ToString(CozyPreferences.PressureUnit);

        public string ToString(PressureUnit unit)
        {
            string unitDenoter = unit switch
            {
                PressureUnit.Atmosphere => "Atm",
                PressureUnit.hPa => "hPa",
                _ => "inHg"
            };

            return $"{GetPressure(unit):F2} {unitDenoter}";
        }

        public static Pressure Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Input string is null or empty.");

            s = s.Trim().ToLowerInvariant();

            Match match = Regex.Match(s, @"([-+]?[0-9]*\.?[0-9]+)\s*(atm|atmosphere|hpa|h|pa|in|hg|inhg|mbar)?");

            if (!match.Success)
            {
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float fallbackValue))
                {
                    return new Pressure(fallbackValue, CozyPreferences.PressureUnit);
                }
                throw new FormatException("Could not parse pressure input.");
            }

            float val = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            string unitStr = match.Groups[2].Value;

            PressureUnit unit = unitStr switch
            {
                "atm" or "atmosphere" => PressureUnit.Atmosphere,
                "hpa" or "h" or "pa" or "mbar" => PressureUnit.hPa,
                "inhg" or "in" or "hg" => PressureUnit.inHg,
                _ => CozyPreferences.PressureUnit
            };

            return new Pressure(val, unit);
        }

        public float GetPressure(PressureUnit unit) => ConvertToUnit(value, unit);
        public float GetPressure() => ConvertToUnit(value, CozyPreferences.PressureUnit);

        public static Pressure Lerp(Pressure a, Pressure b, float t)
        {
            return new Pressure(Mathf.Lerp(a.value, b.value, t), true);
        }
        public Pressure Lerp(Pressure lerpTo, float t)
        {
            return new Pressure(Mathf.Lerp(value, lerpTo.value, t), true);
        }
        public void Add(PressureDelta val)
        {
            value += val.value;
        }
        public void Subtract(PressureDelta val)
        {
            value -= val.value;
        }
    }

    [Serializable]
    public struct PressureDelta : IClimatePropertyDelta<Pressure>
    {
        [SerializeField]
        internal float value;
        public PressureDelta(float v)
        {
            value = v;
        }
        public static implicit operator PressureDelta(float v)
        {
            return new PressureDelta(v);
        }
        public PressureDelta(float v, PressureUnit unit)
        {
            value = Pressure.ConvertFromUnit(v, unit);
        }

        public static implicit operator float(PressureDelta pressure)
        {
            return pressure.value;
        }

        public override string ToString()
        {
            return ToString(CozyPreferences.PressureUnit);
        }

        public string ToString(PressureUnit unit)
        {
            string unitDenoter = unit switch
            {
                PressureUnit.Atmosphere => "Atm",
                PressureUnit.hPa => "hPa",
                _ => "inHg"
            };

            return $"{Pressure.ConvertToUnit(value, unit):F2} {unitDenoter}";
        }

        /// <summary>
        /// Creates a new pressure delta based on a string value.
        /// </summary>
        public static PressureDelta Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Input string is null or empty.");

            s = s.Trim().ToLowerInvariant();

            Match match = Regex.Match(s, @"([-+]?[0-9]*\.?[0-9]+)\s*(atm|atmosphere|hpa|h|pa|in|hg|inhg|mbar)?");

            if (!match.Success)
            {
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float fallbackValue))
                {
                    return new PressureDelta(fallbackValue, CozyPreferences.PressureUnit);
                }
                throw new FormatException("Could not parse pressure input.");
            }

            float val = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            string unitStr = match.Groups[2].Value;

            PressureUnit unit = unitStr switch
            {
                "atm" or "atmosphere" => PressureUnit.Atmosphere,
                "hpa" or "h" or "pa" or "mbar" => PressureUnit.hPa,
                "inhg" or "in" or "hg" => PressureUnit.inHg,
                _ => CozyPreferences.PressureUnit
            };

            return new PressureDelta(val, unit);
        }
    }
}