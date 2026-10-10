using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>A portable sample destination. Editor resolution uses the saved GUID; players use their scene list.</summary>
    [UnityEngine.AddComponentMenu("")]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    public sealed partial class TextStudioCourseDestination : MonoBehaviour
    {
        public static event Action<string> EditorOpenRequested = null;
        [SerializeField] string _sceneGuid;
        [SerializeField] string _sceneName;
        [SerializeField] TMP_Text _feedback;
        bool _opening;

        public string SceneGuid => _sceneGuid;
        public string SceneName => _sceneName;

#if !UNITY_6000_7_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBridge() => EditorOpenRequested = null;
#endif

        public void Configure(string guid, string sceneName, TMP_Text feedback)
        {
            _sceneGuid = guid; _sceneName = sceneName; _feedback = feedback;
        }

        public void Open()
        {
            if (_opening) return;
            if (EditorOpenRequested != null)
            {
                EditorOpenRequested(_sceneGuid);
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(_sceneName))
            {
                if (_feedback != null) _feedback.text = "This scene is not included in this player. Open the saved sample in Unity.";
                return;
            }
            _opening = true;
            SceneManager.LoadSceneAsync(_sceneName);
        }
    }
}
