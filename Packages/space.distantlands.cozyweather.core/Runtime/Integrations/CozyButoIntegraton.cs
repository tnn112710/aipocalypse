//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;

#if COZY_BUTO
using OccaSoftware.Buto.Runtime;
#endif
using UnityEngine;
using UnityEngine.Rendering;

namespace DistantLands.Cozy
{
    [Serializable]
    [CozyIntegration(true, false, "Buto", "COZY Icons/Buto", "https://assetstore.unity.com/packages/tools/utilities/the-visual-engine-286827?aid=1011luj9X")]
    public class CozyButoIntegration : CozyIntegration
    {
        [Range(0, 2)] public float fogBrightnessMultiplier = 1;
        [Range(0, 2)] public float fogDensityMultiplier = 1;
#if COZY_BUTO
        private ButoVolumetricFog buto;
#endif
        private Volume butoVolume;

        public override bool PackageInstalled
        {
            get
            {
#if COZY_BUTO
                return true;
#else
                return false;
#endif
            }
        }
        public override StatusType CheckStatus(out string message)
        {
            message = "";
#if COZY_BUTO
            if (weather == null)
                if (CozyWeather.Instance != null)
                {
                    weather = CozyWeather.Instance;
                    weather.GetModule(out atmosphereModule);
                }
                else
                {
                    message = "No COZY instance in the current scene.";
                    return StatusType.Fail;
                }

            if (atmosphereModule == null && !weather.GetModule(out atmosphereModule))
            {
                message = "Buto Integration requires an active atmosphere module.";
                return StatusType.Fail;
            }
            if (buto == null || butoVolume == null || !buto.active)
            {
                message = "No global volume with Buto found in scene.";
                return StatusType.Fail;
            }
#endif

            return StatusType.Pass;
        }

        public override bool IntegrationInstalled => true;

        private CozyAtmosphereModule atmosphereModule;
        private CozyWeather weather;


        public override void Initialize()
        {
#if COZY_BUTO
            Volume[] volumes = UnityEngine.Object.FindObjectsByType<Volume>();
            foreach (Volume volume in volumes)
            {
                if (volume.isGlobal)
                    if (volume.profile != null)
                    {
                        volume.profile.TryGet(out buto);
                    }
            }
#endif
            if (CozyWeather.Instance != null)
            {
                weather = CozyWeather.Instance;
                weather.GetModule(out atmosphereModule);
            }

            CozyWeather.OnCozyUpdateLoop += UpdateIntegration;

        }

        private void UpdateIntegration()
        {
#if COZY_BUTO
            if (CheckStatus() > StatusType.Warning)
                return;

            buto.colorInfluence.Override(1);
            buto.litColor.Override(atmosphereModule.Parameters.fogGradientColor5.value * fogBrightnessMultiplier);
            buto.shadowedColor.Override(atmosphereModule.Parameters.fogGradientColor5.value * 0.5f * fogBrightnessMultiplier);
            buto.fogDensity.Override(fogDensityMultiplier * 2 * atmosphereModule.Parameters.fogBaseDensityMultiplier.value);
#else

#endif
        }

        public override void Deinitialize()
        {
#if COZY_BUTO

#endif
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