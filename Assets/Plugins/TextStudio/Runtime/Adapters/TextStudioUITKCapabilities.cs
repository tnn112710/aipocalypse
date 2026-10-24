using System;
using UnityEngine.UIElements;

namespace TextStudio
{
    /// <summary>Reports a UI Toolkit binding's supported feature level as unavailable, basic text output, or full Text Studio parity.</summary>
    public enum TextStudioUITKFeatureSet
    {
        None = 0,
        Basic = 1,
        FullParity = 2
    }

    /// <summary>Chooses UI Toolkit's standard text generator, prefers its advanced generator, or follows the project default.</summary>
    public enum TextStudioUITKTextGeneratorPreference
    {
        ProjectDefault = 0,
        PreferAdvanced = 1,
        Standard = 2
    }

    /// <summary>
    /// Selects whether a UI Toolkit binding owns its playback clock or is stepped by its host.
    /// </summary>
    public enum TextStudioUITKUpdateMode
    {
        /// <summary>Advance through the attached element's UI Toolkit scheduler while work is active.</summary>
        Automatic = 0,
        /// <summary>Advance only when the host calls the binding update method.</summary>
        Manual = 1
    }

    /// <summary>Reports UI Toolkit animation and text-generator capabilities for the active Unity version.</summary>
    #if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class TextStudioUITKCapabilities
    {
        // Editor-owned capability resolver; its owner releases it before reload and shutdown.
        internal static Func<IPanel, bool?> AdvancedTextAvailabilityResolver = null;

        /// <summary>Reports known panel availability, or null when the host cannot query Unity's project configuration.</summary>
        public static bool? GetAdvancedTextAvailability(IPanel panel)
        {
#if UNITY_6000_2_OR_NEWER
            return panel != null ? AdvancedTextAvailabilityResolver?.Invoke(panel) : null;
#else
            return false;
#endif
        }

        public static bool BasicAvailable => true;

#if UNITY_6000_2_OR_NEWER
        public static bool FullParityAvailable => true;
        public static bool TextGeneratorPreferenceAvailable => true;
        public static TextStudioUITKFeatureSet CurrentFeatureSet => TextStudioUITKFeatureSet.FullParity;
#else
        public static bool FullParityAvailable => false;
        public static bool TextGeneratorPreferenceAvailable => false;
        public static TextStudioUITKFeatureSet CurrentFeatureSet => TextStudioUITKFeatureSet.Basic;
#endif

#if UNITY_6000_5_OR_NEWER
        public static bool AdvancedTextGeneratorDefaultInRuntime => true;
#else
        public static bool AdvancedTextGeneratorDefaultInRuntime => false;
#endif

        public static TextStudioUITKTextGeneratorPreference DefaultTextGeneratorPreference =>
            AdvancedTextGeneratorDefaultInRuntime
                ? TextStudioUITKTextGeneratorPreference.PreferAdvanced
                : TextStudioUITKTextGeneratorPreference.ProjectDefault;
    }
}
