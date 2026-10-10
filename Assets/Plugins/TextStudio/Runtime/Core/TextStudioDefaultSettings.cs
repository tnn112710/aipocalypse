using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Global Text Studio defaults.
    ///
    /// Text Studio does not ship a default settings asset. Users can optionally create one at
    /// Text Studio's canonical Resources path to customize project-wide defaults; otherwise the
    /// hard-coded defaults below are used.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public partial class TextStudioDefaultSettings : ScriptableObject
    {
        [Header("Defaults")]
        [Tooltip("Default reveal mode for new text controllers.")]
        public RevealMode defaultRevealMode = RevealMode.ByGlyph;

        [Tooltip("Default placement mode for new text controllers.")]
        public PlacementMode defaultPlacementMode = PlacementMode.Linear;

        [Header("Timing")]
        [Tooltip("Default per-character reveal delay in seconds.")]
        [Min(0f)]
        public float defaultCharDelay = 0.03f;

        [Header("Diagnostics")]
        [Tooltip("Log warnings when tag/effect lookups fail.")]
        public bool warnOnMissingTags = true;

        [Header("Authoring")]
        [Tooltip("When adding a TextStudioText hub to a GameObject with no TMP_Text, auto-add a TextMeshPro / TextMeshProUGUI sibling.")]
        public bool autoAddTMPOnHubReset = true;

        static TextStudioDefaultSettings s_Instance = null;

        /// <summary>
        /// Global default settings. If a TextStudioProjectSettings asset exists and has been loaded,
        /// its values are applied on top. Otherwise, the hard-coded defaults are used.
        /// </summary>
        public static TextStudioDefaultSettings Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = CreateInstance<TextStudioDefaultSettings>();
                    s_Instance.name = "Text Studio Default Settings (Built-in)";
                    s_Instance.hideFlags = HideFlags.HideAndDontSave;
                    s_Instance.ResetToBuiltInDefaults();
                    TextStudioProjectSettings.ApplyTo(s_Instance);
                }
                return s_Instance;
            }
        }

        /// <summary>
        /// Force the singleton to re-read from the latest project settings. Called by the
        /// TextStudioProjectSettings import callback whenever its asset changes.
        /// </summary>
        public static void Refresh()
        {
            if (s_Instance == null) return;
            s_Instance.ResetToBuiltInDefaults();
            TextStudioProjectSettings.ApplyTo(s_Instance);
        }

        void ResetToBuiltInDefaults()
        {
            defaultRevealMode = RevealMode.ByGlyph;
            defaultPlacementMode = PlacementMode.Linear;
            defaultCharDelay = 0.03f;
            warnOnMissingTags = true;
            autoAddTMPOnHubReset = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetOnDomainReload()
        {
            ReleaseGeneratedInstance(false);
        }

#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnEnteringPlayMode]
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void ReleaseGeneratedInstanceForUnityLifecycle()
        {
            ReleaseGeneratedInstance(true);
        }
#endif

        static void ReleaseGeneratedInstance(bool destroyImmediately)
        {
            TextStudioDefaultSettings instance = s_Instance;
            s_Instance = null;
            if (instance == null)
                return;

#if UNITY_EDITOR
            if (destroyImmediately)
            {
                DestroyImmediate(instance);
                return;
            }
#endif
            if (Application.isPlaying)
                Destroy(instance);
            else
                DestroyImmediate(instance);
        }
    }
}
