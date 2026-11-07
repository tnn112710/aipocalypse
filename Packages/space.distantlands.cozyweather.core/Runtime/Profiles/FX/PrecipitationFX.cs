//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEngine;
using System.Collections;


namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/FX/Precipitation FX", order = 361)]
    public class PrecipitationFX : CozyFXProfile
    {
        [Range(0, 0.05f)]
        public float rainAccumulationSpeed;
        [Range(0, 1f)]
        public float maximumRainAmount;
        [Range(0, 0.05f)]
        public float snowAccumulationSpeed;
        [Range(0, 1f)]
        public float maximumSnowAmount;
        public float weight;
        IPrecipitationModule precipitationModule;

        public override bool IsValid => !(precipitationModule == null && InitializeEffect() == false);
        public override TransitionTimeModifier TransitionTimeModifier => transitionTimeModifier;
        [SerializeField]
        private TransitionTimeModifier transitionTimeModifier = TransitionTimeModifier.Linear;

        public override void PlayEffect(float weight)
        {
            if (!IsValid)
                return;

            precipitationModule.SnowAccumulationSpeed += snowAccumulationSpeed * Easing.Evaluate(weight, TransitionTimeModifier) * (precipitationModule.AccumulatedSnow < maximumSnowAmount ? 1 : 0);
            precipitationModule.RainAccumulationSpeed += rainAccumulationSpeed * Easing.Evaluate(weight, TransitionTimeModifier) * (precipitationModule.AccumulatedRain < maximumRainAmount ? 1 : 0);

        }

        public override void StopEffect()
        {

        }

        public override bool InitializeEffect()
        {
            CozyWeather weatherManager = CozyWeather.Instance;
            if (weatherManager == null) return false;

            precipitationModule = weatherManager.Precipitation;
            if (precipitationModule == null) return false;

            return true;
        }

        public override void DeinitializeEffect()
        {

        }
    }
}
