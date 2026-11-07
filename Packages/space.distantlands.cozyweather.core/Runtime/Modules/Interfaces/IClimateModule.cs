//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine;

namespace DistantLands.Cozy
{

    /// <summary>
    /// Used by the climate module to relate a type to its delta type
    /// </summary>
    /// <typeparam name="T">The base type for the property</typeparam>
    /// <typeparam name="D">The delta type</typeparam>
    [Serializable]
    public class ClimateProperty<T, D> where T : IClimateProperty<T, D> where D : IClimatePropertyDelta<T>
    {
        [SerializeField]
        private T current;
        public T Current { get => current; set => current = value; }
        public T Offset { get => offset; set => offset = value; }
        private T offset;
        public T High
        {
            get => high; set
            {
                high = value;
            }
        }
        public T Low
        {
            get => low; set
            {
                low = value;
            }
        }
        T high;
        T low;
        D delta;
        public D Delta
        {
            get => delta;
        }

        public static T Lerp(T a, T b, float time)
        {
            return a.Lerp(b, time);
        }
    }

    /// <summary>
    /// Interface for modules that control the climate
    /// </summary>
    public interface IClimateModule : ICozyModule
    {
        public ClimateProperty<Temperature, TemperatureDelta> Temperature { get; }
        public ClimateProperty<Humidity, HumidityDelta> Humidity { get; }
        public ClimateProperty<Pressure, PressureDelta> Pressure { get; }
        public ClimateProperty<UVIndex, UVIndexDelta> UVIndex { get; }
        public ClimateProperty<AirQuality, AirQualityDelta> AirQuality { get; }
        public PrecipitationChance ConvectiveRainChance { get; }
        public PrecipitationChance StratiformRainChance { get; }

        public ClimateSnapshot GetRangesAtDayAndTime(MeridiemDate date, MeridiemTime time);
    }
}