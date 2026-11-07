//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;

namespace DistantLands.Cozy
{
    [Serializable]
    [CozyIntegration(false, true, "Crest 5", "COZY Icons/Crest", "https://assetstore.unity.com/packages/tools/particles-effects/crest-water-5-oceans-rivers-lakes-268614?aid=1011luj9X")]
    public class CozyCrest5Integration : CozyIntegration
    {        public override bool PackageInstalled
        {
            get
            {
#if COZY_CREST
                return true;
#else
                return false;
#endif
            }
        }

        public override bool IntegrationInstalled
        {
            get
            {
#if COZY_CREST
                return ShaderGraphPatcher.Installed("Packages/com.waveharmonic.crest/Runtime/Shaders/Surface/Water.shadergraph");
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

        private CozyWeather weather;

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

            }


        }

        public override void Deinitialize()
        {
            CozyWeather.OnCozyUpdateLoop -= UpdateIntegration;
        }

        public override void ApplyIntegration()
        {
            ShaderGraphPatcher.PatchShader("Packages/com.waveharmonic.crest/Runtime/Shaders/Surface/Water.shadergraph", "SurfaceDescription.Emission");
        }

        public override void UnapplyIntegration()
        {
            ShaderGraphPatcher.UnpatchShader("Packages/com.waveharmonic.crest/Runtime/Shaders/Surface/Water.shadergraph", "SurfaceDescription.Emission");
        }
    }
}