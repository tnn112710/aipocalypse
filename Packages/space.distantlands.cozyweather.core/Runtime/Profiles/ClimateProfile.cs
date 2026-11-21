//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using UnityEngine;



namespace DistantLands.Cozy
{

    [Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/Climate Profile", order = 361)]
    public class ClimateProfile : CozyProfile
    {

        public enum SeasonalTemperatureSmoothness { Smooth, Steps }
        public SeasonalTemperatureSmoothness temperatureSmoothness;

        public Season spring;
        public Season summer;
        public Season fall;
        public Season winter;


        public void GetClimateRanges(MeridiemDate date, out ClimateRanges climateRanges)
        {
            climateRanges = new ClimateRanges();
            date = date.ToBalanced();
            float yearPercent = date.YearPercentage;

            // Season starts
            float springStart = spring.StartDay.YearPercentage;
            float summerStart = summer.StartDay.YearPercentage;
            float fallStart = fall.StartDay.YearPercentage;
            float winterStart = winter.StartDay.YearPercentage;


            climateRanges = winter.climateInformation;
            climateRanges.season = SeasonName.Winter;
            if (yearPercent >= springStart && yearPercent < summerStart)
            {
                climateRanges = spring.climateInformation;
                climateRanges.season = SeasonName.Spring;
            }
            else if (yearPercent >= summerStart && yearPercent < fallStart)
            {
                climateRanges = summer.climateInformation;
                climateRanges.season = SeasonName.Summer;
            }
            else if (yearPercent >= fallStart && yearPercent < winterStart)
            {
                climateRanges = fall.climateInformation;
                climateRanges.season = SeasonName.Fall;
            }

            if (temperatureSmoothness == SeasonalTemperatureSmoothness.Smooth)
            {

                // Compute midpoints safely (handles wrap-around)
                float springMid = (springStart + Wrap01(summerStart, springStart)) * 0.5f % 1f;
                float summerMid = (summerStart + Wrap01(fallStart, summerStart)) * 0.5f % 1f;
                float fallMid = (fallStart + Wrap01(winterStart, fallStart)) * 0.5f % 1f;
                float winterMid = (winterStart + Wrap01(springStart + 1f, winterStart)) * 0.5f % 1f;

                // Distance-based weighting function
                float Weight(float t, float mid)
                {
                    float d = Mathf.Abs(t - mid);
                    if (d > 0.5f) d = 1f - d; // wrap-around
                    float x = 1f - (d / 0.25f); // influence radius = 0.25 year
                    x = Mathf.Clamp01(x);
                    return 0.5f - 0.5f * Mathf.Cos(x * Mathf.PI);
                }

                float wSpring = Weight(yearPercent, springMid);
                float wSummer = Weight(yearPercent, summerMid);
                float wFall = Weight(yearPercent, fallMid);
                float wWinter = Weight(yearPercent, winterMid);

                float sum = wSpring + wSummer + wFall + wWinter;
                if (sum > 0f)
                {
                    wSpring /= sum;
                    wSummer /= sum;
                    wFall /= sum;
                    wWinter /= sum;

                    climateRanges.temperature.high = (spring.climateInformation.temperature.high * wSpring) +
                                                     (summer.climateInformation.temperature.high * wSummer) +
                                                     (fall.climateInformation.temperature.high * wFall) +
                                                     (winter.climateInformation.temperature.high * wWinter);
                    climateRanges.temperature.low = (spring.climateInformation.temperature.low * wSpring) +
                                                    (summer.climateInformation.temperature.low * wSummer) +
                                                    (fall.climateInformation.temperature.low * wFall) +
                                                    (winter.climateInformation.temperature.low * wWinter);
                    climateRanges.temperature.variationAmplitude = (spring.climateInformation.temperature.variationAmplitude * wSpring) +
                                                    (summer.climateInformation.temperature.variationAmplitude * wSummer) +
                                                    (fall.climateInformation.temperature.variationAmplitude * wFall) +
                                                    (winter.climateInformation.temperature.variationAmplitude * wWinter);

                    climateRanges.humidity.high = (spring.climateInformation.humidity.high * wSpring) +
                                                     (summer.climateInformation.humidity.high * wSummer) +
                                                     (fall.climateInformation.humidity.high * wFall) +
                                                     (winter.climateInformation.humidity.high * wWinter);
                    climateRanges.humidity.low = (spring.climateInformation.humidity.low * wSpring) +
                                                    (summer.climateInformation.humidity.low * wSummer) +
                                                    (fall.climateInformation.humidity.low * wFall) +
                                                    (winter.climateInformation.humidity.low * wWinter);
                    climateRanges.humidity.variationAmplitude = (spring.climateInformation.humidity.variationAmplitude * wSpring) +
                                                    (summer.climateInformation.humidity.variationAmplitude * wSummer) +
                                                    (fall.climateInformation.humidity.variationAmplitude * wFall) +
                                                    (winter.climateInformation.humidity.variationAmplitude * wWinter);

                    climateRanges.pressure.high = (spring.climateInformation.pressure.high * wSpring) +
                                                     (summer.climateInformation.pressure.high * wSummer) +
                                                     (fall.climateInformation.pressure.high * wFall) +
                                                     (winter.climateInformation.pressure.high * wWinter);
                    climateRanges.pressure.low = (spring.climateInformation.pressure.low * wSpring) +
                                                    (summer.climateInformation.pressure.low * wSummer) +
                                                    (fall.climateInformation.pressure.low * wFall) +
                                                    (winter.climateInformation.pressure.low * wWinter);
                    climateRanges.pressure.variationAmplitude = (spring.climateInformation.pressure.variationAmplitude * wSpring) +
                                                    (summer.climateInformation.pressure.variationAmplitude * wSummer) +
                                                    (fall.climateInformation.pressure.variationAmplitude * wFall) +
                                                    (winter.climateInformation.pressure.variationAmplitude * wWinter);

                    climateRanges.uvIndex.high = (spring.climateInformation.uvIndex.high * wSpring) +
                                                     (summer.climateInformation.uvIndex.high * wSummer) +
                                                     (fall.climateInformation.uvIndex.high * wFall) +
                                                     (winter.climateInformation.uvIndex.high * wWinter);
                    climateRanges.uvIndex.variationAmplitude = (spring.climateInformation.uvIndex.variationAmplitude * wSpring) +
                                                    (summer.climateInformation.uvIndex.variationAmplitude * wSummer) +
                                                    (fall.climateInformation.uvIndex.variationAmplitude * wFall) +
                                                    (winter.climateInformation.uvIndex.variationAmplitude * wWinter);
                    climateRanges.uvIndex.low = 0;

                    climateRanges.airQuality.high = (spring.climateInformation.airQuality.high * wSpring) +
                                                     (summer.climateInformation.airQuality.high * wSummer) +
                                                     (fall.climateInformation.airQuality.high * wFall) +
                                                     (winter.climateInformation.airQuality.high * wWinter);
                    climateRanges.airQuality.low = (spring.climateInformation.airQuality.low * wSpring) +
                                                    (summer.climateInformation.airQuality.low * wSummer) +
                                                    (fall.climateInformation.airQuality.low * wFall) +
                                                    (winter.climateInformation.airQuality.low * wWinter);
                    climateRanges.airQuality.variationAmplitude = (spring.climateInformation.airQuality.variationAmplitude * wSpring) +
                                                    (summer.climateInformation.airQuality.variationAmplitude * wSummer) +
                                                    (fall.climateInformation.airQuality.variationAmplitude * wFall) +
                                                    (winter.climateInformation.airQuality.variationAmplitude * wWinter);
                }

            }

            TemperatureDelta temperatureDelta = climateRanges.temperature.variationAmplitude * climateRanges.temperature.GetVariation(date, 1);
            HumidityDelta humidityDelta = climateRanges.humidity.variationAmplitude * climateRanges.humidity.GetVariation(date, 500);
            PressureDelta pressureDelta = climateRanges.pressure.variationAmplitude * climateRanges.pressure.GetVariation(date, 150);
            UVIndexDelta uvIndexDelta = climateRanges.uvIndex.variationAmplitude * climateRanges.uvIndex.GetVariation(date, 855);
            AirQualityDelta airQualityDelta = climateRanges.airQuality.variationAmplitude * climateRanges.airQuality.GetVariation(date, 1202);

            climateRanges.temperature.high.Add(temperatureDelta);
            climateRanges.temperature.low.Add(temperatureDelta);

            climateRanges.humidity.high.Add(humidityDelta);
            climateRanges.humidity.low.Add(humidityDelta);

            climateRanges.pressure.high.Add(pressureDelta);
            climateRanges.pressure.low.Add(pressureDelta);

            climateRanges.uvIndex.high.Add(uvIndexDelta);

            climateRanges.airQuality.high.Add(airQualityDelta);
            climateRanges.airQuality.low.Add(airQualityDelta);
        }

        public Season GetSeason(SeasonName season)
        {
            switch (season)
            {
                case SeasonName.Spring:
                    return spring;
                case SeasonName.Summer:
                    return summer;
                case SeasonName.Fall:
                    return fall;
                case SeasonName.Winter:
                    return winter;
            }

            return spring;
        }

        private float Wrap01(float value, float reference)
        {
            while (value < reference) value += 1f;
            return value;
        }

    }
}