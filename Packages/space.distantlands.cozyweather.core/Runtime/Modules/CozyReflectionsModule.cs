//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
#if COZY_URP
using UnityEngine.Rendering.Universal;
#endif

namespace DistantLands.Cozy
{
    [ExecuteAlways]
    [AddComponentMenu("")]
    [CozyModule(name = "Reflections", icon = "Reflection", tooltip = "Sets up a cubemap for reflections with COZY.", category = "Atmosphere")]
    public class CozyReflectionsModule : CozyModule
    {
        public enum UpdateFrequency { everyFrame, onAwake, onHour, viaScripting }

        [CozySearchable("Reflection")]
        public UpdateFrequency updateFrequency;

        [Tooltip("Resolution (per face) of the managed reflection cubemap.")]
        [CozySearchable]
        public int resolution = 256;

        [Tooltip("Render the cubemap in HDR. Recommended if your scene/skybox uses HDR lighting.")]
        [CozySearchable]
        public bool hdr = true;

        [Tooltip("Generate mipmaps for the cubemap so materials can sample blurred reflections for rough surfaces.")]
        [CozySearchable]
        public bool useMipMaps = true;

        private RenderTexture reflectionCubemap;

        public Camera reflectionCamera;

        [Tooltip("How many frames should pass before the cubemap renders again? A value of 0 renders every frame and a value of 30 renders once every 30 frames.")]
        [Range(0, 30)]
        [CozySearchable]
        public int framesBetweenRenders = 10;

        [Tooltip("What layers should be rendered into the skybox reflections?")]
        [CozySearchable]
        public LayerMask layerMask = 2;

        private int framesLeft;
        public int minimumQualityLevel;

        [Tooltip("Refresh the skybox reflections when the scene loads or unloads.")]
        [CozySearchable]
        public bool refreshOnSceneChange;

#if COZY_URP
        public int rendererOverride;
        public UniversalAdditionalCameraData cameraData;
#endif

        // Cached so we can detect inspector changes at runtime/edit-time and rebuild the RT to match.
        private int cachedResolution;
        private bool cachedHDR;
        private bool cachedMipMaps;
        private ITimeModule timeModule;
        private int pendingHourFrame = -1;

        public override void InitializeModule()
        {

            CreateReflectionTexture();
            RenderSettings.customReflectionTexture = reflectionCubemap;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;

            if (updateFrequency == UpdateFrequency.onAwake || updateFrequency == UpdateFrequency.onHour)
            {
                RenderReflections();
            }

            UpdateTimeSubscription();
        }

        public override void OnCozyUpdateLoop()
        {
            if (WeatherSphere == null)
            {
                base.InitializeModule();
            }
            UpdateTimeSubscription();

            if (!CozyPreferences.UpdateInEditMode && !Application.isPlaying)
            {
                return;
            }

            if (pendingHourFrame >= 0 && Time.frameCount != pendingHourFrame)
            {
                pendingHourFrame = -1;
                if (updateFrequency == UpdateFrequency.onHour)
                    RenderReflections();
            }

            if (reflectionCubemap == null || cachedResolution != resolution || cachedHDR != hdr || cachedMipMaps != useMipMaps)
            {
                CreateReflectionTexture();
                RenderSettings.customReflectionTexture = reflectionCubemap;
            }

            if (updateFrequency == UpdateFrequency.everyFrame)
            {
                if (framesLeft <= 0)
                {
                    RenderReflections();
                    framesLeft = framesBetweenRenders;
                }
                else
                {
                    framesLeft--;
                }
            }
        }

        protected void RefreshReflectionsOnSceneChange()
        {
            if (refreshOnSceneChange)
                RenderReflections();
        }

        public int ToLayer(LayerMask mask)
        {
            int value = mask.value;
            if (value == 0)
            {
                return 0;
            }
            for (int l = 1; l < 32; l++)
            {
                if ((value & (1 << l)) != 0)
                {
                    return l;
                }
            }
            return -1;
        }

