//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEngine;
using System.Collections;


namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/FX/Fog FX", order = 361)]
    public class FogFX : CozyFXProfile
    {
        [Tooltip("Controls the average density of the fog.")]
        public FloatRangeOverridable fogCoverage = new(1, 0, 5, true);
        [Tooltip("Controls the average height density of the fog.")]
        public FloatRangeOverridable fogHeightCoverage = new(1, 0, 5, true);

        CozyWeather weatherManager;

        public override bool IsValid => !(weatherManager == null && InitializeEffect() == false);
        public override TransitionTimeModifier TransitionTimeModifier => transitionTimeModifier;
        [SerializeField]
        private TransitionTimeModifier transitionTimeModifier = TransitionTimeModifier.Linear;

        public override void PlayEffect(float weight)
        {
            if (!IsValid)
                return;
            
            FXParameters parameters = weatherManager.FXProvider.FXParameters;

            parameters.fogCoverage = fogCoverage.HasOverride ? Mathf.Lerp(parameters.fogCoverage, fogCoverage, Easing.Evaluate(weight, TransitionTimeModifier)) : parameters.fogCoverage;
            parameters.fogHeightCoverage = fogHeightCoverage.HasOverride ? Mathf.Clamp(parameters.fogHeightCoverage + fogHeightCoverage * Easing.Evaluate(weight, TransitionTimeModifier), 0, 5) : parameters.fogHeightCoverage;

            weatherManager.FXProvider.FXParameters = parameters;
        }

        public override void StopEffect()
        {

        }

        public override bool InitializeEffect()
        {
            if (!CozyPreferences.CurrentStyle.UseFog) return false;

            weatherManager = CozyWeather.Instance;
            return weatherManager != null;
        }

        public override void DeinitializeEffect()
        {

        }
    }
}
