//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;

namespace DistantLands.Cozy
{
    [Serializable]
    [CozyIntegration(false, false, "Stylized Water 3", "COZY Icons/Stylized Water 3", "https://assetstore.unity.com/packages/vfx/shaders/stylized-water-3-287769?aid=1011luj9X")]
    public class CozyStylizedWater3Integration : CozyIntegration
    {
        public override bool PackageInstalled
        {
            get
            {
                
#if COZY_SW3
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

        }

        private void UpdateIntegration()
        {
            if (!ActiveAndEnabled) return;

        }

        public override void Deinitialize()
        {
            
        }

        public override void ApplyIntegration()
        {

        }

        public override void UnapplyIntegration()
        {

        }
    }
}