        public override void DeinitializeModule()
        {
            if (timeModule != null)
                timeModule.OnNewHour -= OnNewHour;
            timeModule = null;
            pendingHourFrame = -1;

            if (reflectionCamera)
            {
                if (Application.isPlaying)
                    Destroy(reflectionCamera.gameObject);
                else
                    DestroyImmediate(reflectionCamera.gameObject);
            }

            if (RenderSettings.customReflectionTexture == reflectionCubemap)
                RenderSettings.customReflectionTexture = null;

            DisposeReflectionTexture();
        }

        private void UpdateTimeSubscription()
        {
            var time = WeatherSphere != null && WeatherSphere.Time != null ? WeatherSphere.Time : null;
            if (time == timeModule) return;
            if (timeModule != null)
                timeModule.OnNewHour -= OnNewHour;
            timeModule = time;
            if (timeModule != null)
                timeModule.OnNewHour += OnNewHour;
        }

        private void OnNewHour(int oldHour, int newHour)
        {
            if (updateFrequency == UpdateFrequency.onHour)
                pendingHourFrame = Time.frameCount;
        }

        public void RenderReflections()
        {
            if (QualitySettings.GetQualityLevel() < minimumQualityLevel)
                return;

            if (reflectionCubemap == null)
                CreateReflectionTexture();

            if (WeatherSphere == null || !Camera.main)
            {
                return;
            }

            if (reflectionCamera == null)
            {
                SetupCamera();
            }

            Camera sourceCamera = Camera.main;

            reflectionCamera.enabled = true;
            reflectionCamera.transform.position = sourceCamera.transform.position;
            reflectionCamera.nearClipPlane = sourceCamera.nearClipPlane;
            reflectionCamera.farClipPlane = sourceCamera.farClipPlane;
            reflectionCamera.cullingMask = layerMask;
            reflectionCamera.allowHDR = hdr;

            reflectionCamera.RenderToCubemap(reflectionCubemap);

            if (useMipMaps)
            {
                reflectionCubemap.GenerateMips();
            }

            reflectionCamera.enabled = false;
        }

        public void SetupCamera()
        {
            GameObject i = new()
            {
                name = "COZY Reflection Camera",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInHierarchy
            };

            reflectionCamera = i.AddComponent<Camera>();
            reflectionCamera.depth = -50;
            reflectionCamera.enabled = false;
            reflectionCamera.allowHDR = hdr;
            reflectionCamera.useOcclusionCulling = false;

#if COZY_URP
            cameraData = reflectionCamera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
            {
                cameraData.SetRenderer(rendererOverride);
                cameraData.renderPostProcessing = false;
                cameraData.renderShadows = true;
                cameraData.antialiasing = AntialiasingMode.None;
            }
#endif
        }

        /// <summary>
        /// Creates (or recreates) the RenderTexture cubemap COZY uses for reflections,
        /// disposing of any previous texture first.
        /// </summary>
        public void CreateReflectionTexture()
        {
            DisposeReflectionTexture();

            int size = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), 32, 2048);

            reflectionCubemap = new RenderTexture(size, size, 16,
                hdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default)
            {
                name = "COZY Reflection Cubemap",
                dimension = UnityEngine.Rendering.TextureDimension.Cube,
                useMipMap = useMipMaps,
                autoGenerateMips = false, // generated manually after each render, once all faces are updated
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.DontSave
            };
            reflectionCubemap.Create();

            cachedResolution = resolution;
            cachedHDR = hdr;
            cachedMipMaps = useMipMaps;
        }

        /// <summary>
        /// Releases and destroys the managed render texture, if one exists.
        /// </summary>
        public void DisposeReflectionTexture()
        {
            if (reflectionCubemap == null)
                return;

            reflectionCubemap.Release();

            if (Application.isPlaying)
                Destroy(reflectionCubemap);
            else
                DestroyImmediate(reflectionCubemap);

            reflectionCubemap = null;
        }
    }
}