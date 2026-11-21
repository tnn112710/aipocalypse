//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using System;
using UnityEngine.Rendering;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

namespace DistantLands.Cozy
{
    [ExecuteAlways]
    [AddComponentMenu("")]
    [CozyModule("Atmosphere", "Atmosphere", "Manage sky, fog, clouds, and lighting settings.", "Atmosphere", true, 0)]
    public class CozyAtmosphereModule : CozyBiomeModule<CozyAtmosphereModule>, IAtmosphereModule
    {
        public AtmosphereProfile AtmosphereProfile
        {
            get
            {
                return atmosphereProfile;
            }
            set
            {
                atmosphereProfile = value;
                UpdateModel();
            }
        }

        [CozySearchable(true, "sky", "atmosphere", "fog", "lighting", "clouds")]
        [SerializeField]
        private AtmosphereProfile atmosphereProfile;

        public AtmosphereParameters Parameters => parameters;
        private AtmosphereParameters parameters = new();
        private AtmosphereParameters lastFrameParameters = new();
        private bool shaderVariablesNeedReset = true;

        public Vector3 SunDirection => sunDirection;
        public Vector3 MoonDirection => moonDirection;
        public Light SunLight => sunLight;
        private UniversalAdditionalLightData sunLightData;
        public Light MoonLight => moonLight;
        public LensFlareComponentSRP sunLensFlare;
        public LensFlareComponentSRP moonLensFlare;
        public Light StrongestLight => GetLightStrength(SunLight) >= GetLightStrength(MoonLight) ? SunLight : MoonLight;
        public bool OverrideSceneLights
        {
            get => overrideSceneLights;
            set
            {
                overrideSceneLights = value;
                if (value) DisableSceneLights();
            }
        }
        private bool overrideSceneLights;
        private Vector3 moonDirection;
        private Vector3 sunDirection;
        private Light sunLight;
        private Light moonLight;
        private Transform sunLightTransform;
        private Transform moonLightTransform;
        private Transform sunFlareTransform;
        private EntityId sunFlareEntityID;
        private Transform moonFlareTransform;
        private EntityId moonFlareEntityID;
        private Quaternion cachedSkyBaseRotation = Quaternion.identity;
        private float cachedSkyPitch = float.NaN;
        private float cachedSkyRotation = float.NaN;
        private bool sunCookieApplied;
        private float lastCookieSize = -1;

        public float LightningLightingOverride { get => lightningLightingOverride; set => lightningLightingOverride = value; }
        private float lightningLightingOverride = 0;

        private Vector3 cloudWindOffset;
        private float cloudNoiseTime;
        private Vector3 cloudWorldOffset;
        private bool cloudShadowRefreshRequested;

        /// <summary>
        /// Compensates cloud sampling for the displacement applied to world objects and the camera.
        /// Call once after moving them; only X and Z are supported.
        /// </summary>
        public void ShiftWorld(Vector3 deltaXZ)
        {
            cloudWorldOffset.x -= deltaXZ.x;
            cloudWorldOffset.z -= deltaXZ.z;
            Shader.SetGlobalVector(CozyShaderIDs.baseWeatherCloudWorldOffset, cloudWorldOffset);
            cloudShadowRefreshRequested = true;
            if (sunCookieApplied && shadowShader && sunLight && sunLightData && WeatherSphere.CozyCamera)
                RenderCloudShadowCookie(WeatherSphere.CozyCamera.transform.position, sunLightTransform.rotation);
        }

        public event Action<CozyAtmosphereModule> RunBeforeBiomes;
        private void ExecuteRunBeforeBiomes() { RunBeforeBiomes?.Invoke(this); }
        public event Action<CozyAtmosphereModule> RunAfterBiomes;
        private void ExecuteRunAfterBiomes() { RunAfterBiomes?.Invoke(this); }
        internal event Action<bool, bool> SceneLightingUpdated;

        [SerializeReference]
        [AtmosphereOverridesList]
        public List<AtmosphereOverride> overrides = new List<AtmosphereOverride>();

        [SerializeField]
        private AtmosphereModel model;
        public AtmosphereModel ResolvedModel => model;
        public ComputeShader shadowShader;

        private RenderTexture cookieTexture;
        private RenderTexture rawTexture;
        private int mainKernel;
        private int blurKernel;
        private int lastResolution;

        private static readonly int LightDirectionID = Shader.PropertyToID("_LightDirection");
        private static readonly int CameraPositionID = Shader.PropertyToID("_CameraPosition");
        private static readonly int CookieSizeID = Shader.PropertyToID("_CookieSize");
        private static readonly int CloudTimeID = Shader.PropertyToID("_CloudTime");
        private static readonly int ShadowStrengthID = Shader.PropertyToID("_ShadowStrength");
        private static readonly int ShadowStepsID = Shader.PropertyToID("_ShadowSteps");
        private static readonly int BlurRadiusID = Shader.PropertyToID("_BlurRadius");
        private static readonly int LightPositionID = Shader.PropertyToID("_LightPosition");
        private static readonly int LightRightID = Shader.PropertyToID("_LightRight");
        private static readonly int LightUpID = Shader.PropertyToID("_LightUp");
        private static readonly int RawTexID = Shader.PropertyToID("_RawTex");
        private static readonly int RawTexReadID = Shader.PropertyToID("_RawTexRead");
        private static readonly int ResultID = Shader.PropertyToID("Result");

        private int framesBetweenCloudShadowRendering = 3;
        private int framesBetweenLensFlareOcclusionRendering = 3;
        private float sunFlareOcclusion = 1;
        private float moonFlareOcclusion = 1;


        public override void InitializeModule()
        {
            base.InitializeModule();
            UpdateModel();

            if (IsBiomeModule) return;
            parameters = new AtmosphereParameters();
            lastFrameParameters = new AtmosphereParameters();
            // cloudWindOffset = Vector3.zero;
            Shader.SetGlobalVector(CozyShaderIDs.baseWeatherCloudWorldOffset, cloudWorldOffset);
            SetupReferences();

            FlareOcclusion.TargetCamera = WeatherSphere.CozyCamera;
        }

        private void OnValidate()
        {
            UpdateModel();
        }

        void SetupReferences()
        {
            sunLight = WeatherSphere.GetChild<Light>("Sun Light");
            sunLightData = sunLight.GetComponent<UniversalAdditionalLightData>();
            moonLight = WeatherSphere.GetChild<Light>("Moon Light");

            sunLensFlare = WeatherSphere.GetChild<LensFlareComponentSRP>("Sun Flare");
            moonLensFlare = WeatherSphere.GetChild<LensFlareComponentSRP>("Moon Flare");

            sunLightTransform = sunLight.transform;
            moonLightTransform = moonLight.transform;
            sunFlareTransform = sunLensFlare.transform;
            moonFlareTransform = moonLensFlare.transform;

            sunFlareEntityID = sunLensFlare.GetEntityId();
            moonFlareEntityID = moonLensFlare.GetEntityId();

            sunCookieApplied = false;

            if (overrideSceneLights) DisableSceneLights();
        }

        private void DisableSceneLights()
        {
            if (sunLight) sunLight.enabled = false;
            if (moonLight) moonLight.enabled = false;
        }

        public override void ApplyBiome(CozyAtmosphereModule biome, float weight)
        {
            float t = WeatherSphere.Time != null ? WeatherSphere.Time.ModifiedTime : 0.5f;

            if (biome.model == null)
            {
                biome.model = new();

                biome.model.Reset();
                biome.model.UpdateModel(biome);
            }

            biome.model.ApplyToParameters(parameters, t, weight);
        }

        public void UpdateModel()
        {
            if (model == null)
            {
                model = new();
            }

            model.Reset();
            model.UpdateModel(this);
            shaderVariablesNeedReset = true;
        }

        public override void PropagateVariables()
        {
            if (model == null)
                UpdateModel();

            parameters.Reset();

            float t = WeatherSphere.Time != null ? WeatherSphere.Time.ModifiedTime : 0.5f;

            model.ApplyToParameters(parameters, t);

            ExecuteRunBeforeBiomes();
            ApplyBiomes();
            ExecuteRunAfterBiomes();

            if (shaderVariablesNeedReset)
            {
                ResetShaderVariables();
                shaderVariablesNeedReset = false;
            }

            //After combining all parameters into a single object, pass them to the shader model
            PassShaderVariables();
        }

