//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine;

namespace DistantLands.Cozy
{
    [Serializable]
    [CozyIntegration(true, false, "MicroSplat", "COZY Icons/MicroSplat", "https://assetstore.unity.com/packages/tools/utilities/the-visual-engine-286827?aid=1011luj9X")]
    public class CozyMicroSplatIntegration : CozyIntegration
    {

        [Header("Wetness")]
        public bool updateWetness = true;
        [Range(0f, 1f)]
        public float minWetness = 0f;
        [Range(0f, 1f)]
        public float maxWetness = 1f;
        [Header("Rain Ripples")]
        public bool updateRainRipples = true;
        [Header("Puddle Settings")]
        public bool updatePuddles = true;
        [Header("Stream Settings")]
        public bool updateStreams = true;
        [Header("Snow Settings")]
        public bool updateSnow = true;
        [Header("Wind Settings")]
        public bool updateWindStrength = true;

        private static readonly int GlobalSnowLevel = Shader.PropertyToID("_Global_SnowLevel");
        private static readonly int GlobalWetnessParams = Shader.PropertyToID("_Global_WetnessParams");
        private static readonly int GlobalPuddleParams = Shader.PropertyToID("_Global_PuddleParams");
        private static readonly int GlobalRainIntensity = Shader.PropertyToID("_Global_RainIntensity");
        private static readonly int GlobalStreamMax = Shader.PropertyToID("_Global_StreamMax");
        private static readonly int GlobalWindParticulateStrength = Shader.PropertyToID("_Global_WindParticulateStrength");
        private static readonly int GlobalSnowParticulateStrength = Shader.PropertyToID("_Global_SnowParticulateStrength");

        public override bool PackageInstalled
        {
            get
            {
#if COZY_MICROSPLAT
                return true;
#else
                return false;
#endif
            }
        }
        public override StatusType CheckStatus(out string message)
        {
            if (wind == null && !weather.GetModule(out wind))
            {
                message = "Requires a module that implements IWindModule to update wind particulate. Add a Wind Module to use the default module.";
                return StatusType.Warning;
            }
            if (precipitation == null && !weather.GetModule(out precipitation))
            {
                message = "Requires a module that implements IPrecipitationModule to update snow and rain accumulation. Add a Climate Module to use the default module.";
                return StatusType.Warning;
            }

            message = "";
            return StatusType.Pass;
        }

        public override bool IntegrationInstalled => true;

        private CozyWeather weather;
        private IWindModule wind;
        private IPrecipitationModule precipitation;

        public override void Initialize()
        {
            if (CozyWeather.Instance != null) weather = CozyWeather.Instance;
            weather.GetModule(out wind);
            weather.GetModule(out precipitation);

#if COZY_MICROSPLAT
            CozyWeather.OnCozyUpdateLoop += UpdateIntegration;
#endif
        }

        private void UpdateIntegration()
        {
#if COZY_MICROSPLAT
            UpdateMicrosplat();
#else

#endif
        }

        public void UpdateMicrosplat()
        {
            if (precipitation != null)
            {
                if (updateSnow)
                {
                    Shader.SetGlobalFloat(GlobalSnowLevel, precipitation.AccumulatedSnow);
                }
                if (updateWetness)
                {
                    float currentWetness = Mathf.Clamp(precipitation.AccumulatedRain, minWetness, maxWetness);
                    Shader.SetGlobalVector(GlobalWetnessParams, new Vector2(minWetness, currentWetness));
                }
                if (updatePuddles)
                {
                    Shader.SetGlobalFloat(GlobalPuddleParams, precipitation.AccumulatedRain);
                }
                if (updateRainRipples)
                {
                    Shader.SetGlobalFloat(GlobalRainIntensity, precipitation.AccumulatedRain);
                }
                if (updateStreams)
                {
                    Shader.SetGlobalFloat(GlobalStreamMax, precipitation.AccumulatedRain);
                }
            }

            if (wind != null)
            {
                if (updateWindStrength)
                {
                    Shader.SetGlobalFloat(GlobalWindParticulateStrength, wind.WindSpeed);

                    if (updateSnow)
                        Shader.SetGlobalFloat(GlobalSnowParticulateStrength, wind.WindSpeed);
                }
            }
        }

        public override void Deinitialize()
        {
#if COZY_MICROSPLAT
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