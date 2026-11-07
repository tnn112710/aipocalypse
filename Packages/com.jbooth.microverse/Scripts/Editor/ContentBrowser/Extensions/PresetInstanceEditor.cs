using UnityEditor;
using UnityEngine;
#if UNITY_6000_7_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif
namespace JBooth.MicroVerseCore.Browser
{
    [CustomEditor(typeof(PresetInstance))]
#if UNITY_6000_7_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public class PresetInstanceEditor : Editor
    {
        private static GUIContent helpContent = new GUIContent("Marker for a prefab that's coming from the Content Browser. Existing presets will be replaced with new ones");

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(helpContent.text, MessageType.Info);

            base.OnInspectorGUI();
        }
    }
}