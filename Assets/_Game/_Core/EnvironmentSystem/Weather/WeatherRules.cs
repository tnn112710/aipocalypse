using System;
using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnvironmentSystem
{
    // How the weather changes on its own (WeatherSchedule): per sky weather, how often it comes in each period, how long
    // it lasts and its wind, all as weights (chance = weight / total). One asset in Data/Environment/Weather, set on WeatherSettings.
    [CreateAssetMenu(menuName = "Aipocalypse/Environment/Weather Rules", fileName = "WeatherRules")]
    public sealed class WeatherRules : ScriptableObject
    {
        [Serializable]
        public sealed class SkyRule
        {
            [Min(0f)] public float Day;
            [Min(0f)] public float Night;
            [Min(0f)] public float BloodMoon;
            [Min(0f)] public float Eclipse;
            [Tooltip("Weight per WeatherRules.DurationHours entry, same order")] public List<float> Durations = new();
            [Min(0f)] public float Calm;
            [Min(0f)] public float Breezy;
            [Min(0f)] public float Windy;
            [Min(0f)] public float Gale;

            public float Weight(WeatherPeriod period) => period switch
            {
                WeatherPeriod.Day => Day,
                WeatherPeriod.Night => Night,
                WeatherPeriod.BloodMoon => BloodMoon,
                WeatherPeriod.Eclipse => Eclipse,
                _ => 0f,
            };

            public void SetWeight(WeatherPeriod period, float weight)
            {
                switch (period)
                {
                    case WeatherPeriod.Day: Day = weight; break;
                    case WeatherPeriod.Night: Night = weight; break;
                    case WeatherPeriod.BloodMoon: BloodMoon = weight; break;
                    case WeatherPeriod.Eclipse: Eclipse = weight; break;
                }
            }

            public float WindWeight(WindLevel wind) => wind switch
            {
                WindLevel.Calm => Calm,
                WindLevel.Breezy => Breezy,
                WindLevel.Windy => Windy,
                WindLevel.Gale => Gale,
                _ => 0f,
            };

            public void SetWindWeight(WindLevel wind, float weight)
            {
                switch (wind)
                {
                    case WindLevel.Calm: Calm = weight; break;
                    case WindLevel.Breezy: Breezy = weight; break;
                    case WindLevel.Windy: Windy = weight; break;
                    case WindLevel.Gale: Gale = weight; break;
                }
            }

            public float DurationWeight(int index) => index < Durations.Count ? Mathf.Max(0f, Durations[index]) : 0f;
        }

        [Tooltip("Off: the weather only changes from the console or code")] public bool Enabled = true;
        [Range(0f, 24f), Tooltip("Hour the night table starts (a Blood Moon night belongs to the day it starts on)")]
        public float NightStart = 18f;
        [Range(0f, 24f), Tooltip("Hour the day table starts again")] public float NightEnd = 6f;
        [Tooltip("Game hours of each in-between weather on the way to a far one (min, max)")]
        public Vector2 StepHours = new(0.5f, 1f);
        [Min(0f), Tooltip("A rolled duration moves up to this many game hours either way, so changes don't land on round hours")]
        public float DurationJitter = 0.5f;
        [Min(0.25f), Tooltip("A weather cut short (a stricter period or a forced weather is coming) lasts at least this many game hours, or isn't picked")]
        public float ShortestHours = 1f;
        [Min(1f), Tooltip("Game hours planned ahead (the forecast)")] public float PlanHours = 24f;
        [Tooltip("The durations a weather can roll, in game hours; each sky weather weighs them")]
        public List<float> DurationHours = new() { 1f, 2f, 4f, 6f, 8f };
        // Unity serializes a dictionary only with [SerializeField], public or not.
        [SerializeField] public Dictionary<SkyWeather, SkyRule> Skies = new();

        public SkyRule Rule(SkyWeather sky) => Skies.TryGetValue(sky, out SkyRule rule) ? rule : null;

        public float Weight(WeatherPeriod period, SkyWeather sky) => Rule(sky) is { } rule ? Mathf.Max(0f, rule.Weight(period)) : 0f;

        public float LongestHours
        {
            get
            {
                float longest = 0f;
                foreach (float hours in DurationHours)
                    longest = Mathf.Max(longest, hours);
                return longest + DurationJitter;
            }
        }
    }
}