        private void ResetShaderVariables()
        {
            CozyStyle currentStyle = CozyPreferences.CurrentStyle;

            // START GENERATED VARIABLE RESET
            #region Lighting
            if (CozyPreferences.CurrentStyle.UseLighting)
            {
                // SunCloudShadows
                if (CozyPreferences.CurrentStyle.UseLightingSunCloudShadows)
                {
                    Shader.SetGlobalFloat(CozyShaderIDs.lightingSunCloudShadowsResolution, parameters.lightingSunCloudShadowsResolution.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.lightingSunCloudShadowsSize, parameters.lightingSunCloudShadowsSize.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.lightingSunCloudShadowsSteps, parameters.lightingSunCloudShadowsSteps.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.lightingSunCloudShadowsBlurRadius, parameters.lightingSunCloudShadowsBlurRadius.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.lightingSunCloudShadowsIntensity, parameters.lightingSunCloudShadowsIntensity.value);
                }

            }
            #endregion

            #region Sky
            if (CozyPreferences.CurrentStyle.UseSky)
            {
                // Gradient
                if (CozyPreferences.CurrentStyle.UseSkyGradient)
                {
                    Shader.SetGlobalColor(CozyShaderIDs.skyGradientZenithColor, parameters.skyGradientZenithColor.value);
                    Shader.SetGlobalColor(CozyShaderIDs.skyGradientHorizonColor, parameters.skyGradientHorizonColor.value);
                    Shader.SetGlobalColor(CozyShaderIDs.skyGradientGroundColor, parameters.skyGradientGroundColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyGradientZenithExponent, parameters.skyGradientZenithExponent.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyGradientGroundExponent, parameters.skyGradientGroundExponent.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyGradientOffset, parameters.skyGradientOffset.value);
                }

                // TwoTone
                if (CozyPreferences.CurrentStyle.UseSkyTwoTone)
                {
                    Shader.SetGlobalColor(CozyShaderIDs.skyTwoToneSunSideColor, parameters.skyTwoToneSunSideColor.value);
                    Shader.SetGlobalColor(CozyShaderIDs.skyTwoToneAntiSunSideColor, parameters.skyTwoToneAntiSunSideColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyTwoToneOffset, parameters.skyTwoToneOffset.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyTwoToneExponent, parameters.skyTwoToneExponent.value);
                }

                // Stars
                if (CozyPreferences.CurrentStyle.UseSkyStars)
                {
                    Shader.SetGlobalColor(CozyShaderIDs.skyStarsColor, parameters.skyStarsColor.value);
                    Shader.SetGlobalTexture(CozyShaderIDs.skyStarsCubemap, parameters.skyStarsCubemap == null ? CozyUtilities.BlackCubemap : parameters.skyStarsCubemap.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyStarsIntensity, parameters.skyStarsIntensity.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyStarsRotationOffset, parameters.skyStarsRotationOffset.value);

                    Shader.SetGlobalFloat(CozyShaderIDs.skyStarsTwinkleAmount, parameters.skyStarsTwinkleAmount.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyStarsTwinkleSpeed, parameters.skyStarsTwinkleSpeed.value);
                    Shader.SetGlobalTexture(CozyShaderIDs.skyStarsTwinkleNoise, parameters.skyStarsTwinkleNoise == null ? CozyUtilities.BlackCubemap : parameters.skyStarsTwinkleNoise.value);
                }

                // Constellations
                if (CozyPreferences.CurrentStyle.UseSkyConstellations)
                {
                    Shader.SetGlobalColor(CozyShaderIDs.skyConstellationsColor, parameters.skyConstellationsColor.value);
                    Shader.SetGlobalTexture(CozyShaderIDs.skyConstellationsCubemap, parameters.skyConstellationsCubemap == null ? CozyUtilities.BlackCubemap : parameters.skyConstellationsCubemap.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyConstellationsIntensity, parameters.skyConstellationsIntensity.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyConstellationsShimmerAmount, parameters.skyConstellationsShimmerAmount.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyConstellationsShimmerSpeed, parameters.skyConstellationsShimmerSpeed.value);
                }

                // Galaxy
                if (CozyPreferences.CurrentStyle.UseSkyGalaxy)
                {
                    Shader.SetGlobalColor(CozyShaderIDs.skyGalaxyPrimaryColor, parameters.skyGalaxyPrimaryColor.value);
                    Shader.SetGlobalColor(CozyShaderIDs.skyGalaxySecondaryColor, parameters.skyGalaxySecondaryColor.value);
                    Shader.SetGlobalColor(CozyShaderIDs.skyGalaxyTertiaryColor, parameters.skyGalaxyTertiaryColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyGalaxyIntensity, parameters.skyGalaxyIntensity.value);
                    Shader.SetGlobalTexture(CozyShaderIDs.skyGalaxyCubemap, parameters.skyGalaxyCubemap == null ? CozyUtilities.BlackCubemap : parameters.skyGalaxyCubemap.value);
                }

                // Rainbow
                if (CozyPreferences.CurrentStyle.UseSkyRainbow)
                {
                    Shader.SetGlobalFloat(CozyShaderIDs.skyRainbowPlacement, parameters.skyRainbowPlacement.value);
                    Shader.SetGlobalTexture(CozyShaderIDs.skyRainbowTexture, parameters.skyRainbowTexture == null ? Texture2D.blackTexture : parameters.skyRainbowTexture.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyRainbowWidth, parameters.skyRainbowWidth.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyRainbowIntensity, parameters.skyRainbowIntensity.value);
                }

                // LightColumns
                if (CozyPreferences.CurrentStyle.UseSkyLightColumns)
                {
                    Shader.SetGlobalColor(CozyShaderIDs.skyLightColumnsColor, parameters.skyLightColumnsColor.value);
                    Shader.SetGlobalTexture(CozyShaderIDs.skyLightColumnsTexture, parameters.skyLightColumnsTexture == null ? Texture2D.blackTexture : parameters.skyLightColumnsTexture.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyLightColumnsIntensity, parameters.skyLightColumnsIntensity.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyLightColumnsHeight, parameters.skyLightColumnsHeight.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyLightColumnsPosition, parameters.skyLightColumnsPosition.value);
                }

                // ShootingStars
                if (CozyPreferences.CurrentStyle.UseSkyShootingStars)
                {
                    Shader.SetGlobalColor(CozyShaderIDs.skyShootingStarsColor, parameters.skyShootingStarsColor.value);
                    Shader.SetGlobalColor(CozyShaderIDs.skyShootingStarsSecondaryColor, parameters.skyShootingStarsSecondaryColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyShootingStarsIntensity, parameters.skyShootingStarsIntensity.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyShootingStarsLifetime, parameters.skyShootingStarsLifetime.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyShootingStarsParticleCount, parameters.skyShootingStarsParticleCount.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyShootingStarsWidth, parameters.skyShootingStarsWidth.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyShootingStarsTravelDistance, parameters.skyShootingStarsTravelDistance.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyShootingStarsDelay, parameters.skyShootingStarsDelay.value);
                }

                // SunDisk
                if (CozyPreferences.CurrentStyle.UseSkySunDisk)
                {
                    Shader.SetGlobalColor(CozyShaderIDs.skySunDiskHaloColor, parameters.skySunDiskHaloColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skySunDiskHaloFalloff, parameters.skySunDiskHaloFalloff.value);
                    Shader.SetGlobalColor(CozyShaderIDs.skySunDiskColor, parameters.skySunDiskColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skySunDiskSize, parameters.skySunDiskSize.value);
                    Shader.SetGlobalTexture(CozyShaderIDs.skySunDiskTexture, parameters.skySunDiskTexture == null ? Texture2D.blackTexture : parameters.skySunDiskTexture.value);
                }

                // MoonDisk
                if (CozyPreferences.CurrentStyle.UseSkyMoonDisk)
                {



                    Shader.SetGlobalColor(CozyShaderIDs.skyMoonDiskColor, parameters.skyMoonDiskColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskSize, parameters.skyMoonDiskSize.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskRotation, parameters.skyMoonDiskRotation.value);
                    Shader.SetGlobalTexture(CozyShaderIDs.skyMoonDiskTexture, parameters.skyMoonDiskTexture == null ? Texture2D.blackTexture : parameters.skyMoonDiskTexture.value);
                    Shader.SetGlobalTexture(CozyShaderIDs.skyMoonDiskNormalMap, parameters.skyMoonDiskNormalMap == null ? Texture2D.blackTexture : parameters.skyMoonDiskNormalMap.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskNormalStrength, parameters.skyMoonDiskNormalStrength.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskDeclination, parameters.skyMoonDiskDeclination.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskDeclinationPeriod, parameters.skyMoonDiskDeclinationPeriod.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskDeclinationOffset, parameters.skyMoonDiskDeclinationOffset.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskOrbitPeriod, parameters.skyMoonDiskOrbitPeriod.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskOrbitOffset, parameters.skyMoonDiskOrbitOffset.value);
                }

            }
            #endregion

            #region Fog
            if (CozyPreferences.CurrentStyle.UseFog)
            {
                // Gradient
                if (CozyPreferences.CurrentStyle.UseFogGradient)
                {
                    Shader.SetGlobalColor(CozyShaderIDs.fogGradientColor1, parameters.fogGradientColor1.value);
                    Shader.SetGlobalColor(CozyShaderIDs.fogGradientColor2, parameters.fogGradientColor2.value);
                    Shader.SetGlobalColor(CozyShaderIDs.fogGradientColor3, parameters.fogGradientColor3.value);
                    Shader.SetGlobalColor(CozyShaderIDs.fogGradientColor4, parameters.fogGradientColor4.value);
                    Shader.SetGlobalColor(CozyShaderIDs.fogGradientColor5, parameters.fogGradientColor5.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogGradientDensity1, parameters.fogGradientDensity1.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogGradientDensity2, parameters.fogGradientDensity2.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogGradientDensity3, parameters.fogGradientDensity3.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogGradientDensity4, parameters.fogGradientDensity4.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogGradientDensity5, parameters.fogGradientDensity5.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogGradientStart1, parameters.fogGradientStart1.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogGradientStart2, parameters.fogGradientStart2.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogGradientStart3, parameters.fogGradientStart3.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogGradientStart4, parameters.fogGradientStart4.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogGradientStart5, parameters.fogGradientStart5.value);
                }

                // SunLightFlare
                if (CozyPreferences.CurrentStyle.UseFogSunLightFlare)
                {
                    Shader.SetGlobalColor(CozyShaderIDs.fogSunLightFlareColor, parameters.fogSunLightFlareColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogSunLightFlareSquish, parameters.fogSunLightFlareSquish.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogSunLightFlareIntensity, parameters.fogSunLightFlareIntensity.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogSunLightFlareFalloff, parameters.fogSunLightFlareFalloff.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogSunLightFlareMinDistance, parameters.fogSunLightFlareMinDistance.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogSunLightFlareMaxDistance, parameters.fogSunLightFlareMaxDistance.value);
                }

                // MoonLightFlare
                if (CozyPreferences.CurrentStyle.UseFogMoonLightFlare)
                {
                    Shader.SetGlobalColor(CozyShaderIDs.fogMoonLightFlareColor, parameters.fogMoonLightFlareColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogMoonLightFlareSquish, parameters.fogMoonLightFlareSquish.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogMoonLightFlareIntensity, parameters.fogMoonLightFlareIntensity.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogMoonLightFlareFalloff, parameters.fogMoonLightFlareFalloff.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogMoonLightFlareMinDistance, parameters.fogMoonLightFlareMinDistance.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogMoonLightFlareMaxDistance, parameters.fogMoonLightFlareMaxDistance.value);
                }

                // TwoTone
                if (CozyPreferences.CurrentStyle.UseFogTwoTone)
                {
                    Shader.SetGlobalColor(CozyShaderIDs.fogTwoToneSunSideColor, parameters.fogTwoToneSunSideColor.value);
                    Shader.SetGlobalColor(CozyShaderIDs.fogTwoToneAntiSunSideColor, parameters.fogTwoToneAntiSunSideColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogTwoToneSunSideDensity, parameters.fogTwoToneSunSideDensity.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogTwoToneAntiSunSideDensity, parameters.fogTwoToneAntiSunSideDensity.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogTwoToneSmoothness, parameters.fogTwoToneSmoothness.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogTwoToneOffset, parameters.fogTwoToneOffset.value);
                }

                // Height
                if (CozyPreferences.CurrentStyle.UseFogHeight)
                {
                    Shader.SetGlobalColor(CozyShaderIDs.fogHeightColor, parameters.fogHeightColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogHeightFalloff, parameters.fogHeightFalloff.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogHeightSmoothness, parameters.fogHeightSmoothness.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogHeightBaseHeight, parameters.fogHeightBaseHeight.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogHeightDensity, parameters.fogHeightDensity.value);
                    Shader.SetGlobalTexture(CozyShaderIDs.fogHeightVariationNoise, parameters.fogHeightVariationNoise == null ? Texture2D.blackTexture : parameters.fogHeightVariationNoise.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogHeightVariationAmount, parameters.fogHeightVariationAmount.value);
                    Shader.SetGlobalVector(CozyShaderIDs.fogHeightVariationTextureScale, parameters.fogHeightVariationTextureScale.value);
                    Shader.SetGlobalVector(CozyShaderIDs.fogHeightVariationTextureFlow, parameters.fogHeightVariationTextureFlow.value);
                }

                // Base
                if (CozyPreferences.CurrentStyle.UseFogBase)
                {
                    Shader.SetGlobalFloat(CozyShaderIDs.fogBaseDensityMultiplier, parameters.fogBaseDensityMultiplier.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogBaseHeight, parameters.fogBaseHeight.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogBaseSmoothness, parameters.fogBaseSmoothness.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogBaseBaseHeight, parameters.fogBaseBaseHeight.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogBaseBaseSmoothness, parameters.fogBaseBaseSmoothness.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.fogBaseTransitionDistance, parameters.fogBaseTransitionDistance.value);
                }

            }
            #endregion

            #region Clouds
            if (CozyPreferences.CurrentStyle.UseClouds)
            {
                // Volumetric
                if (CozyPreferences.CurrentStyle.UseCloudsVolumetric)
                {
                    Shader.SetGlobalTexture(CozyShaderIDs.cloudsVolumetricMainTexture, parameters.cloudsVolumetricMainTexture == null ? Texture2D.blackTexture : parameters.cloudsVolumetricMainTexture.value);
                    Shader.SetGlobalTexture(CozyShaderIDs.cloudsVolumetricNoiseTexture, parameters.cloudsVolumetricNoiseTexture == null ? Texture2D.blackTexture : parameters.cloudsVolumetricNoiseTexture.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricHeight, parameters.cloudsVolumetricHeight.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricDarkeningIntensity, parameters.cloudsVolumetricDarkeningIntensity.value);
                    Shader.SetGlobalColor(CozyShaderIDs.cloudsVolumetricLitColor, parameters.cloudsVolumetricLitColor.value);
                    Shader.SetGlobalColor(CozyShaderIDs.cloudsVolumetricShadeColor, parameters.cloudsVolumetricShadeColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricPlacementScale1, parameters.cloudsVolumetricPlacementScale1.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricPlacementScale2, parameters.cloudsVolumetricPlacementScale2.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricDensity, parameters.cloudsVolumetricDensity.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricNoiseScale, parameters.cloudsVolumetricNoiseScale.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricNoiseAmount, parameters.cloudsVolumetricNoiseAmount.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricPlacementHeight, parameters.cloudsVolumetricPlacementHeight.value);
                    Shader.SetGlobalTexture(CozyShaderIDs.cloudsVolumetricRamp, parameters.cloudsVolumetricRamp == null ? Texture2D.blackTexture : parameters.cloudsVolumetricRamp.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricStepSize, parameters.cloudsVolumetricStepSize.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricSteps, parameters.cloudsVolumetricSteps.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricLODCoefficient, parameters.cloudsVolumetricLODCoefficient.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricLODSteps, parameters.cloudsVolumetricLODSteps.value);
                    Shader.SetGlobalTexture(CozyShaderIDs.cloudsVolumetricBlueNoise, parameters.cloudsVolumetricBlueNoise == null ? Texture2D.blackTexture : parameters.cloudsVolumetricBlueNoise.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricWindStrength, parameters.cloudsVolumetricWindStrength.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricHighlightBrightness, parameters.cloudsVolumetricHighlightBrightness.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricHighlightStepSize, parameters.cloudsVolumetricHighlightStepSize.value);
                }

                // Cumulonimbus
                if (CozyPreferences.CurrentStyle.UseCloudsCumulonimbus)
                {
                    Shader.SetGlobalTexture(CozyShaderIDs.cloudsCumulonimbusMainTexture, parameters.cloudsCumulonimbusMainTexture == null ? Texture2D.blackTexture : parameters.cloudsCumulonimbusMainTexture.value);
                    Shader.SetGlobalTexture(CozyShaderIDs.cloudsCumulonimbusRamp, parameters.cloudsCumulonimbusRamp == null ? Texture2D.blackTexture : parameters.cloudsCumulonimbusRamp.value);
                }

                // Cumulus
                if (CozyPreferences.CurrentStyle.UseCloudsCumulus)
                {
                    Shader.SetGlobalTexture(CozyShaderIDs.cloudsCumulusMainTexture, parameters.cloudsCumulusMainTexture == null ? Texture2D.blackTexture : parameters.cloudsCumulusMainTexture.value);
                    Shader.SetGlobalTexture(CozyShaderIDs.cloudsCumulusErosionTexture, parameters.cloudsCumulusErosionTexture == null ? Texture2D.blackTexture : parameters.cloudsCumulusErosionTexture.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsCumulusHeight, parameters.cloudsCumulusHeight.value);
                    Shader.SetGlobalColor(CozyShaderIDs.cloudsCumulusLitColor, parameters.cloudsCumulusLitColor.value);
                    Shader.SetGlobalColor(CozyShaderIDs.cloudsCumulusShadeColor, parameters.cloudsCumulusShadeColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsCumulusThickness, parameters.cloudsCumulusThickness.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsCumulusDensity, parameters.cloudsCumulusDensity.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsCumulusWindStrength, parameters.cloudsCumulusWindStrength.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsCumulusCohesion, parameters.cloudsCumulusCohesion.value);
                }

                // Altocumulus
                if (CozyPreferences.CurrentStyle.UseCloudsAltocumulus)
                {
                    Shader.SetGlobalTexture(CozyShaderIDs.cloudsAltocumulusMainTexture, parameters.cloudsAltocumulusMainTexture == null ? Texture2D.blackTexture : parameters.cloudsAltocumulusMainTexture.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsAltocumulusHeight, parameters.cloudsAltocumulusHeight.value);
                    Shader.SetGlobalVector(CozyShaderIDs.cloudsAltocumulusTextureScale, parameters.cloudsAltocumulusTextureScale.value);
                    Shader.SetGlobalColor(CozyShaderIDs.cloudsAltocumulusColor, parameters.cloudsAltocumulusColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsAltocumulusDensity, parameters.cloudsAltocumulusDensity.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsAltocumulusRamp, parameters.cloudsAltocumulusRamp.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsAltocumulusWindStrength, parameters.cloudsAltocumulusWindStrength.value);
                }

                // Cirrus
                if (CozyPreferences.CurrentStyle.UseCloudsCirrus)
                {
                    Shader.SetGlobalTexture(CozyShaderIDs.cloudsCirrusMainTexture, parameters.cloudsCirrusMainTexture == null ? Texture2D.blackTexture : parameters.cloudsCirrusMainTexture.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsCirrusHeight, parameters.cloudsCirrusHeight.value);
                    Shader.SetGlobalVector(CozyShaderIDs.cloudsCirrusTextureScale, parameters.cloudsCirrusTextureScale.value);
                    Shader.SetGlobalColor(CozyShaderIDs.cloudsCirrusColor, parameters.cloudsCirrusColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsCirrusDensity, parameters.cloudsCirrusDensity.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsCirrusWindStrength, parameters.cloudsCirrusWindStrength.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsCirrusWispFactor, parameters.cloudsCirrusWispFactor.value);
                }

                // Contrails
                if (CozyPreferences.CurrentStyle.UseCloudsContrails)
                {
                    Shader.SetGlobalTexture(CozyShaderIDs.cloudsContrailsMainTexture, parameters.cloudsContrailsMainTexture == null ? Texture2D.blackTexture : parameters.cloudsContrailsMainTexture.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsContrailsHeight, parameters.cloudsContrailsHeight.value);
                    Shader.SetGlobalVector(CozyShaderIDs.cloudsContrailsTextureScale, parameters.cloudsContrailsTextureScale.value);
                    Shader.SetGlobalColor(CozyShaderIDs.cloudsContrailsColor, parameters.cloudsContrailsColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsContrailsDensity, parameters.cloudsContrailsDensity.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.cloudsContrailsWindStrength, parameters.cloudsContrailsWindStrength.value);
                }

            }
            #endregion

            #region Sunshafts
            if (CozyPreferences.CurrentStyle.UseSunshafts)
            {
                // Screenspace
                if (CozyPreferences.CurrentStyle.UseSunshaftsScreenspace)
                {
                    Shader.SetGlobalColor(CozyShaderIDs.sunshaftsScreenspaceColor, parameters.sunshaftsScreenspaceColor.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.sunshaftsScreenspaceIntensity, parameters.sunshaftsScreenspaceIntensity.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.sunshaftsScreenspaceLength, parameters.sunshaftsScreenspaceLength.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.sunshaftsScreenspaceSteps, parameters.sunshaftsScreenspaceSteps.value);
                    Shader.SetGlobalFloat(CozyShaderIDs.sunshaftsScreenspaceCloudMaskAmount, parameters.sunshaftsScreenspaceCloudMaskAmount.value);
                }

            }
            #endregion

            // END GENERATED VARIABLE RESET
        }

