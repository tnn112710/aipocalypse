#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    [CustomEditor(typeof(GridDesigner))]
    public class GridDesignerEditor : Editor
    {
        private GridDesignerEditor editor;
        private GridDesigner editorTarget;

        #region Grid
        SerializedProperty countX;
        SerializedProperty countZ;
        SerializedProperty distanceX;
        SerializedProperty distanceZ;
        SerializedProperty intersectionCross;
        SerializedProperty intersectionT;
        SerializedProperty layer;
        #endregion Grid

        JitterModule jitterModule;
        SerializedProperty jitterSettings;

        public void OnEnable()
        {
            this.editor = this;
            this.editorTarget = target as GridDesigner;

            countX = serializedObject.FindProperty("countX");
            countZ = serializedObject.FindProperty("countZ");
            distanceX = serializedObject.FindProperty("distanceX");
            distanceZ = serializedObject.FindProperty("distanceZ");
            intersectionCross = serializedObject.FindProperty("intersectionCross");
            intersectionT = serializedObject.FindProperty("intersectionT");
            layer = serializedObject.FindProperty("layer");

            jitterSettings = serializedObject.FindProperty("jitterSettings");
            jitterModule = new JitterModule(GetRoadSystem(), JitterModule.FilterType.Children, jitterSettings, editorTarget.gameObject);

        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox( "Create a grid of roads and intersections. The grid is built using the provided Cross and T intersection gameobjects.", MessageType.None );

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Grid", GUIStyles.BoxTitleStyle);

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    EditorGUILayout.PropertyField(countX);
                    EditorGUILayout.PropertyField(countZ);
                    EditorGUILayout.PropertyField(distanceX);
                    EditorGUILayout.PropertyField(distanceZ);

                    // intersection cross
                    GUI.backgroundColor = intersectionCross.objectReferenceValue != null ? GUIStyles.DefaultBackgroundColor : GUIStyles.ErrorBackgroundColor;
                    {
                        EditorGUILayout.PropertyField(intersectionCross);
                    }
                    GUI.backgroundColor = GUIStyles.DefaultBackgroundColor;

                    // intersection T
                    GUI.backgroundColor = intersectionT.objectReferenceValue != null ? GUIStyles.DefaultBackgroundColor : GUIStyles.ErrorBackgroundColor;
                    {
                        EditorGUILayout.PropertyField(intersectionT);
                    }
                    GUI.backgroundColor = GUIStyles.DefaultBackgroundColor;

                    EditorGUILayout.PropertyField(layer);

                    if (check.changed)
                    {
                    }
                }

                bool isSetupValid = intersectionCross.objectReferenceValue != null && intersectionT.objectReferenceValue != null;

                if( !isSetupValid)
                {
                    EditorGUILayout.HelpBox("Please provide intersection prefabs. If you have the Roads Demo installed you can click the Auto-Setup button for the assignment of the example intersections.", MessageType.Info);
                }
                EditorGUI.BeginDisabledGroup(!isSetupValid);
                {
                    if (GUILayout.Button("Create Grid", GUILayout.Height(30)))
                    {
                        CreateGrid();
                    }
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Setup", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(new GUIContent("Auto-Setup", "Assign intersections from the MicroVerse Roads demo content")))
                    {
                        PerformAutoSetup();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Hierarchy", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(new GUIContent("Clear", "Remove intersections and roads")))
                    {
                        Clear();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            jitterModule.OnInspectorGUI();

            serializedObject.ApplyModifiedProperties();
        }

        private RoadSystem GetRoadSystem()
        {
            GameObject target = editorTarget.gameObject;

            RoadSystem roadSystem = target?.GetComponentInParent<RoadSystem>();

            return roadSystem;
        }

        private void Clear()
        {
            bool isContinue = EditorUtility.DisplayDialog("Clear Grid", "Warning! You're about tho remove all roads and intersections from this gameobject.\n\nThis can't be undone! Continue?", "Yes", "No");

            if (!isContinue)
                return;

            Clear(editorTarget.transform);

        }


        private void Clear(Transform parent)
        {
            // get all intersections
            Intersection[] intersections = parent.GetComponentsInChildren<Intersection>();

            foreach (Intersection intersection in intersections)
            {
                // remove roads from the intersection
                IntersectionUtils.ClearSpawnedRoads(GetRoadSystem(), intersection);

                // delete intersection
                Object.DestroyImmediate(intersection.transform.gameObject);
            }
        }

        private void CreateGrid()
        {
            if (intersectionCross.objectReferenceValue == null || intersectionT.objectReferenceValue == null)
            {
                Debug.LogError("Missing intersection prefabs");
                return;
            }

            RoadSystem roadSystem = GetRoadSystem();

            Transform[] children = editorTarget.GetComponentsInChildren<Transform>();

            if (children.Length > 0)
            {
                bool isContinue = EditorUtility.DisplayDialog("Create Grid", $"GameObject contains children, these will be cleared!\n\nThis can't be undone! Continue?", "Yes", "No");

                if (!isContinue)
                    return;
            }

            Clear(editorTarget.transform);

            GameObject grid = editorTarget.gameObject;

            GridPlacement gridPlacement = new GridPlacement(roadSystem, grid, countX.intValue, countZ.intValue, distanceX.floatValue, distanceZ.floatValue, intersectionCross.objectReferenceValue as GameObject, intersectionT.objectReferenceValue as GameObject, layer.intValue);
            gridPlacement.CreateGrid();

            // update road system
            editor.GetRoadSystem()?.UpdateSystem(null);
        }

        private void PerformAutoSetup()
        {
            GridDesigner gridDesigner = editorTarget;

            gridDesigner.intersectionCross = AutoSetupUtils.FindIntersection("Intersection 2 Lanes Cross");
            gridDesigner.intersectionT = AutoSetupUtils.FindIntersection("Intersection 2 Lanes T");
        }



    }
}
#endif