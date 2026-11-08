//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using System.Collections.Generic;

namespace DistantLands.Cozy
{
    [ExecuteAlways]
    [AddComponentMenu("")]
    [CozyModule("Wind", "Wind", "Control wind within the COZY system.", "Ecosystem", true)]
    public class CozyWindModule : CozyBiomeModule<CozyWindModule>, IWindModule
    {
        public WindParameters WindParameters { get => windParameters; set => windParameters = value; }
        private WindParameters windParameters = new();
        private float m_Seed;

        [Tooltip("Multiplies the total wind power by a coefficient.")]
        [Range(0, 2)]
        [CozySearchable]
        public float windMultiplier = 1;
        #region WindZone
        [CozySearchable]
        public WindZone windZone;
        public float windZoneMultiplier = 1;
        #endregion
        private float m_WindTime;
        public List<WindFX> windFXes = new();

        public Vector3 WindDirection
        {
            get
            {
                return new Vector3(Mathf.Cos(Mathf.Deg2Rad * (Heading + CozyPreferences.SkyRotation)), 0, -Mathf.Sin(Mathf.Deg2Rad * (Heading + CozyPreferences.SkyRotation)));
            }
            set
            {
                if (value == Vector3.zero || Mathf.Abs(value.normalized.y) > 0.95f)
                    return;

                Vector3 newWindValue = new Vector3(value.x, 0, value.z).normalized;

                heading.Value = (Vector3.SignedAngle(CozyPreferences.North, newWindValue, Vector3.up) + 360f) % 360f;
            }
        }

        public float WindSpeed { get => windParameters.windSpeed; set => windSpeed.Value = value; }
        /// <summary>
        /// Control the strength of the wind
        /// </summary>
        public float WindStrength { get => windParameters.windStrength; set => windStrength.Value = value; }

        /// <summary>
        /// The base wind heading in degrees. Calculated clockwise from North (set in COZY Preferences)
        /// </summary>
        public float Heading
        {
            get => windParameters.heading;
            set
            {
                heading.Value = Mathf.Repeat(value, 360f);
            }
        }


        /// <summary>
        /// Gives a range that the current wind heading will sweep through over time.
        /// </summary>
        public float HeadingVariance { get => headingVariance; set => headingVariance.Value = value; }
        public float WindSpeedVariance { get => windSpeedVariance; set => windSpeedVariance.Value = value; }
        public float WindSpeedChangeRate { get => windSpeedChangeRate; set => windSpeedChangeRate = Mathf.Max(value, 0.001f); }
        public float HeadingChangeRate { get => headingChangeRate; set => headingChangeRate = Mathf.Max(value, 0.001f); }
        public float GustingPower { get => windParameters.gustingPower; set => gustingPower.Value = value; }
        public float GustingFrequency { get => windParameters.gustingFrequency; set => gustingFrequency.Value = value; }


        [SerializeField]
        private float windSpeedChangeRate = 1f;
        [SerializeField]
        private float headingChangeRate = 1f;
        [SerializeField]
        private FloatRangeOverridable windStrength = new(0, 0, 5, true);
        [SerializeField]
        private FloatRangeOverridable heading = new(0, 0, 360, true);
        [SerializeField]
        private FloatRangeOverridable headingVariance = new(15, 0, 180, true);
        [SerializeField]
        private FloatRangeOverridable windSpeed = new(5, 0, 32, true);
        [SerializeField]
        private FloatRangeOverridable windSpeedVariance = new(0.2f, 0, 1, true);
        [SerializeField]
        private MinFloatOverridable gustingFrequency = new(1f, 0, true);
        [SerializeField]
        private MinFloatOverridable gustingPower = new(1f, 0, true);

        #region Shader Settings

        [CozySearchable]
        public bool useShaders = true;


        [System.Serializable]
        public struct WindProperty
        {
            public string propertyName;
            internal int propertyID;
            public WindPropertyType propertyType;
        }
        public enum WindPropertyType
        {
            HeadingDegrees,
            HeadingRadians,
            Direction,
            WindSpeed,
            WindStrength,
            GustingPower,
            GustingFrequency,
        }

        public List<WindProperty> globalShaderProperties;

        #endregion

        public override void InitializeModule()
        {

            base.InitializeModule();

            m_WindTime = 0;
            m_Seed = Random.value * 10000;
            windParameters = new();
            FrameReset();

            globalShaderProperties ??= new List<WindProperty>();

            for (int i = 0; i < globalShaderProperties.Count; i++)
            {
                var windProperty = globalShaderProperties[i];
                windProperty.propertyID = Shader.PropertyToID(windProperty.propertyName);
                globalShaderProperties[i] = windProperty;
            }

            if (transform.parent.GetComponentInChildren<WindZone>())
                windZone = transform.parent.GetComponentInChildren<WindZone>();

        }


