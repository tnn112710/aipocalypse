#if __MICROVERSE_ROADS__

using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.Roads
{
    public class RoadsModule
    {
        private RoadsModule editor;
        private RoadsData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty autoAssignment;

        private IRoadsModule intersectionModule;
        private IRoadsModule roadModule;
        private IRoadsModule roadSystemModule;
        private IRoadsModule infoTextModule;

        public void OnEnable(SerializedObject serializedObject, RoadsData target)
        {
            this.editor = this;
            this.editorTarget = target;
            this.serializedObject = serializedObject;

            autoAssignment = serializedObject.FindProperty("autoAssignment");

            this.intersectionModule = new IntersectionModule(editor, serializedObject);
            this.roadModule = new RoadModule(editor, serializedObject);
            this.roadSystemModule = new RoadSystemModule(editor, serializedObject);
            this.infoTextModule = new InfoTextModule(editor, serializedObject);
        }

        public void OnDisable()
        {
        }

        public SerializedObject GetSerializedObject()
        {
            return this.serializedObject;
        }

        public RoadsData GetEditorTarget()
        {
            return editorTarget;
        }

        #region Inspector
        public void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();
                if( GUILayout.Button("Update System"))
                {
                    MicroVerse.instance.ClearCacheAndUpdateRoadSystems();
                }
            }
            EditorGUILayout.EndHorizontal();

            // data
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.LabelField("General Data", GUIStyles.BoxTitleStyle);

                    EditorGUI.BeginChangeCheck();
                    {
                        EditorGUILayout.PropertyField(autoAssignment, new GUIContent("Auto Assignment", "Automatically assign a gameobject on selection change"));
                    }
                    if( EditorGUI.EndChangeCheck())
                    {
                        AssignSelectedGameObject();
                    }

                    
                    GUI.backgroundColor = HasMicroverseGameObject() ? GUIStyles.DefaultBackgroundColor : GUIStyles.ErrorBackgroundColor;
                    {
                        editorTarget.gameObject = EditorGUILayout.ObjectField(new GUIContent("MicroVerse Object"), editorTarget.gameObject, typeof(GameObject), true) as GameObject; // object isn't serialized

                        
                        // visualize the stamp
                        /*
                        GUI.enabled = false;
                        {
                            EditorGUILayout.ObjectField(new GUIContent("Type"), editorTarget..., typeof(.Name.Length.), true); // type isn't serialized
                        }
                        GUI.enabled = true;
                        */

                        if (!HasMicroverseGameObject())
                        {
                            EditorGUILayout.HelpBox("Please select a MicroVerse object", MessageType.Info);
                        }

                    }
                    GUI.backgroundColor = GUIStyles.DefaultBackgroundColor;
            }
            EditorGUILayout.EndVertical();

            // microverse gameobject details
            if(HasMicroverseGameObject())
            {
                // separator
                GUIStyles.DrawSeparator();

                // modules
                if (intersectionModule.IsSupported( editorTarget.gameObject))
                {
                    intersectionModule.OnInspectorGUI();
                }
                else if (roadModule.IsSupported(editorTarget.gameObject))
                {
                    roadModule.OnInspectorGUI();
                }
                else if (roadSystemModule.IsSupported(editorTarget.gameObject))
                {
                    roadSystemModule.OnInspectorGUI();
                }
                else if( infoTextModule.IsSupported(editorTarget.gameObject))
                {
                    infoTextModule.OnInspectorGUI();
                }
                else
                {
                    EditorGUILayout.HelpBox( "Not available for " + editorTarget.gameObject.GetType().Name, MessageType.Error);
                }
            }

            // apply changes to serializedProperty
            serializedObject.ApplyModifiedProperties();
        }

        private bool HasMicroverseGameObject()
        {
            return editorTarget.gameObject != null;
        }
        #endregion Inspector


        public void AssignSelectedGameObject()
        {
            if (!autoAssignment.boolValue)
                return;

            GameObject selectedObject = Selection.activeGameObject;

            // the stamp which is about to be assigned
            GameObject gameObject = null;

            // get the stamp from the selected object
            // note: selected object can actually be null
            if (selectedObject != null)
            {
                gameObject = selectedObject.GetComponent<Intersection>()?.gameObject;

                if( gameObject == null)
                {
                    gameObject = selectedObject.GetComponent<Road>()?.gameObject;
                }

                if (gameObject == null)
                {
                    gameObject = selectedObject.GetComponent<RoadSystem>()?.gameObject;
                }

                if (gameObject == null)
                {
                    gameObject = selectedObject.GetComponent<GridDesigner>()?.gameObject;
                }

            }

            // assign stamp
            editorTarget.gameObject = gameObject;

            EditorUtility.SetDirty(serializedObject.targetObject);
            
        }

        public RoadSystem GetRoadSystem()
        {
            return editorTarget.gameObject.transform.GetComponentInParent<RoadSystem>();
        }

        public void UpdateRoadSystem()
        {
            editor.GetRoadSystem()?.UpdateSystem( null);
        }

        public Road GetRoad()
        {
            GameObject target = editor.GetEditorTarget().gameObject;

            Road road = target?.GetComponent<Road>();

            return road;
        }

        /// <summary>
        /// Get a spline for an undoable operation. The spline must have at least 2 knots
        /// </summary>
        /// <param name="undoText"></param>
        /// <returns></returns>
        public bool GetUndoableSpline(string undoText, out Road road, out SplineContainer splineContainer, out Spline spline)
        {
            // reset values
            road = null;
            splineContainer = null;
            spline = null;

            // road
            road = GetRoad();

            if (!road)
                return false;

            // container
            splineContainer = road.splineContainer;

            // spline
            spline = road.splineContainer?.Spline;

            if (spline.Count < 2)
                return false;

            // register undo
            Undo.RegisterCompleteObjectUndo(road.splineContainer, undoText);

            return true;

        }
    }
}
#endif