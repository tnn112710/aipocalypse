//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEngine;
using System.Collections;


namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/FX/Cloud FX", order = 361)]
    public class CloudFX : CozyFXProfile
    {


        [Tooltip("Multiplier for cumulus clouds.")]
        public FloatRangeOverridable cumulusCoverage = new(1, 0, 1, true);
        [Tooltip("Multiplier for altocumulus clouds.")]
        public FloatRangeOverridable altocumulusCoverage = new(0, 0, 1, false);
        [Tooltip("Multiplier for contrails.")]
        public FloatRangeOverridable contrailsCoverage = new(0, 0, 1, false);
        [Tooltip("Multiplier for cirrostratus clouds.")]
        public FloatRangeOverridable cirrostratusCoverage = new(0, 0, 1, false);
        [Tooltip("Multiplier for cirrus clouds.")]
        public FloatRangeOverridable cirrusCoverage = new(0, 0, 1, false);
        [Tooltip("Multiplier for border clouds.")]
        public FloatRangeOverridable borderCloudsCoverage = new(0, 0, 1, false);
        [Tooltip("Multiplier for cumulonimbus clouds.")]
        public FloatRangeOverridable cumulonimbusCoverage = new(0, 0, 1, false);

        CozyWeather weatherManager;
        [SerializeField]
        private TransitionTimeModifier transitionTimeModifier = TransitionTimeModifier.Linear;

        public override bool IsValid => !(weatherManager == null && InitializeEffect() == false);
        public override TransitionTimeModifier TransitionTimeModifier => transitionTimeModifier;

        public override bool InitializeEffect()
        {
            if (!CozyPreferences.CurrentStyle.UseClouds) return false;

            weatherManager = CozyWeather.Instance;
            return weatherManager != null;
        }

        public override void PlayEffect(float weight)
        {
            if (!IsValid)
                return;

            FXParameters parameters = weatherManager.FXProvider.FXParameters;

            parameters.altocumulusCoverage = altocumulusCoverage.HasOverride ? Mathf.Clamp(parameters.altocumulusCoverage + altocumulusCoverage * Easing.Evaluate(weight, TransitionTimeModifier), 0, 2) : parameters.altocumulusCoverage;
            parameters.borderCloudsCoverage = borderCloudsCoverage.HasOverride ? Mathf.Clamp(parameters.borderCloudsCoverage + borderCloudsCoverage * Easing.Evaluate(weight, TransitionTimeModifier), 0, 2) : parameters.borderCloudsCoverage;
            parameters.cirrostratusCoverage = cirrostratusCoverage.HasOverride ? Mathf.Clamp(parameters.cirrostratusCoverage + cirrostratusCoverage * Easing.Evaluate(weight, TransitionTimeModifier), 0, 2) : parameters.cirrostratusCoverage;
            parameters.cirrusCoverage = cirrusCoverage.HasOverride ? Mathf.Clamp(parameters.cirrusCoverage + cirrusCoverage * Easing.Evaluate(weight, TransitionTimeModifier), 0, 2) : parameters.cirrusCoverage;
            parameters.contrailsCoverage = contrailsCoverage.HasOverride ? Mathf.Clamp(parameters.contrailsCoverage + contrailsCoverage * Easing.Evaluate(weight, TransitionTimeModifier), 0, 2) : parameters.contrailsCoverage;
            parameters.cumulusCoverage = cumulusCoverage.HasOverride ? Mathf.Clamp(parameters.cumulusCoverage + cumulusCoverage * Easing.Evaluate(weight, TransitionTimeModifier), 0, 2) : parameters.cumulusCoverage;
            parameters.cumulonimbusCoverage = cumulonimbusCoverage.HasOverride ? Mathf.Clamp(parameters.cumulonimbusCoverage + cumulonimbusCoverage * Easing.Evaluate(weight, TransitionTimeModifier), 0, 2) : parameters.cumulonimbusCoverage;

            weatherManager.FXProvider.FXParameters = parameters;

        }

        public override void StopEffect() { }
        public override void DeinitializeEffect() { }
    }
}
