//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;

namespace DistantLands.Cozy
{
    [Serializable]
    [CozyIntegration(true, false, "Kripto Water System", "COZY Icons/Kripto Water System", "https://assetstore.unity.com/packages/tools/utilities/the-visual-engine-286827?aid=1011luj9X")]
    public class CozyKryptoWaterSystemIntegration : CozyIntegration
    {
        public override bool PackageInstalled
        {
            get
            {
                return false;
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
#if COMPILER

#endif
            if (CozyWeather.Instance != null) weather = CozyWeather.Instance;

            CozyWeather.OnCozyUpdateLoop += UpdateIntegration;

        }

        private void UpdateIntegration()
        {
#if COMPILER
            return;
#else

#endif
        }

        public override void Deinitialize()
        {
#if COMPILER

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