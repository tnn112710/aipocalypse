using TextStudio.Examples.Tutorials;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TextStudio.Examples.Editor
{
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    // The editor owns navigation registrations and resets the in-flight guard on Play transitions.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioCourseNavigation
    {
        static bool _opening;

        static TextStudioCourseNavigation()
        {
            Connect();
            EditorApplication.playModeStateChanged += OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload += Disconnect;
            EditorApplication.quitting += Disconnect;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            _opening = false;
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode) Connect();
        }

        static void Connect()
        {
            TextStudioCourseDestination.EditorOpenRequested -= Open;
            TextStudioCourseDestination.EditorOpenRequested += Open;
        }

        static void Disconnect()
        {
            TextStudioCourseDestination.EditorOpenRequested -= Open;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload -= Disconnect;
            EditorApplication.quitting -= Disconnect;
            _opening = false;
        }

        static void Open(string guid)
        {
            if (_opening) return;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) { Debug.LogWarning("The selected course scene is no longer installed."); return; }
            if (EditorApplication.isPlaying)
            {
                _opening = true;
                AsyncOperation operation = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
                operation.completed += _ => _opening = false;
            }
            else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }
    }
}
