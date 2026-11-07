//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Cozy
{
    [ExecuteAlways]
    [AddComponentMenu("")]
    [CozyModule("Ambience", "Ambience", "Controls a secondary weather system that runs parallel to the main system allowing for ambient noises and FX.", "Ecosystem", true)]
    public class CozyAmbienceModule : CozyStateMachineModule<AmbienceProfile>, IAmbienceModule
    {
        public AmbienceProfile defaultProfile;

        [CozySearchable("Ambiences", "Ambience profiles", "profiles")]
        [SerializeField]
        private AmbienceProfile[] possibleProfiles = new AmbienceProfile[0];

        public AmbienceProfile[] PossibleProfiles { get => possibleProfiles; set => possibleProfiles = value; }

        public AmbienceChangeMode AmbienceChangeMode => ambienceChangeMode;
        [SerializeField]
        private AmbienceChangeMode ambienceChangeMode = AmbienceChangeMode.ChangeWhenAmbienceFinishes;

        [SerializeField]
        private bool allowRepeats = false;
        public bool AllowRepeats => allowRepeats;

        public override bool UpdateStateMachineTimer => AmbienceChangeMode == AmbienceChangeMode.ChangeWhenAmbienceFinishes;

        private WeatherProfile trackedWeather = null;
        private void ChangeTrackedWeather(WeatherProfile oldProfile, WeatherProfile newProfile)
        {
            if (!Application.isPlaying) return;

            trackedWeather = newProfile;

            if (CurrentProfile == null) return;

            if (CurrentProfile.GetChance(WeatherSphere) < 0.05f)
            {
                ChangeProfile();
            }
        }

        public override void SetupStateMachine()
        {
            SetProfile(defaultProfile);
        }

        public override void InitializeModule()
        {
            base.InitializeModule();

            if (WeatherSphere.GetModule(out CozyWeatherModule weatherModule))
            {
                trackedWeather = weatherModule.CurrentProfile;
                weatherModule.OnProfileChange += ChangeTrackedWeather;
            }
        }

        public override AmbienceProfile GetNextProfile(AmbienceProfile lastProfile, ConditionSnapshot conditionSnapshot)
        {
            if (possibleProfiles.Length == 0) return defaultProfile;

            float totalWeight = 0f;
            List<(AmbienceProfile profile, float chance)> validProfiles = new();

            foreach (var ambience in possibleProfiles)
            {
                float chance = ambience.GetChance(conditionSnapshot);

                foreach (var weather in ambience.dontPlayDuring)
                    if (weather == trackedWeather) chance = 0;

                if (!allowRepeats && lastProfile == ambience) chance = 0;

                if (chance > 0)
                {
                    validProfiles.Add((ambience, chance));
                    totalWeight += chance;
                }
            }

            if (totalWeight <= 0f) return defaultProfile;

            float randomVal = Random.Range(0f, totalWeight);
            foreach (var (profile, chance) in validProfiles)
            {
                randomVal -= chance;
                if (randomVal <= 0f) return profile;
            }

            return validProfiles[^1].profile;
        }

        public AmbienceProfile GetNextAmbience(bool allowRepeats = true)
        {
            if (possibleProfiles.Length == 0) return defaultProfile;

            AmbienceProfile lastProfile = CurrentLocalProfile;
            ConditionSnapshot conditionSnapshot = ConditionSnapshot.GetConditions(WeatherSphere);

            float totalWeight = 0f;
            List<(AmbienceProfile profile, float chance)> validProfiles = new();

            foreach (var ambience in possibleProfiles)
            {
                float chance = ambience.GetChance(conditionSnapshot);

                foreach (var weather in ambience.dontPlayDuring)
                    if (weather == trackedWeather) chance = 0;

                if (!allowRepeats && lastProfile == ambience) chance = 0;

                if (chance > 0)
                {
                    validProfiles.Add((ambience, chance));
                    totalWeight += chance;
                }
            }

            if (totalWeight <= 0f) return defaultProfile;

            float randomVal = Random.Range(0f, totalWeight);
            foreach (var (profile, chance) in validProfiles)
            {
                randomVal -= chance;
                if (randomVal <= 0f) return profile;
            }

            return validProfiles[^1].profile;
        }
    }
}