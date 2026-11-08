//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
#if ZEPHYR
using DistantLands.Zephyr;
#endif
using UnityEngine;

namespace DistantLands.Cozy
{
    [Serializable]
    [CozyIntegration(true, false, "Zephyr", "COZY Icons/Zephyr", "https://assetstore.unity.com/packages/tools/utilities/the-visual-engine-286827?aid=1011luj9X")]
    public class CozyZephyrIntegration : CozyIntegration
    {

        [Min(0)]
        [Tooltip("Scales COZY wind strength before it is sent to Zephyr.")]
        public float windStrengthMultiplier = 1;

#if ZEPHYR
        private ZephyrWind zephyr;
        private ZephyrWind configuredZephyr;
        private Vector3 originalTargetDirection;
        private float originalTargetWindStrength;
        private float originalGustStrength;
        private float originalGustSpeed;
        private bool hasOriginalSettings;
#endif

        public override bool PackageInstalled
        {
            get
            {
#if ZEPHYR
                return true;
#else
                return false;
#endif
            }
        }
        public override StatusType CheckStatus(out string message)
        {
            message = "";
#if ZEPHYR
            if (weather == null)
                weather = CozyWeather.Instance;

            if (weather == null)
            {
                message = "No COZY instance in the current scene.";
                return StatusType.Fail;
            }

            if (windModule == null && !weather.GetModule(out windModule))
            {
                message = "Zephyr Integration requires an active wind module.";
                return StatusType.Fail;
            }

            if (zephyr == null)
                zephyr = ZephyrWind.Instance;

            if (zephyr == null)
            {
                message = "No Zephyr instance in the current scene.";
                return StatusType.Fail;
            }
#endif
            return StatusType.Pass;
        }

        public override bool IntegrationInstalled => true;

        private CozyWeather weather;
        private IWindModule windModule;

        public override void Initialize()
        {
#if ZEPHYR
            weather = CozyWeather.Instance;
            zephyr = ZephyrWind.Instance;

            CaptureOriginalSettings();

            if (weather != null)
                weather.GetModule(out windModule);
#endif

            CozyWeather.OnCozyUpdateLoop += UpdateIntegration;

        }

        private void UpdateIntegration()
        {
            if (!Application.isPlaying) return;
#if ZEPHYR
            if (!ActiveAndEnabled || CheckStatus() > StatusType.Warning)
                return;

            zephyr.TargetDirection = windModule.WindDirection;
            zephyr.targetWindStrength = windModule.WindStrength * windStrengthMultiplier;
            zephyr.gustStrength = windModule.GustingPower;
            zephyr.gustSpeed = windModule.GustingFrequency;
#endif
        }

        public override void Deinitialize()
        {
#if ZEPHYR
            RestoreOriginalSettings();
#endif
            CozyWeather.OnCozyUpdateLoop -= UpdateIntegration;
        }

        public override void ApplyIntegration()
        {

        }

        public override void UnapplyIntegration()
        {
            
        }

#if ZEPHYR
        private void CaptureOriginalSettings()
        {
            if (!zephyr) return;
            if (configuredZephyr == zephyr && hasOriginalSettings)
                return;

            RestoreOriginalSettings();

            configuredZephyr = zephyr;
            originalTargetDirection = zephyr.TargetDirection;
            originalTargetWindStrength = zephyr.targetWindStrength;
            originalGustStrength = zephyr.gustStrength;
            originalGustSpeed = zephyr.gustSpeed;
            hasOriginalSettings = true;
        }

        private void RestoreOriginalSettings()
        {
            if (!zephyr) return;
            if (!hasOriginalSettings || configuredZephyr == null)
                return;

            configuredZephyr.TargetDirection = originalTargetDirection;
            configuredZephyr.targetWindStrength = originalTargetWindStrength;
            configuredZephyr.gustStrength = originalGustStrength;
            configuredZephyr.gustSpeed = originalGustSpeed;

            configuredZephyr = null;
            hasOriginalSettings = false;
        }
#endif
    }
}