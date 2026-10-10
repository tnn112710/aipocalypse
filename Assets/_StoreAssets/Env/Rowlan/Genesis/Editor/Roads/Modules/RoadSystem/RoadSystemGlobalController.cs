#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    public class RoadSystemGlobalController
    {
        private static GUIContent clearCacheAndUpdateSystemContent = new GUIContent("Clear Cache & Update System", "Clear the internal MicroVerse Roads cache and update the road system");

        private RoadsModule editor;

        public RoadSystemGlobalController(RoadsModule editor)
        {
            this.editor = editor;
        }


        public void OnInspectorGUI()
        {

        EditorGUILayout.HelpBox("Operations on the current road system", MessageType.None);

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Update", GUIStyles.BoxTitleStyle);

                if (GUILayout.Button(clearCacheAndUpdateSystemContent))
                {
                    GetRoadSystem()?.ClearCacheAndUpdateSystem();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private RoadSystem GetRoadSystem()
        {
            GameObject target = editor.GetEditorTarget().gameObject;

            RoadSystem roadSystem = target?.GetComponent<RoadSystem>();

            return roadSystem;
        }

    }
}
#endif