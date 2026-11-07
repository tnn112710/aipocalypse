using System;
using UnityEngine;

namespace DistantLands.Cozy.Eclipse
{
    [Serializable]
    public struct SolarEclipsePrediction
    {
        public MeridiemDate Date { get; }
        public MeridiemTime Time { get; }
        public float DaysUntil { get; }
        public float EclipseRatio { get; }

        internal SolarEclipsePrediction(MeridiemDate date, MeridiemTime time, float daysUntil, float eclipseRatio)
        {
            Date = date;
            Time = time;
            DaysUntil = daysUntil;
            EclipseRatio = eclipseRatio;
        }
    }

    [ExecuteAlways]
    [AddComponentMenu("")]
    [CozyModule("Eclipse", "Eclipse", "Apply atmosphere changes during a solar eclipse.", "Atmosphere", true, 5)]
    public class EclipseModule : CozyBiomeModule<EclipseModule>
    {
        public enum EclipseStyle
        {
            Manual,
            Automatic
        }

        [SerializeField]
        private EclipseProfile profile;

        [SerializeField]
        private EclipseStyle eclipseStyle = EclipseStyle.Automatic;

        [SerializeField, Range(0f, 1f)]
        private float eclipseRatio;

        [SerializeField, Range(0.01f, 30f)]
        private float maximumAlignmentAngle = 5.7f;

        [SerializeField, Range(0f, 1.5f)]
        private float eclipseMoonSize = 0.85f;
        public float EclipseMoonSize => eclipseMoonSize;

        private CozyAtmosphereModule atmosphereModule;
        private CozyTimeModule timeModule;

        public override Type[] ModuleRequirements => DependsOn<CozyAtmosphereModule, CozyTimeModule>();

        public EclipseProfile Profile
        {
            get => profile;
            set
            {
                if (profile == value)
                    return;

                profile = value;
            }
        }

        public EclipseStyle Style
        {
            get => eclipseStyle;
            set => eclipseStyle = value;
        }

