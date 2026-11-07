using System;
using UnityEngine;

namespace DistantLands.Cozy.Eclipse
{
    [System.Serializable]
    public class LightingSunEclipseOverride : EclipseOverride
    {
        public override string Title => "Sun";
        public override string Category => "Lighting";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseLightingSun;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleLightingSun();
        [Tooltip("Sets the color of the sun light at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Color, "LightColor")]
        public EclipseColorValue lightColor = new EclipseColorValue(default);
        [Tooltip("Sets the intensity of the sun light at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Float, "LightIntensity")]
        public EclipseFloatValue lightIntensity = new EclipseFloatValue(default);
        [Tooltip("Set the intensity of the sun light shadows")]
        [EclipseProperty(VarType.Float, "ShadowIntensity", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue shadowIntensity = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            lightColor = new EclipseColorValue(parameters.lightingSunLightColor);
            lightIntensity = new EclipseFloatValue(parameters.lightingSunLightIntensity);
            shadowIntensity = new EclipseFloatValue(parameters.lightingSunShadowIntensity);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!lightColor.disabled)
                parameters.lightingSunLightColor.Lerp(lightColor.value, weight);
            if (!lightIntensity.disabled)
                parameters.lightingSunLightIntensity.Lerp(lightIntensity.value, weight);
            if (!shadowIntensity.disabled)
                parameters.lightingSunShadowIntensity.Lerp(shadowIntensity.value, weight);
        }
    }

    [System.Serializable]
    public class LightingSunCloudShadowsEclipseOverride : EclipseOverride
    {
        public override string Title => "SunCloudShadows";
        public override string Category => "Lighting";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseLightingSunCloudShadows;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleLightingSunCloudShadows();
        [Tooltip("Sets the resolution of the cloud shadow cookie")]
        [EclipseProperty(VarType.Float, "Resolution")]
        public EclipseFloatValue resolution = new EclipseFloatValue(default);
        [Tooltip("Sets the size of the cloud shadow cookie")]
        [EclipseProperty(VarType.Float, "Size")]
        public EclipseFloatValue size = new EclipseFloatValue(default);
        [Tooltip("Sets the number of steps used when raymarching the cloud shadow")]
        [EclipseProperty(VarType.Float, "Steps")]
        public EclipseFloatValue steps = new EclipseFloatValue(default);
        [Tooltip("Sets the radius of the cloud blurring")]
        [EclipseProperty(VarType.Float, "BlurRadius")]
        public EclipseFloatValue blurRadius = new EclipseFloatValue(default);
        [Tooltip("Sets the intensity of the shadows")]
        [EclipseProperty(VarType.Float, "Intensity", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue intensity = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            resolution = new EclipseFloatValue(parameters.lightingSunCloudShadowsResolution);
            size = new EclipseFloatValue(parameters.lightingSunCloudShadowsSize);
            steps = new EclipseFloatValue(parameters.lightingSunCloudShadowsSteps);
            blurRadius = new EclipseFloatValue(parameters.lightingSunCloudShadowsBlurRadius);
            intensity = new EclipseFloatValue(parameters.lightingSunCloudShadowsIntensity);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!resolution.disabled)
                parameters.lightingSunCloudShadowsResolution.Lerp(resolution.value, weight);
            if (!size.disabled)
                parameters.lightingSunCloudShadowsSize.Lerp(size.value, weight);
            if (!steps.disabled)
                parameters.lightingSunCloudShadowsSteps.Lerp(steps.value, weight);
            if (!blurRadius.disabled)
                parameters.lightingSunCloudShadowsBlurRadius.Lerp(blurRadius.value, weight);
            if (!intensity.disabled)
                parameters.lightingSunCloudShadowsIntensity.Lerp(intensity.value, weight);
        }
    }

    [System.Serializable]
    public class LightingSunLensFlareEclipseOverride : EclipseOverride
    {
        public override string Title => "SunLensFlare";
        public override string Category => "Lighting";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseLightingSunLensFlare;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleLightingSunLensFlare();
        [Tooltip("Sets the intensity of the sun flare at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Float, "Intensity")]
        public EclipseFloatValue intensity = new EclipseFloatValue(default);
        [Tooltip("Sets the scale of the sun flare at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Float, "Scale")]
        public EclipseFloatValue scale = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            intensity = new EclipseFloatValue(parameters.lightingSunLensFlareIntensity);
            scale = new EclipseFloatValue(parameters.lightingSunLensFlareScale);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!intensity.disabled)
                parameters.lightingSunLensFlareIntensity.Lerp(intensity.value, weight);
            if (!scale.disabled)
                parameters.lightingSunLensFlareScale.Lerp(scale.value, weight);
        }
    }

    [System.Serializable]
    public class LightingMoonEclipseOverride : EclipseOverride
    {
        public override string Title => "Moon";
        public override string Category => "Lighting";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseLightingMoon;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleLightingMoon();
        [Tooltip("Sets the color of the moon light at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Color, "LightColor")]
        public EclipseColorValue lightColor = new EclipseColorValue(default);
        [Tooltip("Sets the intensity of the moon light at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Float, "LightIntensity")]
        public EclipseFloatValue lightIntensity = new EclipseFloatValue(default);
        [Tooltip("Set the intensity of the moon light shadows")]
        [EclipseProperty(VarType.Float, "ShadowIntensity", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue shadowIntensity = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            lightColor = new EclipseColorValue(parameters.lightingMoonLightColor);
            lightIntensity = new EclipseFloatValue(parameters.lightingMoonLightIntensity);
            shadowIntensity = new EclipseFloatValue(parameters.lightingMoonShadowIntensity);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!lightColor.disabled)
                parameters.lightingMoonLightColor.Lerp(lightColor.value, weight);
            if (!lightIntensity.disabled)
                parameters.lightingMoonLightIntensity.Lerp(lightIntensity.value, weight);
            if (!shadowIntensity.disabled)
                parameters.lightingMoonShadowIntensity.Lerp(shadowIntensity.value, weight);
        }
    }

    [System.Serializable]
    public class LightingMoonLensFlareEclipseOverride : EclipseOverride
    {
        public override string Title => "MoonLensFlare";
        public override string Category => "Lighting";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseLightingMoonLensFlare;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleLightingMoonLensFlare();
        [Tooltip("Sets the intensity of the sun flare at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Float, "Intensity")]
        public EclipseFloatValue intensity = new EclipseFloatValue(default);
        [Tooltip("Sets the scale of the sun flare at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Float, "Scale")]
        public EclipseFloatValue scale = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            intensity = new EclipseFloatValue(parameters.lightingMoonLensFlareIntensity);
            scale = new EclipseFloatValue(parameters.lightingMoonLensFlareScale);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!intensity.disabled)
                parameters.lightingMoonLensFlareIntensity.Lerp(intensity.value, weight);
            if (!scale.disabled)
                parameters.lightingMoonLensFlareScale.Lerp(scale.value, weight);
        }
    }

    [System.Serializable]
    public class LightingAmbientEclipseOverride : EclipseOverride
    {
        public override string Title => "Ambient";
        public override string Category => "Lighting";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseLightingAmbient;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleLightingAmbient();
        [Tooltip("Sets the color of the sun light at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Color, "ZenithColor")]
        public EclipseColorValue zenithColor = new EclipseColorValue(default);
        [Tooltip("Sets the color of the horizon (or middle) of the skybox at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Color, "HorizonColor")]
        public EclipseColorValue horizonColor = new EclipseColorValue(default);
        [Tooltip("Sets the color of the ground (or bottom) of the skybox at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Color, "GroundColor")]
        public EclipseColorValue groundColor = new EclipseColorValue(default);
        [Tooltip("Sets the intensity of the ambient lighting.")]
        [EclipseProperty(VarType.Float, "Intensity")]
        public EclipseFloatValue intensity = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            zenithColor = new EclipseColorValue(parameters.lightingAmbientZenithColor);
            horizonColor = new EclipseColorValue(parameters.lightingAmbientHorizonColor);
            groundColor = new EclipseColorValue(parameters.lightingAmbientGroundColor);
            intensity = new EclipseFloatValue(parameters.lightingAmbientIntensity);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!zenithColor.disabled)
                parameters.lightingAmbientZenithColor.Lerp(zenithColor.value, weight);
            if (!horizonColor.disabled)
                parameters.lightingAmbientHorizonColor.Lerp(horizonColor.value, weight);
            if (!groundColor.disabled)
                parameters.lightingAmbientGroundColor.Lerp(groundColor.value, weight);
            if (!intensity.disabled)
                parameters.lightingAmbientIntensity.Lerp(intensity.value, weight);
        }
    }


}
