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

        public float windStrengthMultiplier = 1;

#if ZEPHYR
        private ZephyrWind zephyr;
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
            return StatusType.Pass;
        }

        public override bool IntegrationInstalled => true;

        private CozyWeather weather;

        public override void Initialize()
        {
#if ZEPHYR

#endif
            if (CozyWeather.Instance != null) weather = CozyWeather.Instance;

            CozyWeather.OnCozyUpdateLoop += UpdateIntegration;

        }

        private void UpdateIntegration()
        {
#if ZEPHYR
            return;
#else

#endif
        }

        public override void Deinitialize()
        {
#if ZEPHYR

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