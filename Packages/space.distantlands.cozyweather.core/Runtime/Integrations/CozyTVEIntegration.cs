//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine;

#if THE_VISUAL_ENGINE || THE_VISUAL_ENGINE_V21
using TheVisualEngine;
#endif

namespace DistantLands.Cozy
{
    [Serializable]
    [CozyIntegration(true, false, "The Visual Engine", "COZY Icons/TVE", "https://assetstore.unity.com/packages/tools/utilities/the-visual-engine-286827?aid=1011luj9X")]
    public class CozyTVEIntegration : CozyIntegration
    {
        public override bool PackageInstalled
        {
            get
            {
#if THE_VISUAL_ENGINE || THE_VISUAL_ENGINE_V21
                return true;
#else
                return false;
#endif
            }
        }
        public override StatusType CheckStatus(out string message)
        {
            message = "";
#if THE_VISUAL_ENGINE || THE_VISUAL_ENGINE_V21
            if (visualManager == null)
            {
                if (TVEManager.Instance != null)
                    visualManager = TVEManager.Instance;

                message = "No TVE instance exists in this scene. Please create one!";
                return StatusType.Fail;
            }
#endif

            return StatusType.Pass;
        }

        public override bool IntegrationInstalled => true;

#if THE_VISUAL_ENGINE || THE_VISUAL_ENGINE_V21
        private TVEManager visualManager;
#endif

        private CozyWeather weather;


        [Tooltip("Should COZY automatically assign the main light for TVE")]
        public bool assignMainLight = true;
        [Tooltip("Enable motion integration with TVE")]
        public bool enableMotionControl = true;
        [Tooltip("Enable season integration with TVE")]
        public bool enableSeasonControl = true;
        [Tooltip("Enable wetness integration with TVE")]
        public bool enableWetnessControl = true;
        [Tooltip("Enable snow integration with TVE")]
        public bool enableSnowControl = true;

        public override void Initialize()
        {
#if THE_VISUAL_ENGINE || THE_VISUAL_ENGINE_V21
            if (TVEManager.Instance != null) visualManager = TVEManager.Instance;
            if (CozyWeather.Instance != null) weather = CozyWeather.Instance;

            CozyWeather.OnCozyUpdateLoop += UpdateIntegration;
#endif
        }

        private void UpdateIntegration()
        {
#if !(THE_VISUAL_ENGINE || THE_VISUAL_ENGINE_V21)
            return;
#else

            if (!ActiveAndEnabled) return;
            if (CozyWeather.Instance == null) return;

            if (CheckStatus() > StatusType.Warning)
            {
                return;
            }


            if (weather.Precipitation != null)
            {
                if (enableWetnessControl)
                    visualManager.globalAtmoData.wetnessIntensity = weather.Precipitation.AccumulatedRain;

                if (enableSnowControl)
                    visualManager.globalAtmoData.overlayIntensity = weather.Precipitation.AccumulatedSnow;
            }

            if (weather.Calendar != null)
            {
                if (enableSeasonControl)
                {
                    visualManager.seasonControl = Mathf.Clamp(weather.Calendar.CurrentDate.YearPercentage * 4, 0, 4);
                }
            }

            if (weather.Atmosphere != null)
            {
                if (assignMainLight)
                {
                    visualManager.mainLight = weather.Atmosphere.StrongestLight;
                }
            }

            if (weather.Wind != null)
                if (enableMotionControl)
                {
                    float windPower = 0f;
                    Vector3 windDirection = Vector3.forward;

                    windPower = weather.Wind.WindStrength;
                    windDirection = weather.Wind.WindDirection;

                    visualManager.motionControl = Mathf.Clamp01(windPower);
                    visualManager.transform.LookAt(visualManager.transform.position + windDirection, Vector3.up);
                }
#endif
        }

        public override void Deinitialize()
        {
#if THE_VISUAL_ENGINE || THE_VISUAL_ENGINE_V21
            CozyWeather.OnCozyUpdateLoop -= UpdateIntegration;
#endif
        }

        public override void ApplyIntegration()
        {

        }

        public override void UnapplyIntegration()
        {

        }
    }
}