        private void PassShaderVariables()
        {
            CozyStyle currentStyle = CozyPreferences.CurrentStyle;

            // START GENERATED VARIABLE UPDATE
            #region Lighting
            if (currentStyle.UseLighting)
            {
                // SunCloudShadows
                if (currentStyle.UseLightingSunCloudShadows)
                {
                    if (parameters.lightingSunCloudShadowsResolution.value != lastFrameParameters.lightingSunCloudShadowsResolution.value) Shader.SetGlobalFloat(CozyShaderIDs.lightingSunCloudShadowsResolution, parameters.lightingSunCloudShadowsResolution.value);
                    if (parameters.lightingSunCloudShadowsSize.value != lastFrameParameters.lightingSunCloudShadowsSize.value) Shader.SetGlobalFloat(CozyShaderIDs.lightingSunCloudShadowsSize, parameters.lightingSunCloudShadowsSize.value);
                    if (parameters.lightingSunCloudShadowsSteps.value != lastFrameParameters.lightingSunCloudShadowsSteps.value) Shader.SetGlobalFloat(CozyShaderIDs.lightingSunCloudShadowsSteps, parameters.lightingSunCloudShadowsSteps.value);
                    if (parameters.lightingSunCloudShadowsBlurRadius.value != lastFrameParameters.lightingSunCloudShadowsBlurRadius.value) Shader.SetGlobalFloat(CozyShaderIDs.lightingSunCloudShadowsBlurRadius, parameters.lightingSunCloudShadowsBlurRadius.value);
                    if (parameters.lightingSunCloudShadowsIntensity.value != lastFrameParameters.lightingSunCloudShadowsIntensity.value) Shader.SetGlobalFloat(CozyShaderIDs.lightingSunCloudShadowsIntensity, parameters.lightingSunCloudShadowsIntensity.value);
                }

            }
            #endregion

            #region Sky
            if (currentStyle.UseSky)
            {
                // Gradient
                if (currentStyle.UseSkyGradient)
                {
                    if (parameters.skyGradientZenithColor.value != lastFrameParameters.skyGradientZenithColor.value) Shader.SetGlobalColor(CozyShaderIDs.skyGradientZenithColor, parameters.skyGradientZenithColor.value);
                    if (parameters.skyGradientHorizonColor.value != lastFrameParameters.skyGradientHorizonColor.value) Shader.SetGlobalColor(CozyShaderIDs.skyGradientHorizonColor, parameters.skyGradientHorizonColor.value);
                    if (parameters.skyGradientGroundColor.value != lastFrameParameters.skyGradientGroundColor.value) Shader.SetGlobalColor(CozyShaderIDs.skyGradientGroundColor, parameters.skyGradientGroundColor.value);
                    if (parameters.skyGradientZenithExponent.value != lastFrameParameters.skyGradientZenithExponent.value) Shader.SetGlobalFloat(CozyShaderIDs.skyGradientZenithExponent, parameters.skyGradientZenithExponent.value);
                    if (parameters.skyGradientGroundExponent.value != lastFrameParameters.skyGradientGroundExponent.value) Shader.SetGlobalFloat(CozyShaderIDs.skyGradientGroundExponent, parameters.skyGradientGroundExponent.value);
                    if (parameters.skyGradientOffset.value != lastFrameParameters.skyGradientOffset.value) Shader.SetGlobalFloat(CozyShaderIDs.skyGradientOffset, parameters.skyGradientOffset.value);
                }

                // TwoTone
                if (currentStyle.UseSkyTwoTone)
                {
                    if (parameters.skyTwoToneSunSideColor.value != lastFrameParameters.skyTwoToneSunSideColor.value) Shader.SetGlobalColor(CozyShaderIDs.skyTwoToneSunSideColor, parameters.skyTwoToneSunSideColor.value);
                    if (parameters.skyTwoToneAntiSunSideColor.value != lastFrameParameters.skyTwoToneAntiSunSideColor.value) Shader.SetGlobalColor(CozyShaderIDs.skyTwoToneAntiSunSideColor, parameters.skyTwoToneAntiSunSideColor.value);
                    if (parameters.skyTwoToneOffset.value != lastFrameParameters.skyTwoToneOffset.value) Shader.SetGlobalFloat(CozyShaderIDs.skyTwoToneOffset, parameters.skyTwoToneOffset.value);
                    if (parameters.skyTwoToneExponent.value != lastFrameParameters.skyTwoToneExponent.value) Shader.SetGlobalFloat(CozyShaderIDs.skyTwoToneExponent, parameters.skyTwoToneExponent.value);
                }

                // Stars
                if (currentStyle.UseSkyStars)
                {
                    if (parameters.skyStarsColor.value != lastFrameParameters.skyStarsColor.value) Shader.SetGlobalColor(CozyShaderIDs.skyStarsColor, parameters.skyStarsColor.value);
                    if (parameters.skyStarsCubemap.value != lastFrameParameters.skyStarsCubemap.value) Shader.SetGlobalTexture(CozyShaderIDs.skyStarsCubemap, parameters.skyStarsCubemap == null ? CozyUtilities.BlackCubemap : parameters.skyStarsCubemap.value);
                    if (parameters.skyStarsIntensity.value != lastFrameParameters.skyStarsIntensity.value) Shader.SetGlobalFloat(CozyShaderIDs.skyStarsIntensity, parameters.skyStarsIntensity.value);
                    if (parameters.skyStarsRotationOffset.value != lastFrameParameters.skyStarsRotationOffset.value) Shader.SetGlobalFloat(CozyShaderIDs.skyStarsRotationOffset, parameters.skyStarsRotationOffset.value);
                    if (parameters.skyStarsTwinkleAmount.value != lastFrameParameters.skyStarsTwinkleAmount.value) Shader.SetGlobalFloat(CozyShaderIDs.skyStarsTwinkleAmount, parameters.skyStarsTwinkleAmount.value);
                    if (parameters.skyStarsTwinkleSpeed.value != lastFrameParameters.skyStarsTwinkleSpeed.value) Shader.SetGlobalFloat(CozyShaderIDs.skyStarsTwinkleSpeed, parameters.skyStarsTwinkleSpeed.value);
                    if (parameters.skyStarsTwinkleNoise.value != lastFrameParameters.skyStarsTwinkleNoise.value) Shader.SetGlobalTexture(CozyShaderIDs.skyStarsTwinkleNoise, parameters.skyStarsTwinkleNoise == null ? CozyUtilities.BlackCubemap : parameters.skyStarsTwinkleNoise.value);
                }

                // Constellations
                if (currentStyle.UseSkyConstellations)
                {
                    if (parameters.skyConstellationsColor.value != lastFrameParameters.skyConstellationsColor.value) Shader.SetGlobalColor(CozyShaderIDs.skyConstellationsColor, parameters.skyConstellationsColor.value);
                    if (parameters.skyConstellationsCubemap.value != lastFrameParameters.skyConstellationsCubemap.value) Shader.SetGlobalTexture(CozyShaderIDs.skyConstellationsCubemap, parameters.skyConstellationsCubemap == null ? CozyUtilities.BlackCubemap : parameters.skyConstellationsCubemap.value);
                    if (parameters.skyConstellationsIntensity.value != lastFrameParameters.skyConstellationsIntensity.value) Shader.SetGlobalFloat(CozyShaderIDs.skyConstellationsIntensity, parameters.skyConstellationsIntensity.value);
                    if (parameters.skyConstellationsShimmerAmount.value != lastFrameParameters.skyConstellationsShimmerAmount.value) Shader.SetGlobalFloat(CozyShaderIDs.skyConstellationsShimmerAmount, parameters.skyConstellationsShimmerAmount.value);
                    if (parameters.skyConstellationsShimmerSpeed.value != lastFrameParameters.skyConstellationsShimmerSpeed.value) Shader.SetGlobalFloat(CozyShaderIDs.skyConstellationsShimmerSpeed, parameters.skyConstellationsShimmerSpeed.value);
                }

                // Galaxy
                if (currentStyle.UseSkyGalaxy)
                {
                    if (parameters.skyGalaxyPrimaryColor.value != lastFrameParameters.skyGalaxyPrimaryColor.value) Shader.SetGlobalColor(CozyShaderIDs.skyGalaxyPrimaryColor, parameters.skyGalaxyPrimaryColor.value);
                    if (parameters.skyGalaxySecondaryColor.value != lastFrameParameters.skyGalaxySecondaryColor.value) Shader.SetGlobalColor(CozyShaderIDs.skyGalaxySecondaryColor, parameters.skyGalaxySecondaryColor.value);
                    if (parameters.skyGalaxyTertiaryColor.value != lastFrameParameters.skyGalaxyTertiaryColor.value) Shader.SetGlobalColor(CozyShaderIDs.skyGalaxyTertiaryColor, parameters.skyGalaxyTertiaryColor.value);
                    if (parameters.skyGalaxyIntensity.value != lastFrameParameters.skyGalaxyIntensity.value) Shader.SetGlobalFloat(CozyShaderIDs.skyGalaxyIntensity, parameters.skyGalaxyIntensity.value);
                    if (parameters.skyGalaxyCubemap.value != lastFrameParameters.skyGalaxyCubemap.value) Shader.SetGlobalTexture(CozyShaderIDs.skyGalaxyCubemap, parameters.skyGalaxyCubemap == null ? CozyUtilities.BlackCubemap : parameters.skyGalaxyCubemap.value);
                }

                // Rainbow
                if (currentStyle.UseSkyRainbow)
                {
                    if (parameters.skyRainbowPlacement.value != lastFrameParameters.skyRainbowPlacement.value) Shader.SetGlobalFloat(CozyShaderIDs.skyRainbowPlacement, parameters.skyRainbowPlacement.value);
                    if (parameters.skyRainbowTexture.value != lastFrameParameters.skyRainbowTexture.value) Shader.SetGlobalTexture(CozyShaderIDs.skyRainbowTexture, parameters.skyRainbowTexture == null ? Texture2D.blackTexture : parameters.skyRainbowTexture.value);
                    if (parameters.skyRainbowWidth.value != lastFrameParameters.skyRainbowWidth.value) Shader.SetGlobalFloat(CozyShaderIDs.skyRainbowWidth, parameters.skyRainbowWidth.value);
                    if (parameters.skyRainbowIntensity.value != lastFrameParameters.skyRainbowIntensity.value) Shader.SetGlobalFloat(CozyShaderIDs.skyRainbowIntensity, parameters.skyRainbowIntensity.value);
                }

                // LightColumns
                if (currentStyle.UseSkyLightColumns)
                {
                    if (parameters.skyLightColumnsColor.value != lastFrameParameters.skyLightColumnsColor.value) Shader.SetGlobalColor(CozyShaderIDs.skyLightColumnsColor, parameters.skyLightColumnsColor.value);
                    if (parameters.skyLightColumnsTexture.value != lastFrameParameters.skyLightColumnsTexture.value) Shader.SetGlobalTexture(CozyShaderIDs.skyLightColumnsTexture, parameters.skyLightColumnsTexture == null ? Texture2D.blackTexture : parameters.skyLightColumnsTexture.value);
                    if (parameters.skyLightColumnsIntensity.value != lastFrameParameters.skyLightColumnsIntensity.value) Shader.SetGlobalFloat(CozyShaderIDs.skyLightColumnsIntensity, parameters.skyLightColumnsIntensity.value);
                    if (parameters.skyLightColumnsHeight.value != lastFrameParameters.skyLightColumnsHeight.value) Shader.SetGlobalFloat(CozyShaderIDs.skyLightColumnsHeight, parameters.skyLightColumnsHeight.value);
                    if (parameters.skyLightColumnsPosition.value != lastFrameParameters.skyLightColumnsPosition.value) Shader.SetGlobalFloat(CozyShaderIDs.skyLightColumnsPosition, parameters.skyLightColumnsPosition.value);
                }

                // ShootingStars
                if (currentStyle.UseSkyShootingStars)
                {
                    if (parameters.skyShootingStarsColor.value != lastFrameParameters.skyShootingStarsColor.value) Shader.SetGlobalColor(CozyShaderIDs.skyShootingStarsColor, parameters.skyShootingStarsColor.value);
                    if (parameters.skyShootingStarsSecondaryColor.value != lastFrameParameters.skyShootingStarsSecondaryColor.value) Shader.SetGlobalColor(CozyShaderIDs.skyShootingStarsSecondaryColor, parameters.skyShootingStarsSecondaryColor.value);
                    if (parameters.skyShootingStarsIntensity.value != lastFrameParameters.skyShootingStarsIntensity.value) Shader.SetGlobalFloat(CozyShaderIDs.skyShootingStarsIntensity, parameters.skyShootingStarsIntensity.value);
                    if (parameters.skyShootingStarsLifetime.value != lastFrameParameters.skyShootingStarsLifetime.value) Shader.SetGlobalFloat(CozyShaderIDs.skyShootingStarsLifetime, parameters.skyShootingStarsLifetime.value);
                    if (parameters.skyShootingStarsParticleCount.value != lastFrameParameters.skyShootingStarsParticleCount.value) Shader.SetGlobalFloat(CozyShaderIDs.skyShootingStarsParticleCount, parameters.skyShootingStarsParticleCount.value);
                    if (parameters.skyShootingStarsWidth.value != lastFrameParameters.skyShootingStarsWidth.value) Shader.SetGlobalFloat(CozyShaderIDs.skyShootingStarsWidth, parameters.skyShootingStarsWidth.value);
                    if (parameters.skyShootingStarsTravelDistance.value != lastFrameParameters.skyShootingStarsTravelDistance.value) Shader.SetGlobalFloat(CozyShaderIDs.skyShootingStarsTravelDistance, parameters.skyShootingStarsTravelDistance.value);
                    if (parameters.skyShootingStarsDelay.value != lastFrameParameters.skyShootingStarsDelay.value) Shader.SetGlobalFloat(CozyShaderIDs.skyShootingStarsDelay, parameters.skyShootingStarsDelay.value);
                }

                // SunDisk
                if (currentStyle.UseSkySunDisk)
                {
                    if (parameters.skySunDiskHaloColor.value != lastFrameParameters.skySunDiskHaloColor.value) Shader.SetGlobalColor(CozyShaderIDs.skySunDiskHaloColor, parameters.skySunDiskHaloColor.value);
                    if (parameters.skySunDiskHaloFalloff.value != lastFrameParameters.skySunDiskHaloFalloff.value) Shader.SetGlobalFloat(CozyShaderIDs.skySunDiskHaloFalloff, parameters.skySunDiskHaloFalloff.value);
                    if (parameters.skySunDiskColor.value != lastFrameParameters.skySunDiskColor.value) Shader.SetGlobalColor(CozyShaderIDs.skySunDiskColor, parameters.skySunDiskColor.value);
                    if (parameters.skySunDiskSize.value != lastFrameParameters.skySunDiskSize.value) Shader.SetGlobalFloat(CozyShaderIDs.skySunDiskSize, parameters.skySunDiskSize.value);
                    if (parameters.skySunDiskTexture.value != lastFrameParameters.skySunDiskTexture.value) Shader.SetGlobalTexture(CozyShaderIDs.skySunDiskTexture, parameters.skySunDiskTexture == null ? Texture2D.blackTexture : parameters.skySunDiskTexture.value);
                }

                // MoonDisk
                if (currentStyle.UseSkyMoonDisk)
                {
                    if (parameters.skyMoonDiskColor.value != lastFrameParameters.skyMoonDiskColor.value) Shader.SetGlobalColor(CozyShaderIDs.skyMoonDiskColor, parameters.skyMoonDiskColor.value);
                    if (parameters.skyMoonDiskSize.value != lastFrameParameters.skyMoonDiskSize.value) Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskSize, parameters.skyMoonDiskSize.value);
                    if (parameters.skyMoonDiskRotation.value != lastFrameParameters.skyMoonDiskRotation.value) Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskRotation, parameters.skyMoonDiskRotation.value);
                    if (parameters.skyMoonDiskTexture.value != lastFrameParameters.skyMoonDiskTexture.value) Shader.SetGlobalTexture(CozyShaderIDs.skyMoonDiskTexture, parameters.skyMoonDiskTexture == null ? Texture2D.blackTexture : parameters.skyMoonDiskTexture.value);
                    if (parameters.skyMoonDiskNormalMap.value != lastFrameParameters.skyMoonDiskNormalMap.value) Shader.SetGlobalTexture(CozyShaderIDs.skyMoonDiskNormalMap, parameters.skyMoonDiskNormalMap == null ? Texture2D.blackTexture : parameters.skyMoonDiskNormalMap.value);
                    if (parameters.skyMoonDiskNormalStrength.value != lastFrameParameters.skyMoonDiskNormalStrength.value) Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskNormalStrength, parameters.skyMoonDiskNormalStrength.value);
                    if (parameters.skyMoonDiskDeclination.value != lastFrameParameters.skyMoonDiskDeclination.value) Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskDeclination, parameters.skyMoonDiskDeclination.value);
                    if (parameters.skyMoonDiskDeclinationPeriod.value != lastFrameParameters.skyMoonDiskDeclinationPeriod.value) Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskDeclinationPeriod, parameters.skyMoonDiskDeclinationPeriod.value);
                    if (parameters.skyMoonDiskDeclinationOffset.value != lastFrameParameters.skyMoonDiskDeclinationOffset.value) Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskDeclinationOffset, parameters.skyMoonDiskDeclinationOffset.value);
                    if (parameters.skyMoonDiskOrbitPeriod.value != lastFrameParameters.skyMoonDiskOrbitPeriod.value) Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskOrbitPeriod, parameters.skyMoonDiskOrbitPeriod.value);
                    if (parameters.skyMoonDiskOrbitOffset.value != lastFrameParameters.skyMoonDiskOrbitOffset.value) Shader.SetGlobalFloat(CozyShaderIDs.skyMoonDiskOrbitOffset, parameters.skyMoonDiskOrbitOffset.value);
                }

            }
            #endregion

            #region Fog
            if (currentStyle.UseFog)
            {
                // Gradient
                if (currentStyle.UseFogGradient)
                {
                    if (parameters.fogGradientColor1.value != lastFrameParameters.fogGradientColor1.value) Shader.SetGlobalColor(CozyShaderIDs.fogGradientColor1, parameters.fogGradientColor1.value);
                    if (parameters.fogGradientColor2.value != lastFrameParameters.fogGradientColor2.value) Shader.SetGlobalColor(CozyShaderIDs.fogGradientColor2, parameters.fogGradientColor2.value);
                    if (parameters.fogGradientColor3.value != lastFrameParameters.fogGradientColor3.value) Shader.SetGlobalColor(CozyShaderIDs.fogGradientColor3, parameters.fogGradientColor3.value);
                    if (parameters.fogGradientColor4.value != lastFrameParameters.fogGradientColor4.value) Shader.SetGlobalColor(CozyShaderIDs.fogGradientColor4, parameters.fogGradientColor4.value);
                    if (parameters.fogGradientColor5.value != lastFrameParameters.fogGradientColor5.value) Shader.SetGlobalColor(CozyShaderIDs.fogGradientColor5, parameters.fogGradientColor5.value);
                    if (parameters.fogGradientDensity1.value != lastFrameParameters.fogGradientDensity1.value) Shader.SetGlobalFloat(CozyShaderIDs.fogGradientDensity1, parameters.fogGradientDensity1.value);
                    if (parameters.fogGradientDensity2.value != lastFrameParameters.fogGradientDensity2.value) Shader.SetGlobalFloat(CozyShaderIDs.fogGradientDensity2, parameters.fogGradientDensity2.value);
                    if (parameters.fogGradientDensity3.value != lastFrameParameters.fogGradientDensity3.value) Shader.SetGlobalFloat(CozyShaderIDs.fogGradientDensity3, parameters.fogGradientDensity3.value);
                    if (parameters.fogGradientDensity4.value != lastFrameParameters.fogGradientDensity4.value) Shader.SetGlobalFloat(CozyShaderIDs.fogGradientDensity4, parameters.fogGradientDensity4.value);
                    if (parameters.fogGradientDensity5.value != lastFrameParameters.fogGradientDensity5.value) Shader.SetGlobalFloat(CozyShaderIDs.fogGradientDensity5, parameters.fogGradientDensity5.value);
                    if (parameters.fogGradientStart1.value != lastFrameParameters.fogGradientStart1.value) Shader.SetGlobalFloat(CozyShaderIDs.fogGradientStart1, parameters.fogGradientStart1.value);
                    if (parameters.fogGradientStart2.value != lastFrameParameters.fogGradientStart2.value) Shader.SetGlobalFloat(CozyShaderIDs.fogGradientStart2, parameters.fogGradientStart2.value);
                    if (parameters.fogGradientStart3.value != lastFrameParameters.fogGradientStart3.value) Shader.SetGlobalFloat(CozyShaderIDs.fogGradientStart3, parameters.fogGradientStart3.value);
                    if (parameters.fogGradientStart4.value != lastFrameParameters.fogGradientStart4.value) Shader.SetGlobalFloat(CozyShaderIDs.fogGradientStart4, parameters.fogGradientStart4.value);
                    if (parameters.fogGradientStart5.value != lastFrameParameters.fogGradientStart5.value) Shader.SetGlobalFloat(CozyShaderIDs.fogGradientStart5, parameters.fogGradientStart5.value);
                }

                // SunLightFlare
                if (currentStyle.UseFogSunLightFlare)
                {
                    if (parameters.fogSunLightFlareColor.value != lastFrameParameters.fogSunLightFlareColor.value) Shader.SetGlobalColor(CozyShaderIDs.fogSunLightFlareColor, parameters.fogSunLightFlareColor.value);
                    if (parameters.fogSunLightFlareSquish.value != lastFrameParameters.fogSunLightFlareSquish.value) Shader.SetGlobalFloat(CozyShaderIDs.fogSunLightFlareSquish, parameters.fogSunLightFlareSquish.value);
                    if (parameters.fogSunLightFlareIntensity.value != lastFrameParameters.fogSunLightFlareIntensity.value) Shader.SetGlobalFloat(CozyShaderIDs.fogSunLightFlareIntensity, parameters.fogSunLightFlareIntensity.value);
                    if (parameters.fogSunLightFlareFalloff.value != lastFrameParameters.fogSunLightFlareFalloff.value) Shader.SetGlobalFloat(CozyShaderIDs.fogSunLightFlareFalloff, parameters.fogSunLightFlareFalloff.value);
                    if (parameters.fogSunLightFlareMinDistance.value != lastFrameParameters.fogSunLightFlareMinDistance.value) Shader.SetGlobalFloat(CozyShaderIDs.fogSunLightFlareMinDistance, parameters.fogSunLightFlareMinDistance.value);
                    if (parameters.fogSunLightFlareMaxDistance.value != lastFrameParameters.fogSunLightFlareMaxDistance.value) Shader.SetGlobalFloat(CozyShaderIDs.fogSunLightFlareMaxDistance, parameters.fogSunLightFlareMaxDistance.value);
                }

                // MoonLightFlare
                if (currentStyle.UseFogMoonLightFlare)
                {
                    if (parameters.fogMoonLightFlareColor.value != lastFrameParameters.fogMoonLightFlareColor.value) Shader.SetGlobalColor(CozyShaderIDs.fogMoonLightFlareColor, parameters.fogMoonLightFlareColor.value);
                    if (parameters.fogMoonLightFlareSquish.value != lastFrameParameters.fogMoonLightFlareSquish.value) Shader.SetGlobalFloat(CozyShaderIDs.fogMoonLightFlareSquish, parameters.fogMoonLightFlareSquish.value);
                    if (parameters.fogMoonLightFlareIntensity.value != lastFrameParameters.fogMoonLightFlareIntensity.value) Shader.SetGlobalFloat(CozyShaderIDs.fogMoonLightFlareIntensity, parameters.fogMoonLightFlareIntensity.value);
                    if (parameters.fogMoonLightFlareFalloff.value != lastFrameParameters.fogMoonLightFlareFalloff.value) Shader.SetGlobalFloat(CozyShaderIDs.fogMoonLightFlareFalloff, parameters.fogMoonLightFlareFalloff.value);
                    if (parameters.fogMoonLightFlareMinDistance.value != lastFrameParameters.fogMoonLightFlareMinDistance.value) Shader.SetGlobalFloat(CozyShaderIDs.fogMoonLightFlareMinDistance, parameters.fogMoonLightFlareMinDistance.value);
                    if (parameters.fogMoonLightFlareMaxDistance.value != lastFrameParameters.fogMoonLightFlareMaxDistance.value) Shader.SetGlobalFloat(CozyShaderIDs.fogMoonLightFlareMaxDistance, parameters.fogMoonLightFlareMaxDistance.value);
                }

                // TwoTone
                if (currentStyle.UseFogTwoTone)
                {
                    if (parameters.fogTwoToneSunSideColor.value != lastFrameParameters.fogTwoToneSunSideColor.value) Shader.SetGlobalColor(CozyShaderIDs.fogTwoToneSunSideColor, parameters.fogTwoToneSunSideColor.value);
                    if (parameters.fogTwoToneAntiSunSideColor.value != lastFrameParameters.fogTwoToneAntiSunSideColor.value) Shader.SetGlobalColor(CozyShaderIDs.fogTwoToneAntiSunSideColor, parameters.fogTwoToneAntiSunSideColor.value);
                    if (parameters.fogTwoToneSunSideDensity.value != lastFrameParameters.fogTwoToneSunSideDensity.value) Shader.SetGlobalFloat(CozyShaderIDs.fogTwoToneSunSideDensity, parameters.fogTwoToneSunSideDensity.value);
                    if (parameters.fogTwoToneAntiSunSideDensity.value != lastFrameParameters.fogTwoToneAntiSunSideDensity.value) Shader.SetGlobalFloat(CozyShaderIDs.fogTwoToneAntiSunSideDensity, parameters.fogTwoToneAntiSunSideDensity.value);
                    if (parameters.fogTwoToneSmoothness.value != lastFrameParameters.fogTwoToneSmoothness.value) Shader.SetGlobalFloat(CozyShaderIDs.fogTwoToneSmoothness, parameters.fogTwoToneSmoothness.value);
                    if (parameters.fogTwoToneOffset.value != lastFrameParameters.fogTwoToneOffset.value) Shader.SetGlobalFloat(CozyShaderIDs.fogTwoToneOffset, parameters.fogTwoToneOffset.value);
                }

                // Height
                if (currentStyle.UseFogHeight)
                {
                    if (parameters.fogHeightColor.value != lastFrameParameters.fogHeightColor.value) Shader.SetGlobalColor(CozyShaderIDs.fogHeightColor, parameters.fogHeightColor.value);
                    if (parameters.fogHeightFalloff.value != lastFrameParameters.fogHeightFalloff.value) Shader.SetGlobalFloat(CozyShaderIDs.fogHeightFalloff, parameters.fogHeightFalloff.value);
                    if (parameters.fogHeightSmoothness.value != lastFrameParameters.fogHeightSmoothness.value) Shader.SetGlobalFloat(CozyShaderIDs.fogHeightSmoothness, parameters.fogHeightSmoothness.value);
                    if (parameters.fogHeightBaseHeight.value != lastFrameParameters.fogHeightBaseHeight.value) Shader.SetGlobalFloat(CozyShaderIDs.fogHeightBaseHeight, parameters.fogHeightBaseHeight.value);
                    if (parameters.fogHeightDensity.value != lastFrameParameters.fogHeightDensity.value) Shader.SetGlobalFloat(CozyShaderIDs.fogHeightDensity, parameters.fogHeightDensity.value);
                    if (parameters.fogHeightVariationNoise.value != lastFrameParameters.fogHeightVariationNoise.value) Shader.SetGlobalTexture(CozyShaderIDs.fogHeightVariationNoise, parameters.fogHeightVariationNoise == null ? Texture2D.blackTexture : parameters.fogHeightVariationNoise.value);
                    if (parameters.fogHeightVariationAmount.value != lastFrameParameters.fogHeightVariationAmount.value) Shader.SetGlobalFloat(CozyShaderIDs.fogHeightVariationAmount, parameters.fogHeightVariationAmount.value);
                    if (parameters.fogHeightVariationTextureScale.value != lastFrameParameters.fogHeightVariationTextureScale.value) Shader.SetGlobalVector(CozyShaderIDs.fogHeightVariationTextureScale, parameters.fogHeightVariationTextureScale.value);
                    if (parameters.fogHeightVariationTextureFlow.value != lastFrameParameters.fogHeightVariationTextureFlow.value) Shader.SetGlobalVector(CozyShaderIDs.fogHeightVariationTextureFlow, parameters.fogHeightVariationTextureFlow.value);
                }

                // Base
                if (currentStyle.UseFogBase)
                {
                    if (parameters.fogBaseDensityMultiplier.value != lastFrameParameters.fogBaseDensityMultiplier.value) Shader.SetGlobalFloat(CozyShaderIDs.fogBaseDensityMultiplier, parameters.fogBaseDensityMultiplier.value);
                    if (parameters.fogBaseHeight.value != lastFrameParameters.fogBaseHeight.value) Shader.SetGlobalFloat(CozyShaderIDs.fogBaseHeight, parameters.fogBaseHeight.value);
                    if (parameters.fogBaseSmoothness.value != lastFrameParameters.fogBaseSmoothness.value) Shader.SetGlobalFloat(CozyShaderIDs.fogBaseSmoothness, parameters.fogBaseSmoothness.value);
                    if (parameters.fogBaseBaseHeight.value != lastFrameParameters.fogBaseBaseHeight.value) Shader.SetGlobalFloat(CozyShaderIDs.fogBaseBaseHeight, parameters.fogBaseBaseHeight.value);
                    if (parameters.fogBaseBaseSmoothness.value != lastFrameParameters.fogBaseBaseSmoothness.value) Shader.SetGlobalFloat(CozyShaderIDs.fogBaseBaseSmoothness, parameters.fogBaseBaseSmoothness.value);
                    if (parameters.fogBaseTransitionDistance.value != lastFrameParameters.fogBaseTransitionDistance.value) Shader.SetGlobalFloat(CozyShaderIDs.fogBaseTransitionDistance, parameters.fogBaseTransitionDistance.value);
                }

            }
            #endregion

            #region Clouds
            if (currentStyle.UseClouds)
            {
                // Volumetric
                if (currentStyle.UseCloudsVolumetric)
                {
                    if (parameters.cloudsVolumetricMainTexture.value != lastFrameParameters.cloudsVolumetricMainTexture.value) Shader.SetGlobalTexture(CozyShaderIDs.cloudsVolumetricMainTexture, parameters.cloudsVolumetricMainTexture == null ? Texture2D.blackTexture : parameters.cloudsVolumetricMainTexture.value);
                    if (parameters.cloudsVolumetricNoiseTexture.value != lastFrameParameters.cloudsVolumetricNoiseTexture.value) Shader.SetGlobalTexture(CozyShaderIDs.cloudsVolumetricNoiseTexture, parameters.cloudsVolumetricNoiseTexture == null ? Texture2D.blackTexture : parameters.cloudsVolumetricNoiseTexture.value);
                    if (parameters.cloudsVolumetricHeight.value != lastFrameParameters.cloudsVolumetricHeight.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricHeight, parameters.cloudsVolumetricHeight.value);
                    if (parameters.cloudsVolumetricDarkeningIntensity.value != lastFrameParameters.cloudsVolumetricDarkeningIntensity.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricDarkeningIntensity, parameters.cloudsVolumetricDarkeningIntensity.value);
                    if (parameters.cloudsVolumetricLitColor.value != lastFrameParameters.cloudsVolumetricLitColor.value) Shader.SetGlobalColor(CozyShaderIDs.cloudsVolumetricLitColor, parameters.cloudsVolumetricLitColor.value);
                    if (parameters.cloudsVolumetricShadeColor.value != lastFrameParameters.cloudsVolumetricShadeColor.value) Shader.SetGlobalColor(CozyShaderIDs.cloudsVolumetricShadeColor, parameters.cloudsVolumetricShadeColor.value);
                    if (parameters.cloudsVolumetricPlacementScale1.value != lastFrameParameters.cloudsVolumetricPlacementScale1.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricPlacementScale1, parameters.cloudsVolumetricPlacementScale1.value);
                    if (parameters.cloudsVolumetricPlacementScale2.value != lastFrameParameters.cloudsVolumetricPlacementScale2.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricPlacementScale2, parameters.cloudsVolumetricPlacementScale2.value);
                    if (parameters.cloudsVolumetricDensity.value != lastFrameParameters.cloudsVolumetricDensity.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricDensity, parameters.cloudsVolumetricDensity.value);
                    if (parameters.cloudsVolumetricNoiseScale.value != lastFrameParameters.cloudsVolumetricNoiseScale.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricNoiseScale, parameters.cloudsVolumetricNoiseScale.value);
                    if (parameters.cloudsVolumetricNoiseAmount.value != lastFrameParameters.cloudsVolumetricNoiseAmount.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricNoiseAmount, parameters.cloudsVolumetricNoiseAmount.value);
                    if (parameters.cloudsVolumetricPlacementHeight.value != lastFrameParameters.cloudsVolumetricPlacementHeight.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricPlacementHeight, parameters.cloudsVolumetricPlacementHeight.value);
                    if (parameters.cloudsVolumetricRamp.value != lastFrameParameters.cloudsVolumetricRamp.value) Shader.SetGlobalTexture(CozyShaderIDs.cloudsVolumetricRamp, parameters.cloudsVolumetricRamp == null ? Texture2D.blackTexture : parameters.cloudsVolumetricRamp.value);
                    if (parameters.cloudsVolumetricStepSize.value != lastFrameParameters.cloudsVolumetricStepSize.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricStepSize, parameters.cloudsVolumetricStepSize.value);
                    if (parameters.cloudsVolumetricSteps.value != lastFrameParameters.cloudsVolumetricSteps.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricSteps, parameters.cloudsVolumetricSteps.value);
                    if (parameters.cloudsVolumetricLODCoefficient.value != lastFrameParameters.cloudsVolumetricLODCoefficient.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricLODCoefficient, parameters.cloudsVolumetricLODCoefficient.value);
                    if (parameters.cloudsVolumetricLODSteps.value != lastFrameParameters.cloudsVolumetricLODSteps.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricLODSteps, parameters.cloudsVolumetricLODSteps.value);
                    if (parameters.cloudsVolumetricBlueNoise.value != lastFrameParameters.cloudsVolumetricBlueNoise.value) Shader.SetGlobalTexture(CozyShaderIDs.cloudsVolumetricBlueNoise, parameters.cloudsVolumetricBlueNoise == null ? Texture2D.blackTexture : parameters.cloudsVolumetricBlueNoise.value);
                    if (parameters.cloudsVolumetricWindStrength.value != lastFrameParameters.cloudsVolumetricWindStrength.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricWindStrength, parameters.cloudsVolumetricWindStrength.value);
                    if (parameters.cloudsVolumetricHighlightBrightness.value != lastFrameParameters.cloudsVolumetricHighlightBrightness.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricHighlightBrightness, parameters.cloudsVolumetricHighlightBrightness.value);
                    if (parameters.cloudsVolumetricHighlightStepSize.value != lastFrameParameters.cloudsVolumetricHighlightStepSize.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsVolumetricHighlightStepSize, parameters.cloudsVolumetricHighlightStepSize.value);
                }

                // Cumulonimbus
                if (currentStyle.UseCloudsCumulonimbus)
                {
                    if (parameters.cloudsCumulonimbusMainTexture.value != lastFrameParameters.cloudsCumulonimbusMainTexture.value) Shader.SetGlobalTexture(CozyShaderIDs.cloudsCumulonimbusMainTexture, parameters.cloudsCumulonimbusMainTexture == null ? Texture2D.blackTexture : parameters.cloudsCumulonimbusMainTexture.value);
                    if (parameters.cloudsCumulonimbusRamp.value != lastFrameParameters.cloudsCumulonimbusRamp.value) Shader.SetGlobalTexture(CozyShaderIDs.cloudsCumulonimbusRamp, parameters.cloudsCumulonimbusRamp == null ? Texture2D.blackTexture : parameters.cloudsCumulonimbusRamp.value);
                }

                // Cumulus
                if (currentStyle.UseCloudsCumulus)
                {
                    if (parameters.cloudsCumulusMainTexture.value != lastFrameParameters.cloudsCumulusMainTexture.value) Shader.SetGlobalTexture(CozyShaderIDs.cloudsCumulusMainTexture, parameters.cloudsCumulusMainTexture == null ? Texture2D.blackTexture : parameters.cloudsCumulusMainTexture.value);
                    if (parameters.cloudsCumulusErosionTexture.value != lastFrameParameters.cloudsCumulusErosionTexture.value) Shader.SetGlobalTexture(CozyShaderIDs.cloudsCumulusErosionTexture, parameters.cloudsCumulusErosionTexture == null ? Texture2D.blackTexture : parameters.cloudsCumulusErosionTexture.value);
                    if (parameters.cloudsCumulusHeight.value != lastFrameParameters.cloudsCumulusHeight.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsCumulusHeight, parameters.cloudsCumulusHeight.value);
                    if (parameters.cloudsCumulusLitColor.value != lastFrameParameters.cloudsCumulusLitColor.value) Shader.SetGlobalColor(CozyShaderIDs.cloudsCumulusLitColor, parameters.cloudsCumulusLitColor.value);
                    if (parameters.cloudsCumulusShadeColor.value != lastFrameParameters.cloudsCumulusShadeColor.value) Shader.SetGlobalColor(CozyShaderIDs.cloudsCumulusShadeColor, parameters.cloudsCumulusShadeColor.value);
                    if (parameters.cloudsCumulusThickness.value != lastFrameParameters.cloudsCumulusThickness.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsCumulusThickness, parameters.cloudsCumulusThickness.value);
                    if (parameters.cloudsCumulusDensity.value != lastFrameParameters.cloudsCumulusDensity.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsCumulusDensity, parameters.cloudsCumulusDensity.value);
                    if (parameters.cloudsCumulusWindStrength.value != lastFrameParameters.cloudsCumulusWindStrength.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsCumulusWindStrength, parameters.cloudsCumulusWindStrength.value);
                    if (parameters.cloudsCumulusCohesion.value != lastFrameParameters.cloudsCumulusCohesion.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsCumulusCohesion, parameters.cloudsCumulusCohesion.value);
                }

                // Altocumulus
                if (currentStyle.UseCloudsAltocumulus)
                {
                    if (parameters.cloudsAltocumulusMainTexture.value != lastFrameParameters.cloudsAltocumulusMainTexture.value) Shader.SetGlobalTexture(CozyShaderIDs.cloudsAltocumulusMainTexture, parameters.cloudsAltocumulusMainTexture == null ? Texture2D.blackTexture : parameters.cloudsAltocumulusMainTexture.value);
                    if (parameters.cloudsAltocumulusHeight.value != lastFrameParameters.cloudsAltocumulusHeight.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsAltocumulusHeight, parameters.cloudsAltocumulusHeight.value);
                    if (parameters.cloudsAltocumulusTextureScale.value != lastFrameParameters.cloudsAltocumulusTextureScale.value) Shader.SetGlobalVector(CozyShaderIDs.cloudsAltocumulusTextureScale, parameters.cloudsAltocumulusTextureScale.value);
                    if (parameters.cloudsAltocumulusColor.value != lastFrameParameters.cloudsAltocumulusColor.value) Shader.SetGlobalColor(CozyShaderIDs.cloudsAltocumulusColor, parameters.cloudsAltocumulusColor.value);
                    if (parameters.cloudsAltocumulusDensity.value != lastFrameParameters.cloudsAltocumulusDensity.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsAltocumulusDensity, parameters.cloudsAltocumulusDensity.value);
                    if (parameters.cloudsAltocumulusRamp.value != lastFrameParameters.cloudsAltocumulusRamp.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsAltocumulusRamp, parameters.cloudsAltocumulusRamp.value);
                    if (parameters.cloudsAltocumulusWindStrength.value != lastFrameParameters.cloudsAltocumulusWindStrength.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsAltocumulusWindStrength, parameters.cloudsAltocumulusWindStrength.value);
                }

                // Cirrus
                if (currentStyle.UseCloudsCirrus)
                {
                    if (parameters.cloudsCirrusMainTexture.value != lastFrameParameters.cloudsCirrusMainTexture.value) Shader.SetGlobalTexture(CozyShaderIDs.cloudsCirrusMainTexture, parameters.cloudsCirrusMainTexture == null ? Texture2D.blackTexture : parameters.cloudsCirrusMainTexture.value);
                    if (parameters.cloudsCirrusHeight.value != lastFrameParameters.cloudsCirrusHeight.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsCirrusHeight, parameters.cloudsCirrusHeight.value);
                    if (parameters.cloudsCirrusTextureScale.value != lastFrameParameters.cloudsCirrusTextureScale.value) Shader.SetGlobalVector(CozyShaderIDs.cloudsCirrusTextureScale, parameters.cloudsCirrusTextureScale.value);
                    if (parameters.cloudsCirrusColor.value != lastFrameParameters.cloudsCirrusColor.value) Shader.SetGlobalColor(CozyShaderIDs.cloudsCirrusColor, parameters.cloudsCirrusColor.value);
                    if (parameters.cloudsCirrusDensity.value != lastFrameParameters.cloudsCirrusDensity.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsCirrusDensity, parameters.cloudsCirrusDensity.value);
                    if (parameters.cloudsCirrusWindStrength.value != lastFrameParameters.cloudsCirrusWindStrength.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsCirrusWindStrength, parameters.cloudsCirrusWindStrength.value);
                    if (parameters.cloudsCirrusWispFactor.value != lastFrameParameters.cloudsCirrusWispFactor.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsCirrusWispFactor, parameters.cloudsCirrusWispFactor.value);
                }

                // Contrails
                if (currentStyle.UseCloudsContrails)
                {
                    if (parameters.cloudsContrailsMainTexture.value != lastFrameParameters.cloudsContrailsMainTexture.value) Shader.SetGlobalTexture(CozyShaderIDs.cloudsContrailsMainTexture, parameters.cloudsContrailsMainTexture == null ? Texture2D.blackTexture : parameters.cloudsContrailsMainTexture.value);
                    if (parameters.cloudsContrailsHeight.value != lastFrameParameters.cloudsContrailsHeight.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsContrailsHeight, parameters.cloudsContrailsHeight.value);
                    if (parameters.cloudsContrailsTextureScale.value != lastFrameParameters.cloudsContrailsTextureScale.value) Shader.SetGlobalVector(CozyShaderIDs.cloudsContrailsTextureScale, parameters.cloudsContrailsTextureScale.value);
                    if (parameters.cloudsContrailsColor.value != lastFrameParameters.cloudsContrailsColor.value) Shader.SetGlobalColor(CozyShaderIDs.cloudsContrailsColor, parameters.cloudsContrailsColor.value);
                    if (parameters.cloudsContrailsDensity.value != lastFrameParameters.cloudsContrailsDensity.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsContrailsDensity, parameters.cloudsContrailsDensity.value);
                    if (parameters.cloudsContrailsWindStrength.value != lastFrameParameters.cloudsContrailsWindStrength.value) Shader.SetGlobalFloat(CozyShaderIDs.cloudsContrailsWindStrength, parameters.cloudsContrailsWindStrength.value);
                }

            }
            #endregion

            #region Sunshafts
            if (currentStyle.UseSunshafts)
            {
                // Screenspace
                if (currentStyle.UseSunshaftsScreenspace)
                {
                    if (parameters.sunshaftsScreenspaceColor.value != lastFrameParameters.sunshaftsScreenspaceColor.value) Shader.SetGlobalColor(CozyShaderIDs.sunshaftsScreenspaceColor, parameters.sunshaftsScreenspaceColor.value);
                    if (parameters.sunshaftsScreenspaceIntensity.value != lastFrameParameters.sunshaftsScreenspaceIntensity.value) Shader.SetGlobalFloat(CozyShaderIDs.sunshaftsScreenspaceIntensity, parameters.sunshaftsScreenspaceIntensity.value);
                    if (parameters.sunshaftsScreenspaceLength.value != lastFrameParameters.sunshaftsScreenspaceLength.value) Shader.SetGlobalFloat(CozyShaderIDs.sunshaftsScreenspaceLength, parameters.sunshaftsScreenspaceLength.value);
                    if (parameters.sunshaftsScreenspaceSteps.value != lastFrameParameters.sunshaftsScreenspaceSteps.value) Shader.SetGlobalFloat(CozyShaderIDs.sunshaftsScreenspaceSteps, parameters.sunshaftsScreenspaceSteps.value);
                    if (parameters.sunshaftsScreenspaceCloudMaskAmount.value != lastFrameParameters.sunshaftsScreenspaceCloudMaskAmount.value) Shader.SetGlobalFloat(CozyShaderIDs.sunshaftsScreenspaceCloudMaskAmount, parameters.sunshaftsScreenspaceCloudMaskAmount.value);
                }

            }
            #endregion

            // END GENERATED VARIABLE UPDATE

            float t = WeatherSphere.Time != null ? WeatherSphere.Time.ModifiedTime : 0.5f;

            // 1 = Full Moon, 0 = New Moon
            UpdateOrientation(t, out float moonPhasePercentage, out float moonPhaseSign);

            if (WeatherSphere.Wind != null)
            {
                cloudWindOffset += Time.deltaTime * WeatherSphere.Wind.WindStrength * WeatherSphere.Wind.WindDirection * 0.2f;
                cloudNoiseTime += Time.deltaTime * WeatherSphere.Wind.WindStrength;
            }
            else
            {
                cloudWindOffset -= Vector3.right * Time.deltaTime * 0.2f;
                cloudNoiseTime += Time.deltaTime;
            }

            Shader.SetGlobalFloat(CozyShaderIDs.baseWeatherCloudNoiseTime, cloudNoiseTime);

            Shader.SetGlobalVector(CozyShaderIDs.baseWeatherCloudWindOffset, cloudWindOffset);
            Shader.SetGlobalVector(CozyShaderIDs.baseWeatherCloudWorldOffset, cloudWorldOffset);
            Shader.SetGlobalVector(CozyShaderIDs.baseOrientationSunDirection, sunDirection);
            Shader.SetGlobalVector(CozyShaderIDs.baseOrientationMoonDirection, -moonDirection);
            Shader.SetGlobalVector(CozyShaderIDs.baseOrientationWest, CozyPreferences.West);
            Shader.SetGlobalVector(CozyShaderIDs.baseOrientationNorth, CozyPreferences.North);
            Shader.SetGlobalFloat(CozyShaderIDs.baseOrientationSkyPitch, CozyPreferences.SkyPitch);
            Shader.SetGlobalFloat(CozyShaderIDs.baseOrientationSkyRotation, CozyPreferences.SkyRotation);
            Shader.SetGlobalFloat(CozyShaderIDs.fogLayersSkyFogAmount, CozyPreferences.SkyFogAmount);
            Shader.SetGlobalFloat(CozyShaderIDs.fogLayersCloudsFogAmount, CozyPreferences.CloudsFogAmount);
            Shader.SetGlobalFloat(CozyShaderIDs.baseTimeYearPercentage, WeatherSphere.Calendar == null ? 0 : WeatherSphere.Calendar.CurrentDate.YearPercentage);
            Shader.SetGlobalFloat(CozyShaderIDs.baseTimeDayPercentage, t);
            Shader.SetGlobalFloat(CozyShaderIDs.baseTimeMoonPhasePercentage, moonPhasePercentage);
            Shader.SetGlobalFloat(CozyShaderIDs.baseTimeMoonPhaseSign, moonPhaseSign);

            Shader.SetGlobalColor(CozyShaderIDs.baseLightingSunColor, (Color)parameters.lightingSunLightColor * (float)parameters.lightingSunLightIntensity);
            Shader.SetGlobalColor(CozyShaderIDs.baseLightingMoonColor, (Color)parameters.lightingMoonLightColor * (float)parameters.lightingMoonLightIntensity);

            if (CozyPreferences.HandleSceneLighting)
            {
                if (!sunLight || !sunLensFlare) SetupReferences();
                UpdateSceneLighting(currentStyle, t, moonPhasePercentage);
            }
            else
            {
                DisableSceneLights();
            }

            lastFrameParameters.Copy(parameters);

        }

        Quaternion GetSkyBaseRotation()
        {
            float pitch = CozyPreferences.SkyPitch;
            float rotation = CozyPreferences.SkyRotation;

            if (pitch != cachedSkyPitch || rotation != cachedSkyRotation)
            {
                cachedSkyPitch = pitch;
                cachedSkyRotation = rotation;
                cachedSkyBaseRotation = Quaternion.AngleAxis(rotation, Vector3.up) * Quaternion.AngleAxis(pitch, Vector3.forward);
            }

            return cachedSkyBaseRotation;
        }

        Quaternion GetSunBase(float t, bool quantizable = false)
        {
            if (quantizable && CozyPreferences.QuantizeLighting)
            {
                float angle = CozyPreferences.QuantizeLightingAngle;
                t = Mathf.Round(t * 360 / angle) * angle / 360;
            }

            return GetSkyBaseRotation() * Quaternion.AngleAxis((t * 360f) - 90f, Vector3.right);
        }

        Vector3 GetSunDir(float t, bool quantizable = false)
        {
            return GetSunBase(t, quantizable) * Vector3.forward;
        }

        /// <summary>
        /// Calculates the sun and moon light directions for a supplied calendar day and time.
        /// </summary>
        public void GetCelestialDirections(int day, float timeOfDay, out Vector3 sun, out Vector3 moon)
        {
            if (CozyPreferences.StaticLighting)
            {
                Vector3 staticDirection = Quaternion.Euler(CozyPreferences.StaticLightingRotation) * Vector3.forward;
                sun = staticDirection;
                moon = staticDirection;
                return;
            }

            Quaternion sunBase = GetSunBase(timeOfDay);
            sun = sunBase * Vector3.forward;

            switch (parameters.skyMoonDiskPlacementMode.value)
            {
                case MoonPlacementMode.AlwaysOpposeSun:
                    moon = -sun;
                    break;

                case MoonPlacementMode.Realistic:
                    float orbitPeriodLength = parameters.skyMoonDiskOrbitPeriod;
                    float orbitPeriod = Mathf.Repeat(day + timeOfDay + parameters.skyMoonDiskOrbitOffset, orbitPeriodLength);

                    int declinationPeriodDay = Mathf.FloorToInt((day + parameters.skyMoonDiskDeclinationOffset) % parameters.skyMoonDiskDeclinationPeriod);
                    float declinationPeriod = declinationPeriodDay + timeOfDay;
                    float declination = parameters.skyMoonDiskDeclination * Mathf.Sin(Mathf.PI * 2 * (declinationPeriod / parameters.skyMoonDiskDeclinationPeriod));
                    float orbit = -360f * ((orbitPeriod + 0.5f) / orbitPeriodLength);

                    Quaternion moonRotation =
                        sunBase *
                        Quaternion.AngleAxis(declination, Vector3.up) *
                        Quaternion.AngleAxis(orbit, Vector3.right);

                    moon = moonRotation * Vector3.back;
                    break;

                default:
                    moon = -sun;
                    break;
            }
        }

        void UpdateOrientation(float t, out float moonPhasePercentage, out float moonPhaseSign)
        {
            int day = WeatherSphere.Calendar == null ? 0 : WeatherSphere.Calendar.CurrentDate.AbsoluteDay;
            float orbitPeriodLength = parameters.skyMoonDiskOrbitPeriod;
            float orbitPeriod = Mathf.Repeat(day + t + parameters.skyMoonDiskOrbitOffset, orbitPeriodLength);

            float cycleFrac = Mathf.Repeat(orbitPeriod / orbitPeriodLength, 1f);
            moonPhasePercentage = 1f - Mathf.Abs(cycleFrac - 0.5f) * 2f;
            moonPhaseSign = cycleFrac < 0.5f ? 1f : -1f;

            GetCelestialDirections(day, t, out sunDirection, out moonDirection);

            if (CozyPreferences.StaticLighting)
            {
                return;
            }

            if (parameters.skyMoonDiskPlacementMode == MoonPlacementMode.Realistic)
            {
                moonPhasePercentage = (Vector3.Dot(sunDirection, -moonDirection) + 1) / 2f;
            }
        }

        void UpdateSceneLighting(CozyStyle currentStyle, float t, float moonPhasePercentage)
        {
            RenderSettings.sun = sunLight;

            bool hasFXProvider = WeatherSphere.FXProvider != null;
            FXParameters fxParameters = default;

            if (hasFXProvider)
                fxParameters = WeatherSphere.FXProvider.FXParameters;

            float lightningMultiplier = Mathf.Clamp01(1 - lightningLightingOverride);
            Camera cozyCamera = WeatherSphere.CozyCamera;
            Vector3 cameraPosition = Vector3.zero;
            if (cozyCamera)
                cameraPosition = cozyCamera.transform.position;

            if (currentStyle.UseLightingAmbient)
            {
                RenderSettings.ambientMode = parameters.lightingAmbientAmbientMode.value == AmbientLightingType.Gradient ? AmbientMode.Trilight : AmbientMode.Flat;

                Color equator = (Color)parameters.lightingAmbientHorizonColor * parameters.lightingAmbientIntensity;
                Color sky = (Color)parameters.lightingAmbientZenithColor * parameters.lightingAmbientIntensity;
                Color ground = (Color)parameters.lightingAmbientGroundColor * parameters.lightingAmbientIntensity;

                if (hasFXProvider)
                {
                    equator *= fxParameters.filterColor;
                    sky *= fxParameters.filterColor;
                    ground *= fxParameters.filterColor;
                }

                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientGroundColor = ground;
            }

            Color sunColor = parameters.lightingSunLightColor;
            Color moonColor = parameters.lightingMoonLightColor;

            bool sunOn = (sunColor.r + sunColor.g + sunColor.b) * parameters.lightingSunLightIntensity * lightningMultiplier > 0;
            bool moonOn = (moonColor.r + moonColor.g + moonColor.b) * parameters.lightingMoonLightIntensity * lightningMultiplier > 0;

            bool sunLightEnabled = sunOn && !overrideSceneLights;
            bool moonLightEnabled = moonOn && !overrideSceneLights;
            if (sunLight.enabled != sunLightEnabled) sunLight.enabled = sunLightEnabled;
            if (sunLensFlare.enabled != sunOn) sunLensFlare.enabled = sunOn;
            if (moonLight.enabled != moonLightEnabled) moonLight.enabled = moonLightEnabled;
            if (moonLensFlare.enabled != moonOn) moonLensFlare.enabled = moonOn;

            LightShadows sunShadows = sunOn ? parameters.lightingSunShadowType : LightShadows.None;
            if (sunLight.shadows != sunShadows) sunLight.shadows = sunShadows;

            LightShadows moonShadows = !sunOn && moonOn ? parameters.lightingMoonShadowType : LightShadows.None;
            if (moonLight.shadows != moonShadows) moonLight.shadows = moonShadows;

            if (sunOn)
            {
                sunLight.color = hasFXProvider ? sunColor * fxParameters.sunFilter : sunColor;
                sunLight.intensity = parameters.lightingSunLightIntensity * lightningMultiplier;
                sunLight.shadowStrength = parameters.lightingSunShadowIntensity;

                Vector3 sunForward;
                if (CozyPreferences.StaticLighting)
                {
                    sunForward = sunDirection;
                }
                else
                {
                    float sunAngleClamped = 90 - CozyPreferences.MinLightAngle;
                    sunForward = -Vector3.RotateTowards(Vector3.up, -GetSunDir(t, true), sunAngleClamped * Mathf.Deg2Rad, 0);
                }

                Quaternion sunLightRotation = Quaternion.LookRotation(sunForward);
                sunLightTransform.rotation = sunLightRotation;

                if (currentStyle.UseLightingSunLensFlare)
                {
                    sunFlareTransform.rotation = Quaternion.LookRotation(sunDirection);
                    if (sunLensFlare.lensFlareData != parameters.lightingSunLensFlareReference)
                        sunLensFlare.lensFlareData = parameters.lightingSunLensFlareReference;

                    if (Time.frameCount % framesBetweenLensFlareOcclusionRendering == 0)
                    {
                        if (cozyCamera != null)
                        {
                            Vector3 screenPoint = cozyCamera.WorldToScreenPoint(cameraPosition - sunDirection * 10000);

                            if (screenPoint.z > 0)
                                sunFlareOcclusion = Mathf.Lerp(sunFlareOcclusion, FlareOcclusion.GetFlareOcclusion(screenPoint, sunFlareEntityID), 0.9f * Time.deltaTime);
                        }
                    }

                    sunLensFlare.intensity = parameters.lightingSunLightIntensity * parameters.lightingSunLensFlareIntensity * sunFlareOcclusion;
                    sunLensFlare.scale = parameters.lightingSunLensFlareScale;
                }
                else
                {
                    sunLensFlare.intensity = 0;
                }

                if (currentStyle.UseCloudsVolumetric && currentStyle.UseLightingSunCloudShadows && shadowShader)
                {
                    if (sunLight == null || sunLightData == null)
                    {
                        SetupReferences();
                    }
                    if (cloudShadowRefreshRequested || Time.frameCount % framesBetweenCloudShadowRendering == 0)
                    {
                        RenderCloudShadowCookie(cameraPosition, sunLightRotation);
                        cloudShadowRefreshRequested = false;
                    }
                }
                else if (sunCookieApplied)
                {
                    sunLight.cookie = null;
                    sunCookieApplied = false;
                }
            }

            if (moonOn)
            {
                moonLight.color = hasFXProvider ? moonColor * fxParameters.sunFilter : moonColor;
                moonLightTransform.rotation = Quaternion.LookRotation(moonDirection);

                moonLight.intensity = Mathf.Max(0, parameters.lightingMoonLightIntensity * Mathf.Min(1, -moonDirection.y + 0.5f)) * lightningMultiplier;
                moonLight.shadowStrength = parameters.lightingMoonShadowIntensity;

                if (currentStyle.UseLightingMoonLensFlare)
                {
                    moonFlareTransform.rotation = Quaternion.LookRotation(moonDirection);


                    if (Time.frameCount % framesBetweenLensFlareOcclusionRendering == 0)
                    {
                        if (cozyCamera != null)
                        {
                            Vector3 screenPoint = cozyCamera.WorldToScreenPoint(cameraPosition - moonDirection * 10000);

                            if (screenPoint.z > 0)
                                moonFlareOcclusion = Mathf.Lerp(moonFlareOcclusion, FlareOcclusion.GetFlareOcclusion(screenPoint, moonFlareEntityID), 0.9f * Time.deltaTime);
                        }
                    }

                    moonLensFlare.intensity = parameters.lightingMoonLightIntensity * parameters.lightingMoonLensFlareIntensity * moonPhasePercentage * moonFlareOcclusion;

                    if (moonLensFlare.lensFlareData != parameters.lightingMoonLensFlareReference)
                        moonLensFlare.lensFlareData = parameters.lightingMoonLensFlareReference;
                    moonLensFlare.scale = parameters.lightingMoonLensFlareScale * moonPhasePercentage;
                }
                else
                {
                    moonLensFlare.intensity = 0;
                }
            }

            SceneLightingUpdated?.Invoke(sunOn, moonOn);
        }

        public static float GetLightStrength(Light light)
        {
            return light.intensity * (light.color.r + light.color.g + light.color.b);
        }

        RenderTexture MakeRT(int resolution)
        {
            var rt = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.R16)
            {
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            rt.Create();
            return rt;
        }

        void RenderCloudShadowCookie(Vector3 cameraPosition, Quaternion sunLightRotation)
        {
            float cookieSize = parameters.lightingSunCloudShadowsSize;
            UpdateCookie((int)parameters.lightingSunCloudShadowsResolution.value, cookieSize);

            shadowShader.SetVector(LightDirectionID, -sunDirection);
            shadowShader.SetVector(CameraPositionID, cameraPosition);
            shadowShader.SetVector(CookieSizeID, new Vector4(cookieSize, cookieSize, 1, 1));
            shadowShader.SetFloat(CloudTimeID, cloudNoiseTime);
            shadowShader.SetFloat(ShadowStrengthID, parameters.lightingSunCloudShadowsIntensity);
            shadowShader.SetInt(ShadowStepsID, (int)parameters.lightingSunCloudShadowsSteps);
            shadowShader.SetFloat(BlurRadiusID, parameters.lightingSunCloudShadowsBlurRadius);
            shadowShader.SetVector(LightPositionID, sunLightTransform.position);
            shadowShader.SetVector(LightRightID, sunLightRotation * Vector3.right);
            shadowShader.SetVector(LightUpID, sunLightRotation * Vector3.up);
            shadowShader.SetVector(CozyShaderIDs.baseWeatherCloudWorldOffset, cloudWorldOffset);

            int groups = Mathf.CeilToInt(parameters.lightingSunCloudShadowsResolution / 8.0f);
            shadowShader.SetTexture(mainKernel, RawTexID, rawTexture);
            shadowShader.Dispatch(mainKernel, groups, groups, 1);

            shadowShader.SetTexture(blurKernel, RawTexReadID, rawTexture);
            shadowShader.SetTexture(blurKernel, ResultID, cookieTexture);
            shadowShader.Dispatch(blurKernel, groups, groups, 1);

            if (!sunCookieApplied)
            {
                sunLight.cookie = cookieTexture;
                sunCookieApplied = true;
            }

            if (cookieSize != lastCookieSize)
            {
                if (!sunLightData)
                    SetupReferences();

                sunLightData.lightCookieSize = new Vector2(cookieSize, cookieSize);
                lastCookieSize = cookieSize;
            }
        }

        void UpdateCookie(int resolution, float cookieSize)
        {
            if (cookieTexture == null || lastResolution != resolution)
                InitCookie(resolution, cookieSize);
        }

        void InitCookie(int resolution, float cookieSize)
        {
            ReleaseTextures();

            cookieTexture = MakeRT(resolution);
            rawTexture = MakeRT(resolution);
            lastResolution = resolution;

            mainKernel = shadowShader.FindKernel("CSMain");
            blurKernel = shadowShader.FindKernel("CSBlur");

            sunLight.cookie = cookieTexture;
            sunLight.cookieSize2D = new Vector2(cookieSize, cookieSize);
            sunCookieApplied = true;
        }

        void ReleaseTextures()
        {
            if (cookieTexture != null) { cookieTexture.Release(); cookieTexture = null; }
            if (rawTexture != null) { rawTexture.Release(); rawTexture = null; }
            sunCookieApplied = false;
        }

        public override void DeinitializeModule()
        {
            base.DeinitializeModule();
            ReleaseTextures();
        }
    }

}