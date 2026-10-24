using ImpossibleRobert.Common.Editor.Packages;
using UnityEditor;

namespace TextStudio.Editor
{
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioIntegrationWindowRegistration
    {
        static TextStudioIntegrationWindowRegistration()
        {
            IntegrationWindowRegistry.Register("TextStudio.Integration.LEDStudio", () =>
            {
                TextStudioIntegrationsWindow window = EditorWindow.GetWindow<TextStudioIntegrationsWindow>();
                window.FocusIntegration("led-studio");
                TextStudioIntegrationsWindow.ShowWindow();
            });
        }
    }
}
