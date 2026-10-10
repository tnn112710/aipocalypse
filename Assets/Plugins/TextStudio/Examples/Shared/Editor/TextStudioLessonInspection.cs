using TextStudio.Examples.Tutorials;
using UnityEditor;
using Object = UnityEngine.Object;

namespace TextStudio.Examples.Editor
{
    /// <summary>Connects saved lesson Inspect controls to Unity without a runtime editor dependency.</summary>
    [InitializeOnLoad]
    internal static class TextStudioLessonInspection
    {
        static TextStudioLessonInspection()
        {
            Connect();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Disconnect;
            EditorApplication.quitting += Disconnect;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode) Connect();
        }

        static void Connect()
        {
            TextStudioLessonActions.InspectionRequested -= Inspect;
            TextStudioLessonActions.InspectionRequested += Inspect;
        }

        static void Disconnect() => TextStudioLessonActions.InspectionRequested -= Inspect;

        static void Inspect(Object asset)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
