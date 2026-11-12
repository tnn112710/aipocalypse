//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;

#if COZY_GIGALIGHTS
using TND.GigaLights;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif
using UnityEngine;

namespace DistantLands.Cozy
{
    [Serializable]
    [CozyIntegration(true, false, "GigaLights", "COZY Icons/GigaLights", "https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/gigalights-infinite-lights-for-unity-397894?aid=1011luj9X")]
    public class CozyGigaLightsIntegration : CozyIntegration
    {
        [Range(0, 2), Tooltip("Scales the intensity of the COZY sun and moon in GigaLights.")]
        public float lightBrightnessMultiplier = 1;
        [Range(0, 2), Tooltip("Scales the fog contribution of the COZY sun and moon in GigaLights.")]
        public float fogDensityMultiplier = 1;
#if COZY_GIGALIGHTS
        private GigaLight sunLight;
        private GigaLight moonLight;
        private CozyAtmosphereModule atmosphereModule;
        private Light sourceSun;
        private Light sourceMoon;
        private UniversalAdditionalLightData sunLightData;
        private UniversalAdditionalLightData moonLightData;
        private bool previousLightOverride;
        private RenderTexture sunCookie;
        private RenderTexture moonCookie;
        private static readonly int CookieSourceID = Shader.PropertyToID("_CookieSource");
        private static readonly int CookieExpandedID = Shader.PropertyToID("_CookieExpanded");
        private static readonly int CookieUseAlphaID = Shader.PropertyToID("_CookieUseAlpha");
#endif

        public override bool PackageInstalled
        {
            get
            {
#if COZY_GIGALIGHTS
                return true;
#else
                return false;
#endif
            }
        }
        public override StatusType CheckStatus(out string message)
        {
            message = "";
#if COZY_GIGALIGHTS
            CozyWeather currentWeather = CozyWeather.Instance;
            if (!currentWeather || !currentWeather.isActiveAndEnabled)
            {
                message = "No active COZY instance in the current scene.";
                return StatusType.Fail;
            }
            if (!currentWeather.GetModule(out CozyAtmosphereModule module) || !module ||
                !module.isActiveAndEnabled || !module.Active || module.IsBiomeModule)
            {
                message = "GigaLights integration requires an active COZY atmosphere module.";
                return StatusType.Fail;
            }
            if (!module.SunLight || !module.MoonLight)
            {
                message = "The COZY atmosphere module requires sun and moon lights.";
                return StatusType.Fail;
            }
            if (!module.SunLight.gameObject.activeInHierarchy || !module.MoonLight.gameObject.activeInHierarchy)
            {
                message = "The COZY sun and moon light objects must be active.";
                return StatusType.Fail;
            }
            GigaLight existingSun = module.SunLight.GetComponent<GigaLight>();
            GigaLight existingMoon = module.MoonLight.GetComponent<GigaLight>();
            if ((existingSun && existingSun.isActiveAndEnabled) || (existingMoon && existingMoon.isActiveAndEnabled))
            {
                message = "Disable existing GigaLight components on the COZY sun and moon to avoid duplicate lighting.";
                return StatusType.Fail;
            }
            if (!CozyPreferences.HandleSceneLighting)
            {
                message = "Enable Handle Scene Lighting in COZY preferences to update the GigaLights.";
                return StatusType.Fail;
            }
            if ((RequiresCookieExpansion(module.SunLight.cookie) || RequiresCookieExpansion(module.MoonLight.cookie)) &&
                (!module.shadowShader || !SystemInfo.supportsComputeShaders || !module.shadowShader.HasKernel("CSExpandCookie")))
            {
                message = "The COZY cloud shadow compute shader with CSExpandCookie and compute shader support are required for single-channel cookies.";
                return StatusType.Fail;
            }
            if (!GigaLightsRuntime.settings.enabled)
            {
                message = "Enable GigaLights in its global settings.";
                return StatusType.Fail;
            }
            return StatusType.Pass;
#else
            message = "GigaLights is not installed.";
            return StatusType.Fail;
#endif
        }

        public override bool IntegrationInstalled => true;

