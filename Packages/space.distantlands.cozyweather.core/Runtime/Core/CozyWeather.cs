//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using System;
using UnityEngine.SceneManagement;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy
{
    /// <summary>
    /// Main controller for COZY 4.
    /// Only controls references (camera, anchor, modules), execution, and FX. Everything else is handled in the modules
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    [HelpURL("https://distant-lands.gitbook.io/cozy-weather-4")]
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public class CozyWeather : CozySystem
    {
        #region References
        public Camera CozyCamera
        {
            get
            {
                return cozyCamera ? cozyCamera : Camera.main;
            }
            set
            {
                cozyCamera = value;
            }
        }

        private Camera cozyCamera;

        private static int seed;
        public static int Seed => seed;
        #endregion

        #region Modules

        private IClimateModule _climate;
        public IClimateModule Climate => Get(ref _climate);
        private IPrecipitationModule _precipitation;
        public IPrecipitationModule Precipitation => Get(ref _precipitation);
        private IWeatherModule _weather;
        public IWeatherModule Weather => Get(ref _weather);
        private ISeasonModule _seasons;
        public ISeasonModule Seasons => Get(ref _seasons);

        private ITimeModule _time;
        public ITimeModule Time => Get(ref _time);
        private ICalendarModule _calendar;
        public ICalendarModule Calendar => Get(ref _calendar);
        private IAtmosphereModule _atmosphere;
        public IAtmosphereModule Atmosphere => Get(ref _atmosphere);
        private IWindModule _wind;
        public IWindModule Wind => Get(ref _wind);
        private IAmbienceModule _ambience;
        public IAmbienceModule Ambience => Get(ref _ambience);

        #endregion

        #region Execution
        private event Action onFrameReset;
        private event Action onUpdateWeatherWeights;
        private event Action onSendToGPU;
        private event Action onCozyUpdateLoop;

        public static event Action OnFrameReset
        {
            add { if (Instance != null) Instance.onFrameReset += value; }
            remove { if (Instance != null) Instance.onFrameReset -= value; }
        }

        public static event Action OnUpdateWeatherWeights
        {
            add { if (Instance != null) Instance.onUpdateWeatherWeights += value; }
            remove { if (Instance != null) Instance.onUpdateWeatherWeights -= value; }
        }

        public static event Action OnSendToGPU
        {
            add { if (Instance != null) Instance.onSendToGPU += value; }
            remove { if (Instance != null) Instance.onSendToGPU -= value; }
        }

        public static event Action OnCozyUpdateLoop
        {
            add { if (Instance != null) Instance.onCozyUpdateLoop += value; }
            remove { if (Instance != null) Instance.onCozyUpdateLoop -= value; }
        }

        private void RaiseOnFrameReset() => onFrameReset?.Invoke();
        private void RaiseUpdateWeatherWeights() => onUpdateWeatherWeights?.Invoke();
        private void RaiseSendToGPU() => onSendToGPU?.Invoke();
        private void RaiseCozyUpdateLoop() => onCozyUpdateLoop?.Invoke();

        #endregion

        #region Providers
        private IMaterialProvider materialProvider;
        public IMaterialProvider MaterialProvider
        {
            get
            {
                if (materialProvider == null)
                {
                    materialProvider = new CozyMaterialProvider();
                    materialProvider.UpdateKeywords();
                }

                return materialProvider;
            }
        }
        private IFXProvider fXProvider;
        public IFXProvider FXProvider
        {
            get
            {
                if (fXProvider == null)
                    fXProvider = new CozyFXProvider();

                return fXProvider;
            }
        }
        public IVFXProvider VFXProvider
        {
            get
            {
                if (vfXProvider == null)
                {
                    var vfx = new CozyVFXProvider();
                    vfXProvider = vfx;
                    vfXBlockingProvider = vfx;
                    vfXBlockingProvider.UpdateVFXBlocking();
                }

                return vfXProvider;
            }
        }
        private IVFXProvider vfXProvider;
        public IVFXBlockingProvider VFXBlockingProvider
        {
            get
            {
                if (vfXBlockingProvider == null)
                {
                    var vfx = new CozyVFXProvider();
                    vfXProvider = vfx;
                    vfXBlockingProvider = vfx;
                    vfXBlockingProvider.UpdateVFXBlocking();
                }

                return vfXBlockingProvider;
            }
        }
        private IVFXBlockingProvider vfXBlockingProvider;
        #endregion

#if UNITY_6000_5_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        static void InitializeProvidersOnLoad()
        {
            if (Instance != null)
                Instance.SetupProviders();
        }

        public void SetupProviders()
        {
            //Clean up existing providers
            vfXProvider?.Deinitialize();

            // Setup fx provider
            fXProvider = new CozyFXProvider();

            // Setup VFX provider and VFX blocking provider
            var vfx = new CozyVFXProvider();
            vfXProvider = vfx;
            vfXBlockingProvider = vfx;
            vfXBlockingProvider.UpdateVFXBlocking();

            // Setup material provider
            materialProvider = new CozyMaterialProvider();
            materialProvider.UpdateKeywords();
        }

        private void OnEnable()
        {
            if (cachedInstance != this && cachedInstance != null)
            {
                Debug.LogError($"Only one instance of COZY is allowed at a time. Destroying this instance in favor of {Instance.name}");

                if (Application.isPlaying)
                    Destroy(gameObject);
                else
                    DestroyImmediate(gameObject);
                return;
            }


            cachedInstance = this;

            SceneManager.sceneLoaded += UpdateOnSceneLoaded;
            SceneManager.sceneUnloaded += UpdateOnSceneUnLoaded;

            CozyShaderIDs.Initialize();
            CozyIntegrations.Initialize();

            if (Application.isPlaying)
            {
                if (CozyPreferences.DontDestroyWeatherOnLoad)
                    DontDestroyOnLoad(this);
            }
        }

        private void OnDisable()
        {
            if (cachedInstance != this) return;

            cachedInstance = null;

            SceneManager.sceneLoaded -= UpdateOnSceneLoaded;
            SceneManager.sceneUnloaded -= UpdateOnSceneUnLoaded;

            CozyIntegrations.Deinitialize();

            vfXProvider?.Deinitialize();
        }

        private void Awake()
        {
            ResetQuality();
            ResetModules();

            Priority = -9999;
            TargetWeight = 1;
        }

        /// <summary>
        /// Resets all of the material shaders to the shaders in the atmosphere profiles.
        /// </summary> 
        public void ResetQuality()
        {
            CozyShaderIDs.Initialize();
            SetupProviders();
        }

        // Update is called once per frame
        void Update()
        {

            // Reset FX parameters before executing modules
            fXProvider?.ResetFXParameters();

            RaiseOnFrameReset();
            RaiseUpdateWeatherWeights();
            RaiseCozyUpdateLoop();
            fXProvider?.PassFXVariables();
            RaiseSendToGPU();

            if (!Application.isPlaying) return;

            vfXProvider?.Center();
            vfXBlockingProvider?.UpdateVFXBlocking();

        }

        void UpdateOnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var module in ActiveAndEnabledModules)
            {
                module.OnSceneLoaded(mode);
            }
        }

        void UpdateOnSceneUnLoaded(Scene scene)
        {
            foreach (var module in ActiveAndEnabledModules)
            {
                module.OnSceneUnloaded();
            }
        }

        #region Singleton
        /// <summary>
        /// Get the current instance of Cozy in the scene. Returns null if no weather sphere is found.
        /// </summary> 
        public static CozyWeather Instance
        {
            get
            {
                if (cachedInstance)
                    return cachedInstance;

                cachedInstance = FindAnyObjectByType<CozyWeather>();
                return cachedInstance;
            }
        }
        [Obsolete]
        public static CozyWeather instance => Instance;

        private static CozyWeather cachedInstance;

        public static bool Active => Instance != null && Instance.isActiveAndEnabled;
        #endregion
    }
}