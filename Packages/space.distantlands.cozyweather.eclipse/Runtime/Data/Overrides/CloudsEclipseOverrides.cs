using System;
using UnityEngine;

namespace DistantLands.Cozy.Eclipse
{
    [System.Serializable]
    public class CloudsVolumetricEclipseOverride : EclipseOverride
    {
        public override string Title => "Volumetric";
        public override string Category => "Clouds";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseCloudsVolumetric;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleCloudsVolumetric();
        [Tooltip("Sets the height of the cumulus layer")]
        [EclipseProperty(VarType.Float, "Height")]
        public EclipseFloatValue height = new EclipseFloatValue(default);
        [Tooltip("Darkens the cloud layer in thick clouds")]
        [EclipseProperty(VarType.Float, "DarkeningIntensity")]
        public EclipseFloatValue darkeningIntensity = new EclipseFloatValue(default);
        [Tooltip("Sets the color of the lit clouds")]
        [EclipseProperty(VarType.Color, "LitColor")]
        public EclipseColorValue litColor = new EclipseColorValue(default);
        [Tooltip("Sets the color of the shaded clouds")]
        [EclipseProperty(VarType.Color, "ShadeColor")]
        public EclipseColorValue shadeColor = new EclipseColorValue(default);
        [Tooltip("Sets the primary scale for volumetric cloud placement mask.")]
        [EclipseProperty(VarType.Float, "PlacementScale1")]
        public EclipseFloatValue placementScale1 = new EclipseFloatValue(default);
        [Tooltip("Sets the secondary scale for volumetric cloud placement mask.")]
        [EclipseProperty(VarType.Float, "PlacementScale2")]
        public EclipseFloatValue placementScale2 = new EclipseFloatValue(default);
        [Tooltip("Sets the density of the volumetric layer")]
        [EclipseProperty(VarType.Float, "Density")]
        public EclipseFloatValue density = new EclipseFloatValue(default);
        [Tooltip("Sets the scale of the noise for the volumetric layer")]
        [EclipseProperty(VarType.Float, "NoiseScale")]
        public EclipseFloatValue noiseScale = new EclipseFloatValue(default);
        [Tooltip("Controls the strength of noise erosion on cloud edges.")]
        [EclipseProperty(VarType.Float, "NoiseAmount")]
        public EclipseFloatValue noiseAmount = new EclipseFloatValue(default);
        [Tooltip("Sets the base elevation height where volumetric cloud rendering starts.")]
        [EclipseProperty(VarType.Float, "PlacementHeight")]
        public EclipseFloatValue placementHeight = new EclipseFloatValue(default);
        [Tooltip("Sets the raymarching sample step distance inside the clouds.")]
        [EclipseProperty(VarType.Float, "StepSize")]
        public EclipseFloatValue stepSize = new EclipseFloatValue(default);
        [Tooltip("Sets the total number of raymarching sample iterations.")]
        [EclipseProperty(VarType.Float, "Steps")]
        public EclipseFloatValue steps = new EclipseFloatValue(default);
        [Tooltip("Controls the distance-based level of detail reduction factor.")]
        [EclipseProperty(VarType.Float, "LODCoefficient")]
        public EclipseFloatValue lODCoefficient = new EclipseFloatValue(default);
        [Tooltip("Sets the reduced raymarching step count applied at distance.")]
        [EclipseProperty(VarType.Float, "LODSteps")]
        public EclipseFloatValue lODSteps = new EclipseFloatValue(default);
        [Tooltip("Controls how strongly wind speeds move the volumetric cloud noise.")]
        [EclipseProperty(VarType.Float, "WindStrength")]
        public EclipseFloatValue windStrength = new EclipseFloatValue(default);
        [Tooltip("Sets the intensity brightness of light highlights along cloud edges.")]
        [EclipseProperty(VarType.Float, "HighlightBrightness")]
        public EclipseFloatValue highlightBrightness = new EclipseFloatValue(default);
        [Tooltip("Sets the raymarching step size for computing light highlights.")]
        [EclipseProperty(VarType.Float, "HighlightStepSize")]
        public EclipseFloatValue highlightStepSize = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            height = new EclipseFloatValue(parameters.cloudsVolumetricHeight);
            darkeningIntensity = new EclipseFloatValue(parameters.cloudsVolumetricDarkeningIntensity);
            litColor = new EclipseColorValue(parameters.cloudsVolumetricLitColor);
            shadeColor = new EclipseColorValue(parameters.cloudsVolumetricShadeColor);
            placementScale1 = new EclipseFloatValue(parameters.cloudsVolumetricPlacementScale1);
            placementScale2 = new EclipseFloatValue(parameters.cloudsVolumetricPlacementScale2);
            density = new EclipseFloatValue(parameters.cloudsVolumetricDensity);
            noiseScale = new EclipseFloatValue(parameters.cloudsVolumetricNoiseScale);
            noiseAmount = new EclipseFloatValue(parameters.cloudsVolumetricNoiseAmount);
            placementHeight = new EclipseFloatValue(parameters.cloudsVolumetricPlacementHeight);
            stepSize = new EclipseFloatValue(parameters.cloudsVolumetricStepSize);
            steps = new EclipseFloatValue(parameters.cloudsVolumetricSteps);
            lODCoefficient = new EclipseFloatValue(parameters.cloudsVolumetricLODCoefficient);
            lODSteps = new EclipseFloatValue(parameters.cloudsVolumetricLODSteps);
            windStrength = new EclipseFloatValue(parameters.cloudsVolumetricWindStrength);
            highlightBrightness = new EclipseFloatValue(parameters.cloudsVolumetricHighlightBrightness);
            highlightStepSize = new EclipseFloatValue(parameters.cloudsVolumetricHighlightStepSize);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!height.disabled)
                parameters.cloudsVolumetricHeight.Lerp(height.value, weight);
            if (!darkeningIntensity.disabled)
                parameters.cloudsVolumetricDarkeningIntensity.Lerp(darkeningIntensity.value, weight);
            if (!litColor.disabled)
                parameters.cloudsVolumetricLitColor.Lerp(litColor.value, weight);
            if (!shadeColor.disabled)
                parameters.cloudsVolumetricShadeColor.Lerp(shadeColor.value, weight);
            if (!placementScale1.disabled)
                parameters.cloudsVolumetricPlacementScale1.Lerp(placementScale1.value, weight);
            if (!placementScale2.disabled)
                parameters.cloudsVolumetricPlacementScale2.Lerp(placementScale2.value, weight);
            if (!density.disabled)
                parameters.cloudsVolumetricDensity.Lerp(density.value, weight);
            if (!noiseScale.disabled)
                parameters.cloudsVolumetricNoiseScale.Lerp(noiseScale.value, weight);
            if (!noiseAmount.disabled)
                parameters.cloudsVolumetricNoiseAmount.Lerp(noiseAmount.value, weight);
            if (!placementHeight.disabled)
                parameters.cloudsVolumetricPlacementHeight.Lerp(placementHeight.value, weight);
            if (!stepSize.disabled)
                parameters.cloudsVolumetricStepSize.Lerp(stepSize.value, weight);
            if (!steps.disabled)
                parameters.cloudsVolumetricSteps.Lerp(steps.value, weight);
            if (!lODCoefficient.disabled)
                parameters.cloudsVolumetricLODCoefficient.Lerp(lODCoefficient.value, weight);
            if (!lODSteps.disabled)
                parameters.cloudsVolumetricLODSteps.Lerp(lODSteps.value, weight);
            if (!windStrength.disabled)
                parameters.cloudsVolumetricWindStrength.Lerp(windStrength.value, weight);
            if (!highlightBrightness.disabled)
                parameters.cloudsVolumetricHighlightBrightness.Lerp(highlightBrightness.value, weight);
            if (!highlightStepSize.disabled)
                parameters.cloudsVolumetricHighlightStepSize.Lerp(highlightStepSize.value, weight);
        }
    }

    [System.Serializable]
    public class CloudsCumulonimbusEclipseOverride : EclipseOverride
    {
        public override string Title => "Cumulonimbus";
        public override string Category => "Clouds";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseCloudsCumulonimbus;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleCloudsCumulonimbus();

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            return false;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
        }
    }

    [System.Serializable]
    public class CloudsCumulusEclipseOverride : EclipseOverride
    {
        public override string Title => "Cumulus";
        public override string Category => "Clouds";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseCloudsCumulus;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleCloudsCumulus();
        [Tooltip("Sets the height of the cumulus layer")]
        [EclipseProperty(VarType.Float, "Height")]
        public EclipseFloatValue height = new EclipseFloatValue(default);
        [Tooltip("Sets the color of the lit clouds")]
        [EclipseProperty(VarType.Color, "LitColor")]
        public EclipseColorValue litColor = new EclipseColorValue(default);
        [Tooltip("Sets the color of the shaded clouds")]
        [EclipseProperty(VarType.Color, "ShadeColor")]
        public EclipseColorValue shadeColor = new EclipseColorValue(default);
        [Tooltip("Sets the thickness of the cumulus layer for fake lighting")]
        [EclipseProperty(VarType.Float, "Thickness")]
        public EclipseFloatValue thickness = new EclipseFloatValue(default);
        [Tooltip("Sets the density of the cumulus layer")]
        [EclipseProperty(VarType.Float, "Density")]
        public EclipseFloatValue density = new EclipseFloatValue(default);
        [Tooltip("Controls the movement speed of the cumulus cloud layer based on wind.")]
        [EclipseProperty(VarType.Float, "WindStrength")]
        public EclipseFloatValue windStrength = new EclipseFloatValue(default);
        [Tooltip("Sets the cohesion of the cumulus layer")]
        [EclipseProperty(VarType.Float, "Cohesion")]
        public EclipseFloatValue cohesion = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            height = new EclipseFloatValue(parameters.cloudsCumulusHeight);
            litColor = new EclipseColorValue(parameters.cloudsCumulusLitColor);
            shadeColor = new EclipseColorValue(parameters.cloudsCumulusShadeColor);
            thickness = new EclipseFloatValue(parameters.cloudsCumulusThickness);
            density = new EclipseFloatValue(parameters.cloudsCumulusDensity);
            windStrength = new EclipseFloatValue(parameters.cloudsCumulusWindStrength);
            cohesion = new EclipseFloatValue(parameters.cloudsCumulusCohesion);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!height.disabled)
                parameters.cloudsCumulusHeight.Lerp(height.value, weight);
            if (!litColor.disabled)
                parameters.cloudsCumulusLitColor.Lerp(litColor.value, weight);
            if (!shadeColor.disabled)
                parameters.cloudsCumulusShadeColor.Lerp(shadeColor.value, weight);
            if (!thickness.disabled)
                parameters.cloudsCumulusThickness.Lerp(thickness.value, weight);
            if (!density.disabled)
                parameters.cloudsCumulusDensity.Lerp(density.value, weight);
            if (!windStrength.disabled)
                parameters.cloudsCumulusWindStrength.Lerp(windStrength.value, weight);
            if (!cohesion.disabled)
                parameters.cloudsCumulusCohesion.Lerp(cohesion.value, weight);
        }
    }

    [System.Serializable]
    public class CloudsAltocumulusEclipseOverride : EclipseOverride
    {
        public override string Title => "Altocumulus";
        public override string Category => "Clouds";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseCloudsAltocumulus;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleCloudsAltocumulus();
        [Tooltip("Sets the height of the layer")]
        [EclipseProperty(VarType.Float, "Height")]
        public EclipseFloatValue height = new EclipseFloatValue(default);
        [Tooltip("Sets the scale of the cirrus texture")]
        [EclipseProperty(VarType.Vector3, "TextureScale")]
        public EclipseVector3Value textureScale = new EclipseVector3Value(default);
        [Tooltip("Sets the color of the clouds")]
        [EclipseProperty(VarType.Color, "Color")]
        public EclipseColorValue color = new EclipseColorValue(default);
        [Tooltip("Sets the density of the layer")]
        [EclipseProperty(VarType.Float, "Density")]
        public EclipseFloatValue density = new EclipseFloatValue(default);
        [Tooltip("Controls the power for each individual cloud.")]
        [EclipseProperty(VarType.Float, "Ramp")]
        public EclipseFloatValue ramp = new EclipseFloatValue(default);
        [Tooltip("Controls the movement speed of the layer based on wind.")]
        [EclipseProperty(VarType.Float, "WindStrength")]
        public EclipseFloatValue windStrength = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            height = new EclipseFloatValue(parameters.cloudsAltocumulusHeight);
            textureScale = new EclipseVector3Value(parameters.cloudsAltocumulusTextureScale);
            color = new EclipseColorValue(parameters.cloudsAltocumulusColor);
            density = new EclipseFloatValue(parameters.cloudsAltocumulusDensity);
            ramp = new EclipseFloatValue(parameters.cloudsAltocumulusRamp);
            windStrength = new EclipseFloatValue(parameters.cloudsAltocumulusWindStrength);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!height.disabled)
                parameters.cloudsAltocumulusHeight.Lerp(height.value, weight);
            if (!textureScale.disabled)
                parameters.cloudsAltocumulusTextureScale.Lerp(textureScale.value, weight);
            if (!color.disabled)
                parameters.cloudsAltocumulusColor.Lerp(color.value, weight);
            if (!density.disabled)
                parameters.cloudsAltocumulusDensity.Lerp(density.value, weight);
            if (!ramp.disabled)
                parameters.cloudsAltocumulusRamp.Lerp(ramp.value, weight);
            if (!windStrength.disabled)
                parameters.cloudsAltocumulusWindStrength.Lerp(windStrength.value, weight);
        }
    }

    [System.Serializable]
    public class CloudsCirrusEclipseOverride : EclipseOverride
    {
        public override string Title => "Cirrus";
        public override string Category => "Clouds";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseCloudsCirrus;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleCloudsCirrus();
        [Tooltip("Sets the height of the layer")]
        [EclipseProperty(VarType.Float, "Height")]
        public EclipseFloatValue height = new EclipseFloatValue(default);
        [Tooltip("Sets the scale of the texture")]
        [EclipseProperty(VarType.Vector3, "TextureScale")]
        public EclipseVector3Value textureScale = new EclipseVector3Value(default);
        [Tooltip("Sets the color of the clouds")]
        [EclipseProperty(VarType.Color, "Color")]
        public EclipseColorValue color = new EclipseColorValue(default);
        [Tooltip("Sets the density of the layer")]
        [EclipseProperty(VarType.Float, "Density")]
        public EclipseFloatValue density = new EclipseFloatValue(default);
        [Tooltip("Controls the movement speed of the cloud layer based on wind.")]
        [EclipseProperty(VarType.Float, "WindStrength")]
        public EclipseFloatValue windStrength = new EclipseFloatValue(default);
        [Tooltip("Sets the power of the layer")]
        [EclipseProperty(VarType.Float, "WispFactor")]
        public EclipseFloatValue wispFactor = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            height = new EclipseFloatValue(parameters.cloudsCirrusHeight);
            textureScale = new EclipseVector3Value(parameters.cloudsCirrusTextureScale);
            color = new EclipseColorValue(parameters.cloudsCirrusColor);
            density = new EclipseFloatValue(parameters.cloudsCirrusDensity);
            windStrength = new EclipseFloatValue(parameters.cloudsCirrusWindStrength);
            wispFactor = new EclipseFloatValue(parameters.cloudsCirrusWispFactor);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!height.disabled)
                parameters.cloudsCirrusHeight.Lerp(height.value, weight);
            if (!textureScale.disabled)
                parameters.cloudsCirrusTextureScale.Lerp(textureScale.value, weight);
            if (!color.disabled)
                parameters.cloudsCirrusColor.Lerp(color.value, weight);
            if (!density.disabled)
                parameters.cloudsCirrusDensity.Lerp(density.value, weight);
            if (!windStrength.disabled)
                parameters.cloudsCirrusWindStrength.Lerp(windStrength.value, weight);
            if (!wispFactor.disabled)
                parameters.cloudsCirrusWispFactor.Lerp(wispFactor.value, weight);
        }
    }

    [System.Serializable]
    public class CloudsContrailsEclipseOverride : EclipseOverride
    {
        public override string Title => "Contrails";
        public override string Category => "Clouds";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseCloudsContrails;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleCloudsContrails();
        [Tooltip("Sets the height of the layer")]
        [EclipseProperty(VarType.Float, "Height")]
        public EclipseFloatValue height = new EclipseFloatValue(default);
        [Tooltip("Sets the scale of the texture")]
        [EclipseProperty(VarType.Vector3, "TextureScale")]
        public EclipseVector3Value textureScale = new EclipseVector3Value(default);
        [Tooltip("Sets the color of the lit clouds")]
        [EclipseProperty(VarType.Color, "Color")]
        public EclipseColorValue color = new EclipseColorValue(default);
        [Tooltip("Sets the density of the layer")]
        [EclipseProperty(VarType.Float, "Density")]
        public EclipseFloatValue density = new EclipseFloatValue(default);
        [Tooltip("Controls the movement speed of the cloud layer based on wind.")]
        [EclipseProperty(VarType.Float, "WindStrength")]
        public EclipseFloatValue windStrength = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            height = new EclipseFloatValue(parameters.cloudsContrailsHeight);
            textureScale = new EclipseVector3Value(parameters.cloudsContrailsTextureScale);
            color = new EclipseColorValue(parameters.cloudsContrailsColor);
            density = new EclipseFloatValue(parameters.cloudsContrailsDensity);
            windStrength = new EclipseFloatValue(parameters.cloudsContrailsWindStrength);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!height.disabled)
                parameters.cloudsContrailsHeight.Lerp(height.value, weight);
            if (!textureScale.disabled)
                parameters.cloudsContrailsTextureScale.Lerp(textureScale.value, weight);
            if (!color.disabled)
                parameters.cloudsContrailsColor.Lerp(color.value, weight);
            if (!density.disabled)
                parameters.cloudsContrailsDensity.Lerp(density.value, weight);
            if (!windStrength.disabled)
                parameters.cloudsContrailsWindStrength.Lerp(windStrength.value, weight);
        }
    }


}
