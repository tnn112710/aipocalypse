//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine;

#if COZY_SG2
using sc.stylizedgrass.runtime;
#endif

namespace DistantLands.Cozy
{
    [Serializable]
    [CozyIntegration(true, false, "Stylized Grass 2", "COZY Icons/Stylized Grass 2", "https://assetstore.unity.com/packages/vfx/shaders/stylized-grass-shader-for-unity-6-357954?aid=1011luj9X")]
    public class CozyStylizeGrass2Integration : CozyIntegration
    {
        public override bool PackageInstalled
        {
            get
            {
#if COZY_SG2
                return true;
#else
                return false;
#endif
            }
        }
        public override bool IntegrationInstalled => true;
        
        public override StatusType CheckStatus(out string message)
        {
            message = "";
            return StatusType.Pass;   
        }
        private CozyWeather weather;


        [Tooltip("Should COZY automatically control the snow amount for Stylized Grass 2")]
        public bool controlSnow = true;
        [Tooltip("Should COZY automatically control the wetness amount for Stylized Grass 2")]
        public bool controlWetness = true;
        [Tooltip("Should COZY automatically control the wind direction and speed for Stylized Grass 2")]
        public bool controlWind = true;
        [Tooltip("Multiply COZY's wind strength by this constant before sending the wind to Stylized Grass 2")]
        public float windStrengthMultiplier = 1;
        
        private double lastFrameTime;
        private double timeOffset;
        private Vector3 windDirection;
        private Vector3 gustOffset;
        private static Vector4 globalWindParams;

        private float m_windMain;
        private float m_windTurbulence;

        private static readonly int _GrassWetness = Shader.PropertyToID("_GrassWetness");
        private static readonly int _GrassSnowCoverage = Shader.PropertyToID("_GrassSnowCoverage");
        private static readonly int _GlobalWindParams = Shader.PropertyToID("_GlobalWindParams");
        private static readonly int _GlobalWindDirection = Shader.PropertyToID("_GlobalWindDirection");
        private static readonly int _GlobalWindOffset = Shader.PropertyToID("_GlobalWindOffset");

        public override void Initialize()
        {
            if (CozyWeather.Instance != null) weather = CozyWeather.Instance;

            CozyWeather.OnCozyUpdateLoop += UpdateIntegration;
        }

        private void UpdateIntegration()
        {
            if (CozyWeather.Instance == null) return;

            if (weather.Wind != null)
            {
                // windMain = weather.Wind.WindStrength * windStrengthMultiplier;
                // windTurbulence = weather.Wind.GustingPower * windStrengthMultiplier;
                // direction = weather.Wind.WindDirection;
            }


        }

        public override void Deinitialize()
        {
            CozyWeather.OnCozyUpdateLoop -= UpdateIntegration;
        }

        public override void ApplyIntegration()
        {
            
        }

        public override void UnapplyIntegration()
        {
            
        }
    }
}