        public float EclipseRatio
        {
            get
            {
                if (eclipseStyle == EclipseStyle.Manual)
                    return Mathf.Clamp01(eclipseRatio);

                if (atmosphereModule == null)
                    return 0f;

                return CalculateAutomaticEclipseRatio(
                    atmosphereModule.SunDirection,
                    atmosphereModule.MoonDirection,
                    maximumAlignmentAngle);
            }
            set => eclipseRatio = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Finds the next future solar-eclipse alignment within the requested number of in-game days.
        /// The returned time is the predicted peak alignment time.
        /// </summary>
        public bool TryPredictNextSolarEclipse(out SolarEclipsePrediction prediction, int maximumDaysToSearch = 365)
        {
            prediction = default;
            if (maximumDaysToSearch <= 0 || !WeatherSphere ||
                !WeatherSphere.GetModule(out atmosphereModule) ||
                !WeatherSphere.GetModule(out timeModule) ||
                WeatherSphere.Calendar == null)
            {
                return false;
            }

            AtmosphereParameters parameters = atmosphereModule.Parameters;
            float orbitPeriod = parameters.skyMoonDiskOrbitPeriod;
            float declinationPeriod = parameters.skyMoonDiskDeclinationPeriod;
            if (CozyPreferences.StaticLighting ||
                parameters.skyMoonDiskPlacementMode != MoonPlacementMode.Realistic ||
                orbitPeriod <= 0f || float.IsNaN(orbitPeriod) || float.IsInfinity(orbitPeriod) ||
                declinationPeriod <= 0f || float.IsNaN(declinationPeriod) || float.IsInfinity(declinationPeriod))
            {
                return false;
            }

            MeridiemDate currentDate = WeatherSphere.Calendar.CurrentDate;
            int currentDay = currentDate.AbsoluteDay;
            float currentTime = timeModule.CurrentTime;
            float start = currentDay + currentTime;
            const float sampleInterval = 1f / 24f;

            GetEclipseRatioAt(start, currentDay, timeModule, out float previousRatio);
            float middleTime = start + sampleInterval;
            GetEclipseRatioAt(middleTime, currentDay, timeModule, out float middleRatio);
            float end = start + maximumDaysToSearch;

            for (float sampleTime = middleTime + sampleInterval; sampleTime <= end + sampleInterval; sampleTime += sampleInterval)
            {
                GetEclipseRatioAt(sampleTime, currentDay, timeModule, out float sampleRatio);
                if (middleRatio > 0f && middleRatio >= previousRatio && middleRatio >= sampleRatio)
                {
                    float peakTime = RefineEclipsePeak(
                        middleTime,
                        sampleInterval,
                        currentDay,
                        timeModule);
                    GetEclipseRatioAt(peakTime, currentDay, timeModule, out float ratio);

                    if (peakTime > start && peakTime <= end && ratio > 0f)
                    {
                        int elapsedDays = Mathf.FloorToInt(peakTime);
                        float timeOfDay = peakTime - elapsedDays;
                        MeridiemDate predictedDate = WeatherSphere.Calendar.ProgressDay
                            ? new MeridiemDate(elapsedDays)
                            : currentDate;

                        prediction = new SolarEclipsePrediction(
                            predictedDate,
                            MeridiemTime.ToClamped(timeOfDay),
                            peakTime - start,
                            ratio);
                        return true;
                    }
                }

                previousRatio = middleRatio;
                middleTime = sampleTime;
                middleRatio = sampleRatio;
            }

            return false;
        }

        public override void InitializeModule()
        {
            base.InitializeModule();
            WeatherSphere.GetModule(out atmosphereModule);

            atmosphereModule.RunAfterBiomes -= ApplyToAtmosphere;

            if (!IsBiomeModule && atmosphereModule != null)
                atmosphereModule.RunAfterBiomes += ApplyToAtmosphere;
        }

        public override void DeinitializeModule()
        {
            if (!IsBiomeModule && atmosphereModule != null)
                atmosphereModule.RunAfterBiomes -= ApplyToAtmosphere;

            if (!IsBiomeModule)
            {
                Shader.SetGlobalVector(CozyShaderIDs.skyEclipseDirection, Vector3.zero);
                Shader.SetGlobalFloat(CozyShaderIDs.skyEclipseSize, 0.95f);
                Shader.SetGlobalFloat(CozyShaderIDs.skyEclipseProgress, -1f);
            }

            base.DeinitializeModule();
        }

        private void OnValidate()
        {
            eclipseRatio = Mathf.Clamp01(eclipseRatio);
            maximumAlignmentAngle = Mathf.Max(0.01f, maximumAlignmentAngle);
            eclipseMoonSize = Mathf.Clamp(eclipseMoonSize, 0f, 1.5f);
        }

        private void ApplyToAtmosphere(CozyAtmosphereModule source)
        {
            if (source == null)
                return;

            atmosphereModule = source;
            if (!Active)
            {
                Shader.SetGlobalVector(CozyShaderIDs.skyEclipseDirection, Vector3.zero);
                Shader.SetGlobalFloat(CozyShaderIDs.skyEclipseProgress, -1f);
                return;
            }

            UpdateEclipseShader(source.MoonDirection);
            ApplyProfile(source.Parameters, 1f);
            ApplyBiomes();
        }

        public override void ApplyBiome(EclipseModule biome, float weight)
        {
            if (!Active || biome == null || !biome.Active || weight <= 0f || atmosphereModule == null)
                return;

            biome.atmosphereModule = atmosphereModule;
            UpdateEclipseShader(atmosphereModule.MoonDirection);
            biome.ApplyProfile(atmosphereModule.Parameters, weight);
        }

        private void UpdateEclipseShader(Vector3 moonDirection)
        {
            Shader.SetGlobalVector(CozyShaderIDs.skyEclipseDirection, moonDirection);
            Shader.SetGlobalFloat(CozyShaderIDs.skyEclipseSize, eclipseMoonSize);
            Shader.SetGlobalFloat(
                CozyShaderIDs.skyEclipseProgress,
                eclipseStyle == EclipseStyle.Manual ? EclipseRatio : -1f);
        }

        private void ApplyProfile(AtmosphereParameters parameters, float weight)
        {
            if (profile == null || profile.overrides == null)
                return;

            float blend = Mathf.Clamp01(EclipseRatio * weight);
            if (blend <= 0f)
                return;

            profile.ApplyToAtmosphere(parameters, blend);
        }

        public static float CalculateAutomaticEclipseRatio(
            Vector3 sunDirection,
            Vector3 moonDirection,
            float maximumAlignmentAngle)
        {
            if (maximumAlignmentAngle <= 0f ||
                sunDirection.sqrMagnitude <= Mathf.Epsilon ||
                moonDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return 0f;
            }

            float angle = Vector3.Angle(sunDirection, moonDirection);
            return Mathf.Clamp01(1f - angle / maximumAlignmentAngle) * Mathf.Clamp01(1 - sunDirection.y * 5);
        }

        private void GetEclipseRatioAt(float absoluteTime, int currentDay, CozyTimeModule clock, out float eclipseRatio)
        {
            int elapsedDays = Mathf.FloorToInt(absoluteTime);
            int day = WeatherSphere.Calendar.ProgressDay ? elapsedDays : currentDay;
            float timeOfDay = absoluteTime - elapsedDays;
            float modifiedTime = clock.GetModifiedTime(timeOfDay);
            atmosphereModule.GetCelestialDirections(day, modifiedTime, out Vector3 sunDirection, out Vector3 moonDirection);
            eclipseRatio = CalculateAutomaticEclipseRatio(sunDirection, moonDirection, maximumAlignmentAngle);
        }

        private float RefineEclipsePeak(float center, float sampleInterval, int currentDay, CozyTimeModule clock)
        {
            const float goldenRatioConjugate = 0.61803398875f;
            float left = center - sampleInterval;
            float right = center + sampleInterval;
            float first = right - (right - left) * goldenRatioConjugate;
            float second = left + (right - left) * goldenRatioConjugate;
            GetEclipseRatioAt(first, currentDay, clock, out float firstRatio);
            GetEclipseRatioAt(second, currentDay, clock, out float secondRatio);

            for (int i = 0; i < 24; i++)
            {
                if (firstRatio >= secondRatio)
                {
                    right = second;
                    second = first;
                    secondRatio = firstRatio;
                    first = right - (right - left) * goldenRatioConjugate;
                    GetEclipseRatioAt(first, currentDay, clock, out firstRatio);
                }
                else
                {
                    left = first;
                    first = second;
                    firstRatio = secondRatio;
                    second = left + (right - left) * goldenRatioConjugate;
                    GetEclipseRatioAt(second, currentDay, clock, out secondRatio);
                }
            }

            return (left + right) * 0.5f;
        }
    }
}
