using UnityEditor;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    /// <summary>Reads the panel's serialized build capability without exposing UnityEditor to the runtime assembly.</summary>
    [InitializeOnLoad]
    internal static class TextStudioUITKAvailabilityBridge
    {
        static TextStudioUITKAvailabilityBridge()
        {
            TextStudioUITKCapabilities.AdvancedTextAvailabilityResolver = Query;
            AssemblyReloadEvents.beforeAssemblyReload += Release;
            EditorApplication.quitting += Release;
        }
        static void Release()
        {
            if (TextStudioUITKCapabilities.AdvancedTextAvailabilityResolver == Query)
                TextStudioUITKCapabilities.AdvancedTextAvailabilityResolver = null;
        }
        static bool? Query(IPanel panel)
        {
#if UNITY_6000_2_OR_NEWER
            if (panel.contextType == ContextType.Editor) return true;
            PanelSettings panelSettings = (panel as IRuntimePanel)?.panelSettings;
            if (panelSettings == null) return null;
            // Unity serializes the same ICU payload it uses to select the player text generator.
            using SerializedObject settings = new SerializedObject(panelSettings);
            SerializedProperty payload = settings.FindProperty("m_ICUDataAsset");
            return payload != null ? payload.objectReferenceValue != null : (bool?)null;
#else
            return null;
#endif
        }
    }
}
