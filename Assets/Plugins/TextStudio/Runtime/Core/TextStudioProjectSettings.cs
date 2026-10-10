using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Optional user-created project-level settings. Ships zero assets by default; a user
    /// can create one via Assets → Create → Text Studio → Project Settings to customize the
    /// hard-coded Text Studio default settings and to register project assets by id.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    public sealed partial class TextStudioProjectSettings : ScriptableObject
    {
        internal const string ResourcePath = "TextStudio/TextStudioProjectSettings";
        internal const string CanonicalAssetPath =
            "Assets/Text Studio/Resources/TextStudio/TextStudioProjectSettings.asset";

#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly ProfilerMarker s_bootstrapMarker =
            new ProfilerMarker("TextStudio.RuntimeBootstrap");

        public bool overrideDefaults;
        public RevealMode defaultRevealMode = RevealMode.ByGlyph;
        public PlacementMode defaultPlacementMode = PlacementMode.Linear;
        [Min(0f)]
        public float defaultCharDelay = 0.03f;
        public bool warnOnMissingTags = true;
        [Tooltip("Package-wide animation policy. Reduced limits motion while preserving color and material emphasis; None removes motion channels.")]
        public TextStudioMotionPolicy motionPolicy = TextStudioMotionPolicy.Full;
        [Tooltip("Optional authoring syntax override. Leave unassigned for angle tags plus supported Text Animator brace tags.")]
        public TextStudioMarkupSyntax markupSyntax;

        public List<TextStudioEffectDefinitionAsset> effectCatalog = new List<TextStudioEffectDefinitionAsset>();
        public List<TextStudio2DStyleAsset> appearanceStyleCatalog = new List<TextStudio2DStyleAsset>();
        public List<TextStudioActionAsset> actionCatalog = new List<TextStudioActionAsset>();
        public List<TextStudioCurveProfile> curveCatalog = new List<TextStudioCurveProfile>();
        public List<EffectPlaybackAsset> playbackCatalog = new List<EffectPlaybackAsset>();
        public List<TextStudioTagExpansionSheet> tagExpansionSheets = new List<TextStudioTagExpansionSheet>();

        static TextStudioProjectSettings s_active = null;
        static int s_activationCount = 0;
        static int s_bootstrapActivationCount = 0;
        static double s_lastBootstrapMilliseconds = 0d;
        static int s_lastEffectCatalogCount = 0;
        static int s_lastActionCatalogCount = 0;
        static int s_lastCurveCatalogCount = 0;
        static int s_lastPlaybackCatalogCount = 0;
        static int s_lastTagExpansionSheetCount = 0;

        public static TextStudioProjectSettings Active => s_active;
        public static TextStudioMarkupSyntax ActiveMarkupSyntax => s_active != null ? s_active.markupSyntax : null;
        internal static int ActivationCount => s_activationCount;
        internal static int BootstrapActivationCount => s_bootstrapActivationCount;
        internal static double LastBootstrapMilliseconds => s_lastBootstrapMilliseconds;
        internal static int LastEffectCatalogCount => s_lastEffectCatalogCount;
        internal static int LastActionCatalogCount => s_lastActionCatalogCount;
        internal static int LastCurveCatalogCount => s_lastCurveCatalogCount;
        internal static int LastPlaybackCatalogCount => s_lastPlaybackCatalogCount;
        internal static int LastTagExpansionSheetCount =>
            s_lastTagExpansionSheetCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntimeSession()
        {
            s_active = null;
            s_activationCount = 0;
            s_bootstrapActivationCount = 0;
            s_lastBootstrapMilliseconds = 0d;
            s_lastEffectCatalogCount = 0;
            s_lastActionCatalogCount = 0;
            s_lastCurveCatalogCount = 0;
            s_lastPlaybackCatalogCount = 0;
            s_lastTagExpansionSheetCount = 0;
            TextStudioMotionSettings.GlobalPolicy = TextStudioMotionPolicy.Full;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BootstrapRuntimeSettings()
        {
            long started = Stopwatch.GetTimestamp();
            using (s_bootstrapMarker.Auto())
            {
                TextStudioProjectSettings settings =
                    Resources.Load<TextStudioProjectSettings>(ResourcePath);
                s_bootstrapActivationCount++;
                SetActive(settings);
            }

            long elapsed = Stopwatch.GetTimestamp() - started;
            s_lastBootstrapMilliseconds =
                elapsed * 1000d / Stopwatch.Frequency;
        }

        /// <summary>
        /// Make a project settings asset active and atomically replace the project-owned
        /// catalog tier. Explicit runtime registrations remain in place and take precedence.
        /// Passing null clears the previous project catalog and restores built-in defaults.
        /// </summary>
        public static void SetActive(TextStudioProjectSettings settings)
        {
            TextStudioRegistry.BeginProjectCatalogActivation();
            try
            {
                TextStudioRegistry.ReplaceProjectCatalog(settings);
                s_active = settings;
                TextStudioMotionSettings.GlobalPolicy = settings != null
                    ? settings.motionPolicy
                    : TextStudioMotionPolicy.Full;
                TextStudioDefaultSettings.Refresh();
                s_lastEffectCatalogCount = Count(settings?.effectCatalog);
                s_lastActionCatalogCount = Count(settings?.actionCatalog);
                s_lastCurveCatalogCount = Count(settings?.curveCatalog);
                s_lastPlaybackCatalogCount = Count(settings?.playbackCatalog);
                s_lastTagExpansionSheetCount =
                    Count(settings?.tagExpansionSheets);
                s_activationCount++;
            }
            finally
            {
                TextStudioRegistry.NotifyProjectCatalogChanged();
            }
        }

        /// <summary>
        /// Applies this settings asset's values onto the given default settings instance (if enabled).
        /// </summary>
        public static void ApplyTo(TextStudioDefaultSettings defaultSettings)
        {
            if (s_active == null || !s_active.overrideDefaults) return;
            defaultSettings.defaultRevealMode = s_active.defaultRevealMode;
            defaultSettings.defaultPlacementMode = s_active.defaultPlacementMode;
            defaultSettings.defaultCharDelay = Mathf.Max(0f, s_active.defaultCharDelay);
            defaultSettings.warnOnMissingTags = s_active.warnOnMissingTags;
        }

        /// <summary>
        /// Replaces the project-owned registry tier with this asset's catalog. Prefer
        /// <see cref="SetActive"/> when changing the active project settings.
        /// </summary>
        public void RegisterCatalog()
        {
            TextStudioRegistry.ReplaceProjectCatalog(this);
        }

        static int Count<T>(IReadOnlyCollection<T> entries)
        {
            return entries != null ? entries.Count : 0;
        }
    }
}
