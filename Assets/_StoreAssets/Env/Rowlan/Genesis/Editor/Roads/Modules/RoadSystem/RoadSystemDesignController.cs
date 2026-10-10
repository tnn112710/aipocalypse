#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    public class RoadSystemDesignController
    {
        private static GUIContent addGridDesignerContent = new GUIContent("Add Grid Designer", "Creates a container gameobject at the current position and adds the grid designer");

        private RoadsModule editor;

        public RoadSystemDesignController(RoadsModule editor)
        {
            this.editor = editor;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox( "Designers for road structures", MessageType.None );

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Designers", GUIStyles.BoxTitleStyle);

                if (GUILayout.Button(addGridDesignerContent))
                {
                    AddGridDesigner();
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

        private void AddGridDesigner()
        {
            RoadSystem roadSystem = GetRoadSystem();
            GameObject parent = roadSystem.gameObject;

            // create gameobject
            GameObject go = new GameObject("Grid");

            // set parent
            GameObjectUtility.SetParentAndAlign(go, parent);

            // position in scene view at look position
            Transform sceneCameraTransform = SceneView.lastActiveSceneView.camera.transform;

            if (Physics.Raycast(sceneCameraTransform.position, sceneCameraTransform.forward, out RaycastHit hit))
            {
                go.transform.position = hit.point;
                go.transform.rotation = Quaternion.identity;
            }

            // ensure unique name depending on parent
            GameObjectUtility.EnsureUniqueNameForSibling(go);

            // add scripts
            GridDesigner gridDesigner = go.AddComponent<GridDesigner>();

            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);

            Selection.activeObject = go;

        }
    }
}
#endif