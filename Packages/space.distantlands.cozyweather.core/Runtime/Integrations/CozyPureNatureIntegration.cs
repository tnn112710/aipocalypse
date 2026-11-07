//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine;

namespace DistantLands.Cozy
{
    [Serializable]
    [CozyIntegration(true, false, "Pure Nature 2", "COZY Icons/Pure Nature", "https://assetstore.unity.com/packages/tools/utilities/the-visual-engine-286827?aid=1011luj9X")]
    public class CozyBKIntegration : CozyIntegration
    {
        public override bool PackageInstalled
        {
            get
            {
#if COZY_BK
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

        [Tooltip("Base wind animate the trunks")]
        [Range(0f, 5f)]
        public float baseWindPower = 3f;
        [Tooltip("Base wind animate the trunks")]
        public float baseWindSpeed = 1f;
        [Tooltip("Bursts are managed by a moving World-Space noise that multiply the base wind speed and power")]
        [Range(0f, 10f)]
        public float burstsPower = 0.5f;
        [Tooltip("Speed of the Bursts noise")]
        public float burstsSpeed = 5f;
        [Tooltip("Size of the Bursts noise in Word-Space")]
        public float burstsScale = 10f;
        [Tooltip("Micro wind animate the leaves")]
        [Range(0f, 1f)]
        public float microPower = 0.1f;
        [Tooltip("Micro wind animate the leaves")]
        public float microSpeed = 1f;
        [Tooltip("Micro wind animate the leaves")]
        public float microFrequency = 3f;
        public float renderDistance = 30f;
        
        private static readonly int WindPower = Shader.PropertyToID("WindPower");
        private static readonly int WindSpeed = Shader.PropertyToID("WindSpeed");
        private static readonly int WindBurstsPower = Shader.PropertyToID("WindBurstsPower");
        private static readonly int WindBurstsSpeed = Shader.PropertyToID("WindBurstsSpeed");
        private static readonly int WindBurstsScale = Shader.PropertyToID("WindBurstsScale");
        private static readonly int MicroPower = Shader.PropertyToID("MicroPower");
        private static readonly int MicroSpeed = Shader.PropertyToID("MicroSpeed");
        private static readonly int MicroFrequency = Shader.PropertyToID("MicroFrequency");
        private static readonly int GrassRenderDist = Shader.PropertyToID("GrassRenderDist");

        private CozyWeather weather;
        public override void Initialize()
        {
#if COZY_BK

#endif
            if (CozyWeather.Instance != null) weather = CozyWeather.Instance;

            CozyWeather.OnCozyUpdateLoop += UpdateIntegration;

        }

        private void UpdateIntegration()
        {
#if COZY_BK
            // Pure Nature does not use an asmdef so we cannot access scripts directly. Because of this, we need to directly set the shader properties rather than using a bridge.
            if (weather.Wind != null)
            {
                Shader.SetGlobalFloat(WindPower, baseWindPower);
                Shader.SetGlobalFloat(WindSpeed, baseWindSpeed * weather.Wind.WindSpeed);
                Shader.SetGlobalFloat(WindBurstsPower, burstsPower * weather.Wind.GustingPower);
                Shader.SetGlobalFloat(WindBurstsSpeed, burstsSpeed * weather.Wind.GustingFrequency);
                Shader.SetGlobalFloat(WindBurstsScale, burstsScale);
                Shader.SetGlobalFloat(MicroPower, microPower * weather.Wind.GustingPower);
                Shader.SetGlobalFloat(MicroSpeed, microSpeed * weather.Wind.GustingFrequency);
                Shader.SetGlobalFloat(MicroFrequency, microFrequency);
                Shader.SetGlobalFloat(GrassRenderDist, renderDistance);
            }
#endif
        }

        public override void Deinitialize()
        {
#if COZY_BK

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