        public override void Initialize()
        {
#if COZY_GIGALIGHTS
            CozyWeather.OnCozyUpdateLoop -= UpdateIntegration;
            CozyWeather.OnCozyUpdateLoop += UpdateIntegration;
#endif
        }

        private void UpdateIntegration()
        {
#if COZY_GIGALIGHTS
            if (!ActiveAndEnabled || CheckStatus() > StatusType.Warning)
            {
                ReleaseLights();
                return;
            }

            CozyWeather.Instance.GetModule(out CozyAtmosphereModule module);
            if (atmosphereModule != module || sourceSun != module.SunLight ||
                sourceMoon != module.MoonLight || !sunLight || !moonLight)
            {
                ReleaseLights();
                atmosphereModule = module;
                sourceSun = module.SunLight;
                sourceMoon = module.MoonLight;
                sunLightData = sourceSun.GetComponent<UniversalAdditionalLightData>();
                moonLightData = sourceMoon.GetComponent<UniversalAdditionalLightData>();
                sunLight = CreateLight(sourceSun, "GigaLights Sun");
                moonLight = CreateLight(sourceMoon, "GigaLights Moon");
                previousLightOverride = atmosphereModule.OverrideSceneLights;
                atmosphereModule.OverrideSceneLights = true;
                atmosphereModule.SceneLightingUpdated += SynchronizeLights;
            }
            else if (!atmosphereModule.OverrideSceneLights)
                atmosphereModule.OverrideSceneLights = true;
#endif
        }

        public override void Deinitialize()
        {
#if COZY_GIGALIGHTS
            CozyWeather.OnCozyUpdateLoop -= UpdateIntegration;
            ReleaseLights();
#endif
        }

        public override void ApplyIntegration()
        {

        }

        public override void UnapplyIntegration()
        {

        }

#if COZY_GIGALIGHTS
        private static GigaLight CreateLight(Light source, string name)
        {
            var lightObject = new GameObject(name);
            lightObject.hideFlags = HideFlags.DontSave;
            lightObject.SetActive(false);
            lightObject.transform.SetParent(source.transform, false);
            GigaLight light = lightObject.AddComponent<GigaLight>();
            light.type = GigaLightType.Directional;
            light.enabled = false;
            lightObject.SetActive(true);
            return light;
        }

        private void SynchronizeLights(bool sunOn, bool moonOn)
        {
            if (!sourceSun || !sourceMoon)
            {
                ReleaseLights();
                return;
            }
            if ((RequiresCookieExpansion(sourceSun.cookie) || RequiresCookieExpansion(sourceMoon.cookie)) &&
                (!atmosphereModule.shadowShader || !SystemInfo.supportsComputeShaders ||
                 !atmosphereModule.shadowShader.HasKernel("CSExpandCookie")))
            {
                Debug.LogError("COZY GigaLights integration requires the cloud shadow compute shader with CSExpandCookie and compute shader support for single-channel cookies.");
                ReleaseLights();
                return;
            }
            SynchronizeLight(sunLight, sourceSun, sunLightData, sunOn, ref sunCookie);
            SynchronizeLight(moonLight, sourceMoon, moonLightData, moonOn, ref moonCookie);
        }

