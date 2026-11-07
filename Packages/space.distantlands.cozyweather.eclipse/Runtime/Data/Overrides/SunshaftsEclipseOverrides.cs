using System;
using UnityEngine;

namespace DistantLands.Cozy.Eclipse
{
    [System.Serializable]
    public class SunshaftsScreenspaceEclipseOverride : EclipseOverride
    {
        public override string Title => "Screenspace";
        public override string Category => "Sunshafts";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseSunshaftsScreenspace;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleSunshaftsScreenspace();
        [Tooltip("Set the color of the sunshafts")]
        [EclipseProperty(VarType.Color, "Color")]
        public EclipseColorValue color = new EclipseColorValue(default);
        [Tooltip("Set the brightness of the sunshafts")]
        [EclipseProperty(VarType.Float, "Intensity", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue intensity = new EclipseFloatValue(default);
        [Tooltip("Blurs the sunshafts to sets the length")]
        [EclipseProperty(VarType.Float, "Length", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue length = new EclipseFloatValue(default);
        [Tooltip("Set the step count")]
        [EclipseProperty(VarType.Float, "Steps")]
        public EclipseFloatValue steps = new EclipseFloatValue(default);
        [Tooltip("How strongly do clouds occlude sunrays")]
        [EclipseProperty(VarType.Float, "CloudMaskAmount", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue cloudMaskAmount = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            color = new EclipseColorValue(parameters.sunshaftsScreenspaceColor);
            intensity = new EclipseFloatValue(parameters.sunshaftsScreenspaceIntensity);
            length = new EclipseFloatValue(parameters.sunshaftsScreenspaceLength);
            steps = new EclipseFloatValue(parameters.sunshaftsScreenspaceSteps);
            cloudMaskAmount = new EclipseFloatValue(parameters.sunshaftsScreenspaceCloudMaskAmount);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!color.disabled)
                parameters.sunshaftsScreenspaceColor.Lerp(color.value, weight);
            if (!intensity.disabled)
                parameters.sunshaftsScreenspaceIntensity.Lerp(intensity.value, weight);
            if (!length.disabled)
                parameters.sunshaftsScreenspaceLength.Lerp(length.value, weight);
            if (!steps.disabled)
                parameters.sunshaftsScreenspaceSteps.Lerp(steps.value, weight);
            if (!cloudMaskAmount.disabled)
                parameters.sunshaftsScreenspaceCloudMaskAmount.Lerp(cloudMaskAmount.value, weight);
        }
    }


}
