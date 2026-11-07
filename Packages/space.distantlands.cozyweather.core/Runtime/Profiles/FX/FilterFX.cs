//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEngine;
using System.Collections;



namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/FX/Filter FX", order = 361)]
    public class FilterFX : CozyFXProfile
    {

        public FloatRangeOverridable filterSaturation = new(0, -1, 1, true);
        public FloatRangeOverridable filterValue = new(0, -1, 1, true);
        public ColorOverridable filterColor = new(Color.white, true, false, false);
        public ColorOverridable sunFilter = new(Color.white, true, false, false);
        public ColorOverridable cloudFilter = new(Color.white, true, false, false);
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

            parameters.filterSaturation = filterSaturation.HasOverride ? Mathf.Lerp(parameters.filterSaturation, filterSaturation, weight) : filterSaturation;
            parameters.filterValue = filterValue.HasOverride ? Mathf.Lerp(parameters.filterValue, filterValue, weight) : parameters.filterValue;
            parameters.filterColor = filterColor.HasOverride ? Color.Lerp(parameters.filterColor, filterColor, weight) : parameters.filterColor;
            parameters.sunFilter = sunFilter.HasOverride ? Color.Lerp(parameters.sunFilter, sunFilter, weight) : parameters.sunFilter;
            parameters.cloudFilter = cloudFilter.HasOverride ? Color.Lerp(parameters.cloudFilter, cloudFilter, weight) : parameters.cloudFilter;

            weatherManager.FXProvider.FXParameters = parameters;

        }

        public override void StopEffect()
        {

        }

        public override bool InitializeEffect()
        {
            weatherManager = CozyWeather.Instance;
            return weatherManager != null;
        }

        public override void DeinitializeEffect()
        {

        }

    }
}