        private void SynchronizeLight(GigaLight target, Light source, UniversalAdditionalLightData additionalData, bool lightOn, ref RenderTexture expandedCookie)
        {
            if (!target || !source) return;

            float intensity = source.intensity * Mathf.Max(0, lightBrightnessMultiplier);
            Vector2 cookieSize = additionalData ? additionalData.lightCookieSize : source.cookieSize2D;
            Vector2 cookieOffset = additionalData ? additionalData.lightCookieOffset : Vector2.zero;
            GigaLightFeatures features = GigaLightFeatures.Light | GigaLightFeatures.Fog;
            if (source.shadows != LightShadows.None) features |= GigaLightFeatures.Shadows;

            if (target.color != source.color) target.color = source.color;
            if (target.useColorTemperature != source.useColorTemperature) target.useColorTemperature = source.useColorTemperature;
            if (target.colorTemperature != source.colorTemperature) target.colorTemperature = source.colorTemperature;
            if (target.intensity != intensity) target.intensity = intensity;
            Texture cookie = ExpandCookie(source.cookie, ref expandedCookie);
            if (!ReferenceEquals(target.cookie, cookie)) target.cookie = cookie;
            if (target.cookieSize != cookieSize) target.cookieSize = cookieSize;
            if (target.cookieOffset != cookieOffset) target.cookieOffset = cookieOffset;
            if (target.features != features) target.features = features;
            if (target.shadowStrength != source.shadowStrength) target.shadowStrength = source.shadowStrength;
            float shadowSoftness = source.shadows == LightShadows.Hard ? 0f : 0.05f;
            if (target.shadowSoftness != shadowSoftness) target.shadowSoftness = shadowSoftness;
            if (target.cullingMask != source.cullingMask) target.cullingMask = source.cullingMask;
            uint renderingLayerMask = (uint)source.renderingLayerMask;
            if (target.renderingLayerMask != renderingLayerMask) target.renderingLayerMask = renderingLayerMask;
            float fogContribution = Mathf.Clamp(fogDensityMultiplier, 0, 8);
            if (target.fogContribution != fogContribution) target.fogContribution = fogContribution;
            if (target.enabled != lightOn) target.enabled = lightOn;
        }

        private static bool RequiresCookieExpansion(Texture cookie)
        {
            return cookie && cookie.dimension == TextureDimension.Tex2D &&
                GraphicsFormatUtility.GetComponentCount(cookie.graphicsFormat) == 1;
        }

        private Texture ExpandCookie(Texture source, ref RenderTexture expanded)
        {
            if (!RequiresCookieExpansion(source))
            {
                ReleaseCookie(ref expanded);
                return source;
            }

            if (!expanded || expanded.width != source.width || expanded.height != source.height)
            {
                ReleaseCookie(ref expanded);
                expanded = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    name = "COZY GigaLights Cookie",
                    hideFlags = HideFlags.DontSave,
                    enableRandomWrite = true,
                    wrapMode = source.wrapMode,
                    filterMode = source.filterMode
                };
                if (!expanded.Create())
                    throw new InvalidOperationException("COZY GigaLights could not create the expanded cookie texture.");
            }
            else if (!expanded.IsCreated())
            {
                if (!expanded.Create())
                    throw new InvalidOperationException("COZY GigaLights could not recreate the expanded cookie texture.");
            }

            ComputeShader shader = atmosphereModule.shadowShader;
            int kernel = shader.FindKernel("CSExpandCookie");
            shader.SetTexture(kernel, CookieSourceID, source);
            shader.SetTexture(kernel, CookieExpandedID, expanded);
            shader.SetInt(CookieUseAlphaID, GraphicsFormatUtility.IsAlphaOnlyFormat(source.graphicsFormat) ? 1 : 0);
            shader.Dispatch(kernel, Mathf.CeilToInt(source.width / 8f), Mathf.CeilToInt(source.height / 8f), 1);
            return expanded;
        }

        private static void ReleaseCookie(ref RenderTexture cookie)
        {
            if (!cookie) return;
            cookie.Release();
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(cookie);
            else
                UnityEngine.Object.DestroyImmediate(cookie);
            cookie = null;
        }

        private void ReleaseLights()
        {
            if (atmosphereModule)
            {
                atmosphereModule.SceneLightingUpdated -= SynchronizeLights;
                atmosphereModule.OverrideSceneLights = previousLightOverride;
            }

            DestroyLight(sunLight);
            DestroyLight(moonLight);
            ReleaseCookie(ref sunCookie);
            ReleaseCookie(ref moonCookie);
            atmosphereModule = null;
            sourceSun = null;
            sourceMoon = null;
            sunLightData = null;
            moonLightData = null;
            sunLight = null;
            moonLight = null;
        }

        private static void DestroyLight(GigaLight light)
        {
            if (!light) return;
            light.gameObject.SetActive(false);
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(light.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(light.gameObject);
        }
#endif
    }
}