        public override void ApplyBiome(CozyWindModule biome, float weight)
        {
            float calculatedHeading = biome.heading + biome.HeadingVariance * (Mathf.PerlinNoise1D((m_Seed + Time.time) * biome.headingChangeRate) - 0.5f) * 2;
            float calculatedWindSpeed = Mathf.Lerp(biome.windSpeed, biome.windSpeed * biome.windSpeedVariance, Mathf.PerlinNoise1D(m_Seed * 1.5f + Time.time * biome.windSpeedChangeRate));

            if (biome.heading.HasOverride)
                windParameters.heading = Mathf.Lerp(windParameters.heading, calculatedHeading, weight);
            if (biome.windSpeed.HasOverride)
                windParameters.windSpeed = Mathf.Lerp(windParameters.windSpeed, calculatedWindSpeed, weight);
            if (biome.gustingFrequency.HasOverride)
                windParameters.gustingFrequency = Mathf.Lerp(windParameters.gustingFrequency, biome.gustingFrequency, weight);
            if (biome.gustingPower.HasOverride)
                windParameters.gustingPower = Mathf.Lerp(windParameters.gustingPower, biome.gustingPower, weight);
            if (biome.windStrength.HasOverride)
                windParameters.windStrength = Mathf.Lerp(windParameters.windStrength, biome.windStrength, weight);
        }

        public override void OnCozyUpdateLoop()
        {
            if (!Application.isPlaying) return;

            float calculatedHeading = Heading + HeadingVariance * (Mathf.PerlinNoise1D((m_Seed + Time.time) * headingChangeRate) - 0.5f) * 2;
            float calculatedWindSpeed = Mathf.Lerp(windSpeed, windSpeed * windSpeedVariance, Mathf.PerlinNoise1D(m_Seed * 1.5f + Time.time * windSpeedChangeRate));

            windParameters.heading = Mathf.Repeat(calculatedHeading, 360f);
            windParameters.windSpeed = calculatedWindSpeed;

            ApplyBiomes();

            if (windZone)
            {
                windZone.transform.forward = WindDirection;
                windZone.windMain = WindParameters.windStrength * windZoneMultiplier;
                windZone.windPulseMagnitude = WindParameters.gustingPower;
                windZone.windPulseFrequency = WindParameters.gustingFrequency;
            }

            m_WindTime += Time.deltaTime * WindParameters.windSpeed;

            if (useShaders)
            {
                Shader.SetGlobalFloat("CZY_WindTime", m_WindTime);
                Shader.SetGlobalVector("CZY_WindDirection", WindParameters.windStrength * windMultiplier * WindDirection);

                for (int i = 0; i < globalShaderProperties.Count; i++)
                {
                    var windProperty = globalShaderProperties[i];
                    if (windProperty.propertyID == 0)
                    {
                        windProperty.propertyID = Shader.PropertyToID(windProperty.propertyName);
                        globalShaderProperties[i] = windProperty;
                    }

                    switch (windProperty.propertyType)
                    {
                        case WindPropertyType.Direction:
                            Shader.SetGlobalVector(windProperty.propertyID, WindDirection);
                            break;
                        case WindPropertyType.GustingFrequency:
                            Shader.SetGlobalFloat(windProperty.propertyID, GustingFrequency);
                            break;
                        case WindPropertyType.GustingPower:
                            Shader.SetGlobalFloat(windProperty.propertyID, GustingPower);
                            break;
                        case WindPropertyType.HeadingDegrees:
                            Shader.SetGlobalFloat(windProperty.propertyID, Heading);
                            break;
                        case WindPropertyType.HeadingRadians:
                            Shader.SetGlobalFloat(windProperty.propertyID, Heading * Mathf.Deg2Rad);
                            break;
                        case WindPropertyType.WindSpeed:
                            Shader.SetGlobalFloat(windProperty.propertyID, WindSpeed);
                            break;
                        case WindPropertyType.WindStrength:
                            Shader.SetGlobalFloat(windProperty.propertyID, WindStrength);
                            break;
                    }
                }
            }

        }

        public override void FrameReset()
        {
            if (heading.HasOverride)
                windParameters.heading = heading;
            if (gustingFrequency.HasOverride)
                windParameters.gustingFrequency = gustingFrequency;
            if (gustingPower.HasOverride)
                windParameters.gustingPower = gustingPower;
            if (windStrength.HasOverride)
                windParameters.windStrength = windStrength;
            if (windSpeed.HasOverride)
                windParameters.windSpeed = windSpeed;
        }

        public override void DeinitializeModule()
        {
            if (!IsBiomeModule)
            {
                Shader.SetGlobalFloat("CZY_WindTime", 0);
                Shader.SetGlobalVector("CZY_WindDirection", Vector3.zero);
            }

            base.DeinitializeModule();
        }

    }

}