using System;
using UnityEngine;

namespace DistantLands.Cozy.Eclipse
{
    [System.Serializable]
    public class SkyGradientEclipseOverride : EclipseOverride
    {
        public override string Title => "Gradient";
        public override string Category => "Sky";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseSkyGradient;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleSkyGradient();
        [Tooltip("Sets the color of the zenith (or top) of the skybox at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Color, "ZenithColor")]
        public EclipseColorValue zenithColor = new EclipseColorValue(default);
        [Tooltip("Sets the color of the horizon (or middle) of the skybox at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Color, "HorizonColor")]
        public EclipseColorValue horizonColor = new EclipseColorValue(default);
        [Tooltip("Sets the color of the ground (or bottom) of the skybox at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Color, "GroundColor")]
        public EclipseColorValue groundColor = new EclipseColorValue(default);
        [Tooltip("Controls the exponent used to modulate from the horizon color to the zenith color of the sky.")]
        [EclipseProperty(VarType.Float, "ZenithExponent", minFloat = 0.1f, maxFloat = 10f)]
        public EclipseFloatValue zenithExponent = new EclipseFloatValue(default);
        [Tooltip("Controls the exponent used to modulate from the horizon color to the ground color of the sky.")]
        [EclipseProperty(VarType.Float, "GroundExponent", minFloat = 0.1f, maxFloat = 10f)]
        public EclipseFloatValue groundExponent = new EclipseFloatValue(default);
        [Tooltip("Controls the vertical position offset of the sky gradient.")]
        [EclipseProperty(VarType.Float, "Offset", minFloat = -1, maxFloat = 1)]
        public EclipseFloatValue offset = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            zenithColor = new EclipseColorValue(parameters.skyGradientZenithColor);
            horizonColor = new EclipseColorValue(parameters.skyGradientHorizonColor);
            groundColor = new EclipseColorValue(parameters.skyGradientGroundColor);
            zenithExponent = new EclipseFloatValue(parameters.skyGradientZenithExponent);
            groundExponent = new EclipseFloatValue(parameters.skyGradientGroundExponent);
            offset = new EclipseFloatValue(parameters.skyGradientOffset);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!zenithColor.disabled)
                parameters.skyGradientZenithColor.Lerp(zenithColor.value, weight);
            if (!horizonColor.disabled)
                parameters.skyGradientHorizonColor.Lerp(horizonColor.value, weight);
            if (!groundColor.disabled)
                parameters.skyGradientGroundColor.Lerp(groundColor.value, weight);
            if (!zenithExponent.disabled)
                parameters.skyGradientZenithExponent.Lerp(zenithExponent.value, weight);
            if (!groundExponent.disabled)
                parameters.skyGradientGroundExponent.Lerp(groundExponent.value, weight);
            if (!offset.disabled)
                parameters.skyGradientOffset.Lerp(offset.value, weight);
        }
    }

    [System.Serializable]
    public class SkyTwoToneEclipseOverride : EclipseOverride
    {
        public override string Title => "TwoTone";
        public override string Category => "Sky";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseSkyTwoTone;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleSkyTwoTone();
        [Tooltip("Sets the color of the sun side of the horizon at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Color, "SunSideColor")]
        public EclipseColorValue sunSideColor = new EclipseColorValue(default);
        [Tooltip("Sets the color opposite to the sun side of the horizon at a certain time. Starts and ends at midnight.")]
        [EclipseProperty(VarType.Color, "AntiSunSideColor")]
        public EclipseColorValue antiSunSideColor = new EclipseColorValue(default);
        [Tooltip("Controls the offset used to modulate from the sun side color to the anti-sun side color of the sky.")]
        [EclipseProperty(VarType.Float, "Offset", minFloat = -1f, maxFloat = 1f)]
        public EclipseFloatValue offset = new EclipseFloatValue(default);
        [Tooltip("Controls the exponent used to modulate from the sun side color to the anti-sun side color of the sky.")]
        [EclipseProperty(VarType.Float, "Exponent", minFloat = 0.1f, maxFloat = 10f)]
        public EclipseFloatValue exponent = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            sunSideColor = new EclipseColorValue(parameters.skyTwoToneSunSideColor);
            antiSunSideColor = new EclipseColorValue(parameters.skyTwoToneAntiSunSideColor);
            offset = new EclipseFloatValue(parameters.skyTwoToneOffset);
            exponent = new EclipseFloatValue(parameters.skyTwoToneExponent);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!sunSideColor.disabled)
                parameters.skyTwoToneSunSideColor.Lerp(sunSideColor.value, weight);
            if (!antiSunSideColor.disabled)
                parameters.skyTwoToneAntiSunSideColor.Lerp(antiSunSideColor.value, weight);
            if (!offset.disabled)
                parameters.skyTwoToneOffset.Lerp(offset.value, weight);
            if (!exponent.disabled)
                parameters.skyTwoToneExponent.Lerp(exponent.value, weight);
        }
    }

    [System.Serializable]
    public class SkyStarsEclipseOverride : EclipseOverride
    {
        public override string Title => "Stars";
        public override string Category => "Sky";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseSkyStars;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleSkyStars();
        [Tooltip("Sets the color of the star cubemap")]
        [EclipseProperty(VarType.Color, "Color")]
        public EclipseColorValue color = new EclipseColorValue(default);
        [Tooltip("Multiplies the color for the stars by a set value")]
        [EclipseProperty(VarType.Float, "Intensity", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue intensity = new EclipseFloatValue(default);
        [Tooltip("Rotates the cubemap around the primary axis. For pitch rotation, use the pitch setting in the COZY Preferences section.")]
        [EclipseProperty(VarType.Float, "RotationOffset", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue rotationOffset = new EclipseFloatValue(default);
        [Tooltip("Sets the amount for stars to fade out when twinkling. A twinkle amount of 0 means no fade, and a twinkle amount of 1 fades completely.")]
        [EclipseProperty(VarType.Float, "TwinkleAmount", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue twinkleAmount = new EclipseFloatValue(default);
        [Tooltip("Sets the rate at which stars twinkle.")]
        [EclipseProperty(VarType.Float, "TwinkleSpeed")]
        public EclipseFloatValue twinkleSpeed = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            color = new EclipseColorValue(parameters.skyStarsColor);
            intensity = new EclipseFloatValue(parameters.skyStarsIntensity);
            rotationOffset = new EclipseFloatValue(parameters.skyStarsRotationOffset);
            twinkleAmount = new EclipseFloatValue(parameters.skyStarsTwinkleAmount);
            twinkleSpeed = new EclipseFloatValue(parameters.skyStarsTwinkleSpeed);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!color.disabled)
                parameters.skyStarsColor.Lerp(color.value, weight);
            if (!intensity.disabled)
                parameters.skyStarsIntensity.Lerp(intensity.value, weight);
            if (!rotationOffset.disabled)
                parameters.skyStarsRotationOffset.Lerp(rotationOffset.value, weight);
            if (!twinkleAmount.disabled)
                parameters.skyStarsTwinkleAmount.Lerp(twinkleAmount.value, weight);
            if (!twinkleSpeed.disabled)
                parameters.skyStarsTwinkleSpeed.Lerp(twinkleSpeed.value, weight);
        }
    }

    [System.Serializable]
    public class SkyConstellationsEclipseOverride : EclipseOverride
    {
        public override string Title => "Constellations";
        public override string Category => "Sky";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseSkyConstellations;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleSkyConstellations();
        [Tooltip("Sets the color of the constellation cubemap. This is multiplied with the star color.")]
        [EclipseProperty(VarType.Color, "Color")]
        public EclipseColorValue color = new EclipseColorValue(default);
        [Tooltip("Multiplies the color for the constellations by a set value")]
        [EclipseProperty(VarType.Float, "Intensity", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue intensity = new EclipseFloatValue(default);
        [Tooltip("Sets the amount for constellations to fade out one at a time. Constellations use the value in the green channel to determine their offset.")]
        [EclipseProperty(VarType.Float, "ShimmerAmount", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue shimmerAmount = new EclipseFloatValue(default);
        [Tooltip("How quickly will constellations fade in and out.")]
        [EclipseProperty(VarType.Float, "ShimmerSpeed")]
        public EclipseFloatValue shimmerSpeed = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            color = new EclipseColorValue(parameters.skyConstellationsColor);
            intensity = new EclipseFloatValue(parameters.skyConstellationsIntensity);
            shimmerAmount = new EclipseFloatValue(parameters.skyConstellationsShimmerAmount);
            shimmerSpeed = new EclipseFloatValue(parameters.skyConstellationsShimmerSpeed);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!color.disabled)
                parameters.skyConstellationsColor.Lerp(color.value, weight);
            if (!intensity.disabled)
                parameters.skyConstellationsIntensity.Lerp(intensity.value, weight);
            if (!shimmerAmount.disabled)
                parameters.skyConstellationsShimmerAmount.Lerp(shimmerAmount.value, weight);
            if (!shimmerSpeed.disabled)
                parameters.skyConstellationsShimmerSpeed.Lerp(shimmerSpeed.value, weight);
        }
    }

    [System.Serializable]
    public class SkyGalaxyEclipseOverride : EclipseOverride
    {
        public override string Title => "Galaxy";
        public override string Category => "Sky";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseSkyGalaxy;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleSkyGalaxy();
        [Tooltip("Sets the main tint color of the galaxy.")]
        [EclipseProperty(VarType.Color, "PrimaryColor")]
        public EclipseColorValue primaryColor = new EclipseColorValue(default);
        [Tooltip("Sets the secondary accent color of the galaxy.")]
        [EclipseProperty(VarType.Color, "SecondaryColor")]
        public EclipseColorValue secondaryColor = new EclipseColorValue(default);
        [Tooltip("Sets the tertiary accent color of the galaxy.")]
        [EclipseProperty(VarType.Color, "TertiaryColor")]
        public EclipseColorValue tertiaryColor = new EclipseColorValue(default);
        [Tooltip("Multiplies the overall intensity of the galaxy.")]
        [EclipseProperty(VarType.Float, "Intensity", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue intensity = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            primaryColor = new EclipseColorValue(parameters.skyGalaxyPrimaryColor);
            secondaryColor = new EclipseColorValue(parameters.skyGalaxySecondaryColor);
            tertiaryColor = new EclipseColorValue(parameters.skyGalaxyTertiaryColor);
            intensity = new EclipseFloatValue(parameters.skyGalaxyIntensity);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!primaryColor.disabled)
                parameters.skyGalaxyPrimaryColor.Lerp(primaryColor.value, weight);
            if (!secondaryColor.disabled)
                parameters.skyGalaxySecondaryColor.Lerp(secondaryColor.value, weight);
            if (!tertiaryColor.disabled)
                parameters.skyGalaxyTertiaryColor.Lerp(tertiaryColor.value, weight);
            if (!intensity.disabled)
                parameters.skyGalaxyIntensity.Lerp(intensity.value, weight);
        }
    }

    [System.Serializable]
    public class SkyRainbowEclipseOverride : EclipseOverride
    {
        public override string Title => "Rainbow";
        public override string Category => "Sky";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseSkyRainbow;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleSkyRainbow();
        [Tooltip("Controls the rotational offset and position of the rainbow in the sky.")]
        [EclipseProperty(VarType.Float, "Placement", minFloat = -1, maxFloat = 1)]
        public EclipseFloatValue placement = new EclipseFloatValue(default);
        [Tooltip("Sets the width of the rainbow arc.")]
        [EclipseProperty(VarType.Float, "Width")]
        public EclipseFloatValue width = new EclipseFloatValue(default);
        [Tooltip("Sets the maximum opacity and brightness of the rainbow. This is modulated by the Climate module.")]
        [EclipseProperty(VarType.Float, "Intensity", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue intensity = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            placement = new EclipseFloatValue(parameters.skyRainbowPlacement);
            width = new EclipseFloatValue(parameters.skyRainbowWidth);
            intensity = new EclipseFloatValue(parameters.skyRainbowIntensity);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!placement.disabled)
                parameters.skyRainbowPlacement.Lerp(placement.value, weight);
            if (!width.disabled)
                parameters.skyRainbowWidth.Lerp(width.value, weight);
            if (!intensity.disabled)
                parameters.skyRainbowIntensity.Lerp(intensity.value, weight);
        }
    }

    [System.Serializable]
    public class SkyLightColumnsEclipseOverride : EclipseOverride
    {
        public override string Title => "LightColumns";
        public override string Category => "Sky";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseSkyLightColumns;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleSkyLightColumns();
        [Tooltip("Sets the color of the light columns.")]
        [EclipseProperty(VarType.Color, "Color")]
        public EclipseColorValue color = new EclipseColorValue(default);
        [Tooltip("Sets the brightness intensity of the light columns.")]
        [EclipseProperty(VarType.Float, "Intensity", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue intensity = new EclipseFloatValue(default);
        [Tooltip("Controls the vertical height scale of the light columns.")]
        [EclipseProperty(VarType.Float, "Height", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue height = new EclipseFloatValue(default);
        [Tooltip("Controls the horizontal positioning of the light columns.")]
        [EclipseProperty(VarType.Float, "Position", minFloat = -1, maxFloat = 1)]
        public EclipseFloatValue position = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            color = new EclipseColorValue(parameters.skyLightColumnsColor);
            intensity = new EclipseFloatValue(parameters.skyLightColumnsIntensity);
            height = new EclipseFloatValue(parameters.skyLightColumnsHeight);
            position = new EclipseFloatValue(parameters.skyLightColumnsPosition);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!color.disabled)
                parameters.skyLightColumnsColor.Lerp(color.value, weight);
            if (!intensity.disabled)
                parameters.skyLightColumnsIntensity.Lerp(intensity.value, weight);
            if (!height.disabled)
                parameters.skyLightColumnsHeight.Lerp(height.value, weight);
            if (!position.disabled)
                parameters.skyLightColumnsPosition.Lerp(position.value, weight);
        }
    }

    [System.Serializable]
    public class SkyShootingStarsEclipseOverride : EclipseOverride
    {
        public override string Title => "ShootingStars";
        public override string Category => "Sky";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseSkyShootingStars;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleSkyShootingStars();
        [Tooltip("Sets the main color of the shooting stars.")]
        [EclipseProperty(VarType.Color, "Color")]
        public EclipseColorValue color = new EclipseColorValue(default);
        [Tooltip("Sets the secondary trailing color of the shooting stars.")]
        [EclipseProperty(VarType.Color, "SecondaryColor")]
        public EclipseColorValue secondaryColor = new EclipseColorValue(default);
        [Tooltip("Sets the brightness intensity of the shooting stars.")]
        [EclipseProperty(VarType.Float, "Intensity", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue intensity = new EclipseFloatValue(default);
        [Tooltip("Controls how long each shooting star remains visible.")]
        [EclipseProperty(VarType.Float, "Lifetime")]
        public EclipseFloatValue lifetime = new EclipseFloatValue(default);
        [Tooltip("Sets the maximum number of shooting stars that can spawn concurrently.")]
        [EclipseProperty(VarType.Float, "ParticleCount")]
        public EclipseFloatValue particleCount = new EclipseFloatValue(default);
        [Tooltip("Sets the width of the shooting star trail.")]
        [EclipseProperty(VarType.Float, "Width")]
        public EclipseFloatValue width = new EclipseFloatValue(default);
        [Tooltip("Sets the distance traveled by each shooting star during its lifetime.")]
        [EclipseProperty(VarType.Float, "TravelDistance")]
        public EclipseFloatValue travelDistance = new EclipseFloatValue(default);
        [Tooltip("Sets the delay interval between shooting star spawns.")]
        [EclipseProperty(VarType.Float, "Delay")]
        public EclipseFloatValue delay = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            color = new EclipseColorValue(parameters.skyShootingStarsColor);
            secondaryColor = new EclipseColorValue(parameters.skyShootingStarsSecondaryColor);
            intensity = new EclipseFloatValue(parameters.skyShootingStarsIntensity);
            lifetime = new EclipseFloatValue(parameters.skyShootingStarsLifetime);
            particleCount = new EclipseFloatValue(parameters.skyShootingStarsParticleCount);
            width = new EclipseFloatValue(parameters.skyShootingStarsWidth);
            travelDistance = new EclipseFloatValue(parameters.skyShootingStarsTravelDistance);
            delay = new EclipseFloatValue(parameters.skyShootingStarsDelay);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!color.disabled)
                parameters.skyShootingStarsColor.Lerp(color.value, weight);
            if (!secondaryColor.disabled)
                parameters.skyShootingStarsSecondaryColor.Lerp(secondaryColor.value, weight);
            if (!intensity.disabled)
                parameters.skyShootingStarsIntensity.Lerp(intensity.value, weight);
            if (!lifetime.disabled)
                parameters.skyShootingStarsLifetime.Lerp(lifetime.value, weight);
            if (!particleCount.disabled)
                parameters.skyShootingStarsParticleCount.Lerp(particleCount.value, weight);
            if (!width.disabled)
                parameters.skyShootingStarsWidth.Lerp(width.value, weight);
            if (!travelDistance.disabled)
                parameters.skyShootingStarsTravelDistance.Lerp(travelDistance.value, weight);
            if (!delay.disabled)
                parameters.skyShootingStarsDelay.Lerp(delay.value, weight);
        }
    }

    [System.Serializable]
    public class SkySunDiskEclipseOverride : EclipseOverride
    {
        public override string Title => "SunDisk";
        public override string Category => "Sky";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseSkySunDisk;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleSkySunDisk();
        [Tooltip("Sets the color of the solar halo.")]
        [EclipseProperty(VarType.Color, "HaloColor")]
        public EclipseColorValue haloColor = new EclipseColorValue(default);
        [Tooltip("Controls the falloff rate for the solar halo fading outwards.")]
        [EclipseProperty(VarType.Float, "HaloFalloff")]
        public EclipseFloatValue haloFalloff = new EclipseFloatValue(default);
        [Tooltip("Sets the color of the sun disk sprite.")]
        [EclipseProperty(VarType.Color, "Color")]
        public EclipseColorValue color = new EclipseColorValue(default);
        [Tooltip("Sets the scale size of the sun disk in the sky.")]
        [EclipseProperty(VarType.Float, "Size")]
        public EclipseFloatValue size = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            haloColor = new EclipseColorValue(parameters.skySunDiskHaloColor);
            haloFalloff = new EclipseFloatValue(parameters.skySunDiskHaloFalloff);
            color = new EclipseColorValue(parameters.skySunDiskColor);
            size = new EclipseFloatValue(parameters.skySunDiskSize);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!haloColor.disabled)
                parameters.skySunDiskHaloColor.Lerp(haloColor.value, weight);
            if (!haloFalloff.disabled)
                parameters.skySunDiskHaloFalloff.Lerp(haloFalloff.value, weight);
            if (!color.disabled)
                parameters.skySunDiskColor.Lerp(color.value, weight);
            if (!size.disabled)
                parameters.skySunDiskSize.Lerp(size.value, weight);
        }
    }

    [System.Serializable]
    public class SkyMoonDiskEclipseOverride : EclipseOverride
    {
        public override string Title => "MoonDisk";
        public override string Category => "Sky";
        public override bool FeatureEnabledGlobally => CozyPreferences.CurrentStyle.UseSkyMoonDisk;
        public override void ToggleFeature() => CozyPreferences.CurrentStyle.ToggleSkyMoonDisk();
        [Tooltip("Sets the color of the moon sprite in the sky.")]
        [EclipseProperty(VarType.Color, "Color")]
        public EclipseColorValue color = new EclipseColorValue(default);
        [Tooltip("Sets the size of the moon sprite in the sky.")]
        [EclipseProperty(VarType.Float, "Size")]
        public EclipseFloatValue size = new EclipseFloatValue(default);
        [Tooltip("Changes the orientation of the moon sprite in the sky.")]
        [EclipseProperty(VarType.Float, "Rotation", minFloat = 0, maxFloat = 1)]
        public EclipseFloatValue rotation = new EclipseFloatValue(default);
        [Tooltip("Sets the strength of the normal map for the moon.")]
        [EclipseProperty(VarType.Float, "NormalStrength")]
        public EclipseFloatValue normalStrength = new EclipseFloatValue(default);
        [Tooltip("Sets the strength of the declination. Declination offsets the moon from the celestial plane (in line with the sun). Moves back and forth following a sine wave.")]
        [EclipseProperty(VarType.Float, "Declination")]
        public EclipseFloatValue declination = new EclipseFloatValue(default);
        [Tooltip("Set the number of days between full cycles of declination.")]
        [EclipseProperty(VarType.Float, "DeclinationPeriod")]
        public EclipseFloatValue declinationPeriod = new EclipseFloatValue(default);
        [Tooltip("Offsets the period for the sine wave of declination.")]
        [EclipseProperty(VarType.Float, "DeclinationOffset")]
        public EclipseFloatValue declinationOffset = new EclipseFloatValue(default);
        [Tooltip("Set the number of days between full cycles the orbit.")]
        [EclipseProperty(VarType.Float, "OrbitPeriod")]
        public EclipseFloatValue orbitPeriod = new EclipseFloatValue(default);
        [Tooltip("Offsets the period for the orbit.")]
        [EclipseProperty(VarType.Float, "OrbitOffset")]
        public EclipseFloatValue orbitOffset = new EclipseFloatValue(default);

        public override bool CaptureFromParameters(AtmosphereParameters parameters) {
            color = new EclipseColorValue(parameters.skyMoonDiskColor);
            size = new EclipseFloatValue(parameters.skyMoonDiskSize);
            rotation = new EclipseFloatValue(parameters.skyMoonDiskRotation);
            normalStrength = new EclipseFloatValue(parameters.skyMoonDiskNormalStrength);
            declination = new EclipseFloatValue(parameters.skyMoonDiskDeclination);
            declinationPeriod = new EclipseFloatValue(parameters.skyMoonDiskDeclinationPeriod);
            declinationOffset = new EclipseFloatValue(parameters.skyMoonDiskDeclinationOffset);
            orbitPeriod = new EclipseFloatValue(parameters.skyMoonDiskOrbitPeriod);
            orbitOffset = new EclipseFloatValue(parameters.skyMoonDiskOrbitOffset);
            return true;
        }

        public override void ApplyToAtmosphere(AtmosphereParameters parameters, float weight) {
            if (!active || weight <= 0f) return;
            if (!color.disabled)
                parameters.skyMoonDiskColor.Lerp(color.value, weight);
            if (!size.disabled)
                parameters.skyMoonDiskSize.Lerp(size.value, weight);
            if (!rotation.disabled)
                parameters.skyMoonDiskRotation.Lerp(rotation.value, weight);
            if (!normalStrength.disabled)
                parameters.skyMoonDiskNormalStrength.Lerp(normalStrength.value, weight);
            if (!declination.disabled)
                parameters.skyMoonDiskDeclination.Lerp(declination.value, weight);
            if (!declinationPeriod.disabled)
                parameters.skyMoonDiskDeclinationPeriod.Lerp(declinationPeriod.value, weight);
            if (!declinationOffset.disabled)
                parameters.skyMoonDiskDeclinationOffset.Lerp(declinationOffset.value, weight);
            if (!orbitPeriod.disabled)
                parameters.skyMoonDiskOrbitPeriod.Lerp(orbitPeriod.value, weight);
            if (!orbitOffset.disabled)
                parameters.skyMoonDiskOrbitOffset.Lerp(orbitOffset.value, weight);
        }
    }


}
