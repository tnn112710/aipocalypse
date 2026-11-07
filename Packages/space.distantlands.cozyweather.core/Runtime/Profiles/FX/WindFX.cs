//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/FX/Wind FX", order = 361)]
    public class WindFX : CozyFXProfile
    {
        public FloatRangeOverridable windStrength = new(0, 0, 5, false);
        public FloatRangeOverridable heading = new(0, 0, 360, false);
        public MinFloatOverridable windSpeed = new(5, 0, true);
        public FloatRangeOverridable gustingPower = new(1f, 0, 2, false);
        public MinFloatOverridable gustingFrequency = new(1f, 0, false);
        IWindModule windModule;

        public override bool IsValid => !(windModule == null && InitializeEffect() == false);
        public override TransitionTimeModifier TransitionTimeModifier => transitionTimeModifier;
        [SerializeField]
        private TransitionTimeModifier transitionTimeModifier = TransitionTimeModifier.Linear;

        public override void PlayEffect(float weight)
        {
            if (!IsValid)
                return;

            var parameters = windModule.WindParameters;

            if (windSpeed.HasOverride)
                parameters.windSpeed += windSpeed * weight;
            if (windStrength.HasOverride)
                parameters.windStrength += windStrength * weight;
            if (heading.HasOverride)
                parameters.heading = Mathf.Lerp(parameters.heading, heading, weight);
            if (gustingPower.HasOverride)
                parameters.gustingPower += gustingPower * weight;
            if (gustingFrequency.HasOverride)
                parameters.gustingFrequency += gustingFrequency * weight;

            windModule.WindParameters = parameters;
        }

        public override bool InitializeEffect()
        {
            CozyWeather weatherManager = CozyWeather.Instance;
            if (weatherManager == null) return false;

            windModule = weatherManager.Wind;
            if (windModule == null) return false;

            return true;
        }

        public override void StopEffect()
        {

        }

        public override void DeinitializeEffect()
        {

        }
    }
}
