using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JBooth.MicroVerseCore
{
    class SceneBuildStripping : IProcessSceneWithReport
    {
        public int callbackOrder { get { return 0; } }
        public void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report)
        {
#if UNITY_6000_7_OR_NEWER
            var mvs = GameObject.FindObjectsByType<MicroVerse>(FindObjectsInactive.Include);
#else
            var mvs = GameObject.FindObjectsByType<MicroVerse>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#endif

            foreach (var mv in mvs)
            {
                mv.CancelInvoke();
#if __MICROVERSE_VEGETATION__
                var binds = mv.GetComponentsInChildren<BindHeightFilterRangeToTransform>();
                foreach (var b in binds)
                {
                    GameObject.DestroyImmediate(b);
                }
#endif
                var all = mv.GetComponentsInChildren<IModifier>(true);
                
                foreach (var m in all)
                {
                    m.StripInBuild();
                }
                if (Application.isPlaying)
                {
                    GameObject.Destroy(mv);
                }
                else
                {
                    GameObject.DestroyImmediate(mv);
                }
            }
        }
    }
}