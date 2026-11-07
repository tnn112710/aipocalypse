using System;
using UnityEngine;

namespace DistantLands.Cozy.Eclipse
{
    [System.Serializable]
    public class FogGradientEclipseOverride : EclipseOverride
    {
        public override string Title => "Gradient";
        public override string Category => "Fog";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseFogGradient;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleFogGradient();
        [Tooltip("Sets the primary fog color band.")]
        [EclipseProperty(VarType.Color, "Color1")]
        public EclipseColorValue color1 = new EclipseColorValue(default);
        [Tooltip("Sets the secondary fog color band.")]
        [EclipseProperty(VarType.Color, "Color2")]
        public EclipseColorValue color2 = new EclipseColorValue(default);
        [Tooltip("Sets the tertiary fog color band.")]
        [EclipseProperty(VarType.Color, "Color3")]
        public EclipseColorValue color3 = new EclipseColorValue(default);
        [Tooltip("Sets the quaternary fog color band.")]
        [EclipseProperty(VarType.Color, "Color4")]
        public EclipseColorValue color4 = new EclipseColorValue(default);
        [Tooltip("Sets the quinary fog color band.")]
        [EclipseProperty(VarType.Color, "Color5")]
        public EclipseColorValue color5 = new EclipseColorValue(default);
        [Tooltip("Sets the density value for the primary fog band.")]
        [EclipseProperty(VarType.Float, "Density1", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue density1 = new EclipseFloatValue(default);
        [Tooltip("Sets the density value for the secondary fog band.")]
        [EclipseProperty(VarType.Float, "Density2", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue density2 = new EclipseFloatValue(default);
        [Tooltip("Sets the density value for the tertiary fog band.")]
        [EclipseProperty(VarType.Float, "Density3", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue density3 = new EclipseFloatValue(default);
        [Tooltip("Sets the density value for the quaternary fog band.")]
        [EclipseProperty(VarType.Float, "Density4", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue density4 = new EclipseFloatValue(default);
        [Tooltip("Sets the density value for the quinary fog band.")]
        [EclipseProperty(VarType.Float, "Density5", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue density5 = new EclipseFloatValue(default);
        [Tooltip("Sets the start distance for the primary fog band.")]
        [EclipseProperty(VarType.Float, "Start1")]
        public EclipseFloatValue start1 = new EclipseFloatValue(default);
        [Tooltip("Sets the start distance for the secondary fog band.")]
        [EclipseProperty(VarType.Float, "Start2")]
        public EclipseFloatValue start2 = new EclipseFloatValue(default);
        [Tooltip("Sets the start distance for the tertiary fog band.")]
        [EclipseProperty(VarType.Float, "Start3")]
        public EclipseFloatValue start3 = new EclipseFloatValue(default);
        [Tooltip("Sets the start distance for the quaternary fog band.")]
        [EclipseProperty(VarType.Float, "Start4")]
        public EclipseFloatValue start4 = new EclipseFloatValue(default);
        [Tooltip("Sets the start distance for the quinary fog band.")]
        [EclipseProperty(VarType.Float, "Start5")]
        public EclipseFloatValue start5 = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            color1 = new EclipseColorValue(parameters.fogGradientColor1);
            color2 = new EclipseColorValue(parameters.fogGradientColor2);
            color3 = new EclipseColorValue(parameters.fogGradientColor3);
            color4 = new EclipseColorValue(parameters.fogGradientColor4);
            color5 = new EclipseColorValue(parameters.fogGradientColor5);
            density1 = new EclipseFloatValue(parameters.fogGradientDensity1);
            density2 = new EclipseFloatValue(parameters.fogGradientDensity2);
            density3 = new EclipseFloatValue(parameters.fogGradientDensity3);
            density4 = new EclipseFloatValue(parameters.fogGradientDensity4);
            density5 = new EclipseFloatValue(parameters.fogGradientDensity5);
            start1 = new EclipseFloatValue(parameters.fogGradientStart1);
            start2 = new EclipseFloatValue(parameters.fogGradientStart2);
            start3 = new EclipseFloatValue(parameters.fogGradientStart3);
            start4 = new EclipseFloatValue(parameters.fogGradientStart4);
            start5 = new EclipseFloatValue(parameters.fogGradientStart5);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!color1.disabled)
                parameters.fogGradientColor1.Lerp(color1.value, weight);
            if (!color2.disabled)
                parameters.fogGradientColor2.Lerp(color2.value, weight);
            if (!color3.disabled)
                parameters.fogGradientColor3.Lerp(color3.value, weight);
            if (!color4.disabled)
                parameters.fogGradientColor4.Lerp(color4.value, weight);
            if (!color5.disabled)
                parameters.fogGradientColor5.Lerp(color5.value, weight);
            if (!density1.disabled)
                parameters.fogGradientDensity1.Lerp(density1.value, weight);
            if (!density2.disabled)
                parameters.fogGradientDensity2.Lerp(density2.value, weight);
            if (!density3.disabled)
                parameters.fogGradientDensity3.Lerp(density3.value, weight);
            if (!density4.disabled)
                parameters.fogGradientDensity4.Lerp(density4.value, weight);
            if (!density5.disabled)
                parameters.fogGradientDensity5.Lerp(density5.value, weight);
            if (!start1.disabled)
                parameters.fogGradientStart1.Lerp(start1.value, weight);
            if (!start2.disabled)
                parameters.fogGradientStart2.Lerp(start2.value, weight);
            if (!start3.disabled)
                parameters.fogGradientStart3.Lerp(start3.value, weight);
            if (!start4.disabled)
                parameters.fogGradientStart4.Lerp(start4.value, weight);
            if (!start5.disabled)
                parameters.fogGradientStart5.Lerp(start5.value, weight);
        }
    }

    [System.Serializable]
    public class FogSunLightFlareEclipseOverride : EclipseOverride
    {
        public override string Title => "SunLightFlare";
        public override string Category => "Fog";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseFogSunLightFlare;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleFogSunLightFlare();
        [Tooltip("Sets the color tint of the sun fog light flare.")]
        [EclipseProperty(VarType.Color, "Color")]
        public EclipseColorValue color = new EclipseColorValue(default);
        [Tooltip("Controls the squish/distortion shape of the light flare.")]
        [EclipseProperty(VarType.Float, "Squish")]
        public EclipseFloatValue squish = new EclipseFloatValue(default);
        [Tooltip("Sets the intensity brightness of the sun light flare.")]
        [EclipseProperty(VarType.Float, "Intensity")]
        public EclipseFloatValue intensity = new EclipseFloatValue(default);
        [Tooltip("Controls the falloff rate for the light flare effect.")]
        [EclipseProperty(VarType.Float, "Falloff")]
        public EclipseFloatValue falloff = new EclipseFloatValue(default);
        [Tooltip("Sets the minimum rendering distance for the light flare.")]
        [EclipseProperty(VarType.Float, "MinDistance")]
        public EclipseFloatValue minDistance = new EclipseFloatValue(default);
        [Tooltip("Sets the maximum rendering distance for the light flare.")]
        [EclipseProperty(VarType.Float, "MaxDistance")]
        public EclipseFloatValue maxDistance = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            color = new EclipseColorValue(parameters.fogSunLightFlareColor);
            squish = new EclipseFloatValue(parameters.fogSunLightFlareSquish);
            intensity = new EclipseFloatValue(parameters.fogSunLightFlareIntensity);
            falloff = new EclipseFloatValue(parameters.fogSunLightFlareFalloff);
            minDistance = new EclipseFloatValue(parameters.fogSunLightFlareMinDistance);
            maxDistance = new EclipseFloatValue(parameters.fogSunLightFlareMaxDistance);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!color.disabled)
                parameters.fogSunLightFlareColor.Lerp(color.value, weight);
            if (!squish.disabled)
                parameters.fogSunLightFlareSquish.Lerp(squish.value, weight);
            if (!intensity.disabled)
                parameters.fogSunLightFlareIntensity.Lerp(intensity.value, weight);
            if (!falloff.disabled)
                parameters.fogSunLightFlareFalloff.Lerp(falloff.value, weight);
            if (!minDistance.disabled)
                parameters.fogSunLightFlareMinDistance.Lerp(minDistance.value, weight);
            if (!maxDistance.disabled)
                parameters.fogSunLightFlareMaxDistance.Lerp(maxDistance.value, weight);
        }
    }

    [System.Serializable]
    public class FogMoonLightFlareEclipseOverride : EclipseOverride
    {
        public override string Title => "MoonLightFlare";
        public override string Category => "Fog";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseFogMoonLightFlare;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleFogMoonLightFlare();
        [Tooltip("Sets the color tint of the moon fog light flare.")]
        [EclipseProperty(VarType.Color, "Color")]
        public EclipseColorValue color = new EclipseColorValue(default);
        [Tooltip("Controls the squish/distortion shape of the light flare.")]
        [EclipseProperty(VarType.Float, "Squish")]
        public EclipseFloatValue squish = new EclipseFloatValue(default);
        [Tooltip("Sets the intensity brightness of the moon light flare.")]
        [EclipseProperty(VarType.Float, "Intensity")]
        public EclipseFloatValue intensity = new EclipseFloatValue(default);
        [Tooltip("Controls the falloff rate for the light flare effect.")]
        [EclipseProperty(VarType.Float, "Falloff")]
        public EclipseFloatValue falloff = new EclipseFloatValue(default);
        [Tooltip("Sets the minimum rendering distance for the light flare.")]
        [EclipseProperty(VarType.Float, "MinDistance")]
        public EclipseFloatValue minDistance = new EclipseFloatValue(default);
        [Tooltip("Sets the maximum rendering distance for the light flare.")]
        [EclipseProperty(VarType.Float, "MaxDistance")]
        public EclipseFloatValue maxDistance = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            color = new EclipseColorValue(parameters.fogMoonLightFlareColor);
            squish = new EclipseFloatValue(parameters.fogMoonLightFlareSquish);
            intensity = new EclipseFloatValue(parameters.fogMoonLightFlareIntensity);
            falloff = new EclipseFloatValue(parameters.fogMoonLightFlareFalloff);
            minDistance = new EclipseFloatValue(parameters.fogMoonLightFlareMinDistance);
            maxDistance = new EclipseFloatValue(parameters.fogMoonLightFlareMaxDistance);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!color.disabled)
                parameters.fogMoonLightFlareColor.Lerp(color.value, weight);
            if (!squish.disabled)
                parameters.fogMoonLightFlareSquish.Lerp(squish.value, weight);
            if (!intensity.disabled)
                parameters.fogMoonLightFlareIntensity.Lerp(intensity.value, weight);
            if (!falloff.disabled)
                parameters.fogMoonLightFlareFalloff.Lerp(falloff.value, weight);
            if (!minDistance.disabled)
                parameters.fogMoonLightFlareMinDistance.Lerp(minDistance.value, weight);
            if (!maxDistance.disabled)
                parameters.fogMoonLightFlareMaxDistance.Lerp(maxDistance.value, weight);
        }
    }

    [System.Serializable]
    public class FogTwoToneEclipseOverride : EclipseOverride
    {
        public override string Title => "TwoTone";
        public override string Category => "Fog";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseFogTwoTone;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleFogTwoTone();
        [Tooltip("Sets the fog color facing toward the sun direction.")]
        [EclipseProperty(VarType.Color, "SunSideColor")]
        public EclipseColorValue sunSideColor = new EclipseColorValue(default);
        [Tooltip("Sets the fog color facing away from the sun direction.")]
        [EclipseProperty(VarType.Color, "AntiSunSideColor")]
        public EclipseColorValue antiSunSideColor = new EclipseColorValue(default);
        [Tooltip("Controls the density of the fog facing toward the sun direction.")]
        [EclipseProperty(VarType.Float, "SunSideDensity", minFloat = 0, maxFloat = 3)]
        public EclipseFloatValue sunSideDensity = new EclipseFloatValue(default);
        [Tooltip("Controls the density of the fog facing away from the sun direction.")]
        [EclipseProperty(VarType.Float, "AntiSunSideDensity", minFloat = 0, maxFloat = 3)]
        public EclipseFloatValue antiSunSideDensity = new EclipseFloatValue(default);
        [Tooltip("Controls the blend smoothness between sun side and moon side fog.")]
        [EclipseProperty(VarType.Float, "Smoothness")]
        public EclipseFloatValue smoothness = new EclipseFloatValue(default);
        [Tooltip("Controls the directional offset separating the sun and moon fog colors.")]
        [EclipseProperty(VarType.Float, "Offset")]
        public EclipseFloatValue offset = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            sunSideColor = new EclipseColorValue(parameters.fogTwoToneSunSideColor);
            antiSunSideColor = new EclipseColorValue(parameters.fogTwoToneAntiSunSideColor);
            sunSideDensity = new EclipseFloatValue(parameters.fogTwoToneSunSideDensity);
            antiSunSideDensity = new EclipseFloatValue(parameters.fogTwoToneAntiSunSideDensity);
            smoothness = new EclipseFloatValue(parameters.fogTwoToneSmoothness);
            offset = new EclipseFloatValue(parameters.fogTwoToneOffset);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!sunSideColor.disabled)
                parameters.fogTwoToneSunSideColor.Lerp(sunSideColor.value, weight);
            if (!antiSunSideColor.disabled)
                parameters.fogTwoToneAntiSunSideColor.Lerp(antiSunSideColor.value, weight);
            if (!sunSideDensity.disabled)
                parameters.fogTwoToneSunSideDensity.Lerp(sunSideDensity.value, weight);
            if (!antiSunSideDensity.disabled)
                parameters.fogTwoToneAntiSunSideDensity.Lerp(antiSunSideDensity.value, weight);
            if (!smoothness.disabled)
                parameters.fogTwoToneSmoothness.Lerp(smoothness.value, weight);
            if (!offset.disabled)
                parameters.fogTwoToneOffset.Lerp(offset.value, weight);
        }
    }

    [System.Serializable]
    public class FogHeightEclipseOverride : EclipseOverride
    {
        public override string Title => "Height";
        public override string Category => "Fog";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseFogHeight;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleFogHeight();
        [Tooltip("Sets the color tint of the height fog.")]
        [EclipseProperty(VarType.Color, "Color")]
        public EclipseColorValue color = new EclipseColorValue(default);
        [Tooltip("Controls how quickly height fog dissipates vertically.")]
        [EclipseProperty(VarType.Float, "Falloff")]
        public EclipseFloatValue falloff = new EclipseFloatValue(default);
        [Tooltip("Controls the softness transition for height fog bounds.")]
        [EclipseProperty(VarType.Float, "Smoothness")]
        public EclipseFloatValue smoothness = new EclipseFloatValue(default);
        [Tooltip("Sets the base elevation plane where height fog starts.")]
        [EclipseProperty(VarType.Float, "BaseHeight")]
        public EclipseFloatValue baseHeight = new EclipseFloatValue(default);
        [Tooltip("Sets the overall density of the height fog layer.")]
        [EclipseProperty(VarType.Float, "Density")]
        public EclipseFloatValue density = new EclipseFloatValue(default);
        [Tooltip("Control the amount of variation")]
        [EclipseProperty(VarType.Float, "VariationAmount", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue variationAmount = new EclipseFloatValue(default);
        [Tooltip("Sets the scale of the height fog variation")]
        [EclipseProperty(VarType.Vector3, "VariationTextureScale")]
        public EclipseVector3Value variationTextureScale = new EclipseVector3Value(default);
        [Tooltip("Sets the movement speed and direction of the height fog variation")]
        [EclipseProperty(VarType.Vector3, "VariationTextureFlow")]
        public EclipseVector3Value variationTextureFlow = new EclipseVector3Value(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            color = new EclipseColorValue(parameters.fogHeightColor);
            falloff = new EclipseFloatValue(parameters.fogHeightFalloff);
            smoothness = new EclipseFloatValue(parameters.fogHeightSmoothness);
            baseHeight = new EclipseFloatValue(parameters.fogHeightBaseHeight);
            density = new EclipseFloatValue(parameters.fogHeightDensity);
            variationAmount = new EclipseFloatValue(parameters.fogHeightVariationAmount);
            variationTextureScale = new EclipseVector3Value(parameters.fogHeightVariationTextureScale);
            variationTextureFlow = new EclipseVector3Value(parameters.fogHeightVariationTextureFlow);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!color.disabled)
                parameters.fogHeightColor.Lerp(color.value, weight);
            if (!falloff.disabled)
                parameters.fogHeightFalloff.Lerp(falloff.value, weight);
            if (!smoothness.disabled)
                parameters.fogHeightSmoothness.Lerp(smoothness.value, weight);
            if (!baseHeight.disabled)
                parameters.fogHeightBaseHeight.Lerp(baseHeight.value, weight);
            if (!density.disabled)
                parameters.fogHeightDensity.Lerp(density.value, weight);
            if (!variationAmount.disabled)
                parameters.fogHeightVariationAmount.Lerp(variationAmount.value, weight);
            if (!variationTextureScale.disabled)
                parameters.fogHeightVariationTextureScale.Lerp(variationTextureScale.value, weight);
            if (!variationTextureFlow.disabled)
                parameters.fogHeightVariationTextureFlow.Lerp(variationTextureFlow.value, weight);
        }
    }

    [System.Serializable]
    public class FogBaseEclipseOverride : EclipseOverride
    {
        public override string Title => "Base";
        public override string Category => "Fog";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseFogBase;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleFogBase();
        [Tooltip("Global multiplier applied to overall fog density.")]
        [EclipseProperty(VarType.Float, "DensityMultiplier")]
        public EclipseFloatValue densityMultiplier = new EclipseFloatValue(default);
        [Tooltip("Sets the base elevation height for fog in the background.")]
        [EclipseProperty(VarType.Float, "Height", minFloat = -1, maxFloat = 1)]
        public EclipseFloatValue height = new EclipseFloatValue(default);
        [Tooltip("Controls the transition smoothness of the fog in the background.")]
        [EclipseProperty(VarType.Float, "Smoothness")]
        public EclipseFloatValue smoothness = new EclipseFloatValue(default);
        [Tooltip("Sets the base height (in meters) for the foreground fog.")]
        [EclipseProperty(VarType.Float, "BaseHeight")]
        public EclipseFloatValue baseHeight = new EclipseFloatValue(default);
        [Tooltip("Sets the base smoothness (in meters) for the foreground fog.")]
        [EclipseProperty(VarType.Float, "BaseSmoothness")]
        public EclipseFloatValue baseSmoothness = new EclipseFloatValue(default);
        [Tooltip("Sets the distance (in meters) that the fog will transition from the height-based fog (foreground) to the angle-based fog (background).")]
        [EclipseProperty(VarType.Float, "TransitionDistance")]
        public EclipseFloatValue transitionDistance = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            densityMultiplier = new EclipseFloatValue(parameters.fogBaseDensityMultiplier);
            height = new EclipseFloatValue(parameters.fogBaseHeight);
            smoothness = new EclipseFloatValue(parameters.fogBaseSmoothness);
            baseHeight = new EclipseFloatValue(parameters.fogBaseBaseHeight);
            baseSmoothness = new EclipseFloatValue(parameters.fogBaseBaseSmoothness);
            transitionDistance = new EclipseFloatValue(parameters.fogBaseTransitionDistance);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!densityMultiplier.disabled)
                parameters.fogBaseDensityMultiplier.Lerp(densityMultiplier.value, weight);
            if (!height.disabled)
                parameters.fogBaseHeight.Lerp(height.value, weight);
            if (!smoothness.disabled)
                parameters.fogBaseSmoothness.Lerp(smoothness.value, weight);
            if (!baseHeight.disabled)
                parameters.fogBaseBaseHeight.Lerp(baseHeight.value, weight);
            if (!baseSmoothness.disabled)
                parameters.fogBaseBaseSmoothness.Lerp(baseSmoothness.value, weight);
            if (!transitionDistance.disabled)
                parameters.fogBaseTransitionDistance.Lerp(transitionDistance.value, weight);
        }
    }


}
