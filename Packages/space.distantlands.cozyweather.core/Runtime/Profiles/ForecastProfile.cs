//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Stores a list of weather profiles and the forecasting behavior for each profile. For raw chance, use the chance effectors on the individual weather profiles.
    /// </summary>
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/Forecast Profile", order = 361)]
    public class ForecastProfile : CozyProfile, ISerializationCallbackReceiver
    {
        [Serializable]
        public struct ForecastableWeather
        {
            public WeatherProfile profile;
            public ForecastNext[] forecastNextBehavior;
            public bool CanForecastNext(WeatherProfile targetProfile)
            {
                bool isIncluded = false;

                foreach (ForecastNext rule in forecastNextBehavior)
                {
                    if (RuleMatchesProfile(rule, targetProfile))
                    {
                        if (rule.modifierType == ForecastNext.ForecastNextModifierType.ForceExclude)
                        {
                            return false;
                        }

                        if (rule.modifierType == ForecastNext.ForecastNextModifierType.ForceInclude)
                        {
                            isIncluded = true;
                        }

                        if (rule.modifierType == ForecastNext.ForecastNextModifierType.ModifiedChance)
                        {
                            if (rule.chanceMultiplier <= 0f) return false;
                            isIncluded = true;
                        }
                    }
                }

                return isIncluded;
            }

            public bool CanForecastNext(WeatherProfile targetProfile, out float chance)
            {
                bool isIncluded = false;
                chance = 1;

                foreach (ForecastNext rule in forecastNextBehavior)
                {
                    if (RuleMatchesProfile(rule, targetProfile))
                    {
                        if (rule.modifierType == ForecastNext.ForecastNextModifierType.ForceExclude)
                        {
                            return false;
                        }

                        if (rule.modifierType == ForecastNext.ForecastNextModifierType.ForceInclude)
                        {
                            isIncluded = true;
                        }

                        if (rule.modifierType == ForecastNext.ForecastNextModifierType.ModifiedChance)
                        {
                            chance = rule.chanceMultiplier;
                            if (rule.chanceMultiplier <= 0f) return false;
                            isIncluded = true;
                        }
                    }
                }

                return isIncluded;
            }

            public bool CanForecastNext(WeatherProfile targetProfile, out ForecastNext appliedRule)
            {
                bool isIncluded = false;
                appliedRule = new ForecastNext();

                foreach (ForecastNext rule in forecastNextBehavior)
                {
                    if (RuleMatchesProfile(rule, targetProfile))
                    {
                        if (rule.modifierType == ForecastNext.ForecastNextModifierType.ForceExclude)
                        {
                            appliedRule = rule;
                            return false;
                        }

                        if (rule.modifierType == ForecastNext.ForecastNextModifierType.ForceInclude)
                        {
                            isIncluded = true;
                            appliedRule = rule;
                        }

                        if (rule.modifierType == ForecastNext.ForecastNextModifierType.ModifiedChance)
                        {
                            if (rule.chanceMultiplier <= 0f)
                            {
                                appliedRule = rule;
                                return false;
                            }
                            isIncluded = true;
                            appliedRule = rule;
                        }
                    }
                }

                return isIncluded;
            }

            private bool RuleMatchesProfile(ForecastNext rule, WeatherProfile targetProfile)
            {
                switch (rule.method)
                {
                    case ForecastNext.ForecastNextMethod.Any:
                        return true;
                    case ForecastNext.ForecastNextMethod.ByTag:
                        if ((targetProfile.Tag & rule.tag) != 0) return true;
                        break;
                    case ForecastNext.ForecastNextMethod.ByProfile:
                        if (Array.IndexOf(rule.profiles, targetProfile) != -1) return true;
                        break;
                }
                return false;
            }
        }

        /// <summary>
        /// Defines a weather profile shift that will happen if a certain condition is met.
        /// </summary>
        [Serializable]
        public struct LateralWeather
        {
            public struct ShiftCondition
            {
                public enum LateralShiftQualification
                {
                    Time,
                    Day,
                    Humidity,
                    Temperature
                }
                public LateralShiftQualification type;
                public bool Evaluate(CozyWeather cozyWeather)
                {
                    return true;
                }
            }
        }

        [Serializable]
        public struct ForecastNext
        {
            public enum ForecastNextMethod
            {
                Any,
                ByTag,
                ByProfile
            }
            public enum ForecastNextModifierType
            {
                ForceInclude,
                ForceExclude,
                ModifiedChance
            }
            public ForecastNextMethod method;
            public ForecastNextModifierType modifierType;
            public WeatherProfile[] profiles;
            public WeatherTag tag;
            [Range(0, 1)]
            public float chanceMultiplier;
        }

        [Tooltip("The weather profiles that this profile will forecast.")]
        public List<ForecastableWeather> profiles;
        [Tooltip("How many profiles ahead will the forecast schedule?")]
        [FormerlySerializedAs("forecastAhead")]
        [Min(1)]
        public int forecastAheadAmount = 5;

        public HashSet<WeatherProfile> ForecastableWeatherProfiles
        {
            get
            {
                HashSet<WeatherProfile> forecastableProfiles = new();

                if (profiles == null) profiles = new();

                foreach (ForecastableWeather forecastable in profiles)
                {
                    forecastableProfiles.Add(forecastable.profile);
                }

                return forecastableProfiles;
            }
        }
        public int ForecastableWeatherProfileCount => _forecastableProfilesHash.Count;
        public WeatherProfile RandomWeatherProfile => profiles[UnityEngine.Random.Range(0, profiles.Count)].profile;
        private Dictionary<WeatherProfile, ForecastableWeather> _forecastingBehaviorDictionary = new();
        private HashSet<WeatherProfile> _forecastableProfilesHash = new();

        /// <summary>
        /// Gets the forecasting behavior for this weather profile based on this forecast profile.
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        public ForecastableWeather GetForecastingBehavior(WeatherProfile profile)
        {
            if (!_forecastingBehaviorDictionary.ContainsKey(profile))
            {
                Debug.LogError("Trying to access profile that does not exist in the forecast profile. Returning empty forecast data");
                return new ForecastableWeather();
            }

            return _forecastingBehaviorDictionary[profile];
        }

        /// <summary>
        /// Returns true if this weather profile is a part of the forecast profile
        /// </summary>
        public bool HasProfile(WeatherProfile profile) => _forecastableProfilesHash != null && _forecastableProfilesHash.Contains(profile);

        List<(WeatherProfile profile, float chance)> hashSetCache;

        /// <summary>
        /// Get a hash set of all of the profiles that can potentially follow this particular weather profile.
        /// </summary>
        public List<(WeatherProfile profile, float chance)> PotentialNextProfiles(WeatherProfile initialProfile)
        {
            hashSetCache ??= new List<(WeatherProfile profile, float chance)>();
            hashSetCache.Clear();

            if (!_forecastableProfilesHash.Contains(initialProfile)) return hashSetCache; // Return all profiles if this is not part of the current profile.

            ForecastableWeather cachedBehavior = _forecastingBehaviorDictionary[initialProfile];

            foreach (var profile in _forecastableProfilesHash)
                if (cachedBehavior.CanForecastNext(profile, out float chance))
                    hashSetCache.Add((profile, chance));

            return hashSetCache;
        }

        public void OnBeforeSerialize()
        {

        }

        public void OnAfterDeserialize()
        {
            _forecastingBehaviorDictionary.Clear();

            foreach (var item in profiles)
            {
                if (item.profile != null && !_forecastingBehaviorDictionary.ContainsKey(item.profile))
                {
                    _forecastingBehaviorDictionary.Add(item.profile, item);
                    _forecastableProfilesHash.Add(item.profile);
                }
            }
        }

    }

}

