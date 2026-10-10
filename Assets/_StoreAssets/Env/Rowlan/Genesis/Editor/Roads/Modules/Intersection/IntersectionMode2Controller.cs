#if __MICROVERSE_ROADS__
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    public class IntersectionMode2Controller
    {
        private static GUIContent resetRotationContent = new GUIContent("Reset Rotation", "Reset the transform rotation");
        private static GUIContent addConnectedRoadsContent = new GUIContent("Add Connected Roads", "Add the roads of the intersection to the current selection (including this intersection)");
        private static GUIContent selectConnectedRoadsContent = new GUIContent("Select Connected Roads", "Set roads of the intersection as the current selection (excluding this intersection)");
        private static GUIContent alignSelectionToTerrainContent = new GUIContent("Align Selection to Terrain", "Align the selected intersection to the terrain. This includes the roads of the intersection");

        private RoadsModule editor;

        public IntersectionMode2Controller(RoadsModule editor)
        {
            this.editor = editor;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Hierarchy", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(resetRotationContent))
                    {
                        ResetRotation();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();


            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Selection", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(addConnectedRoadsContent))
                    {
                        SelectionUtils.AddConnectedRoadsToSelection();
                    }

                    if (GUILayout.Button(selectConnectedRoadsContent))
                    {
                        SelectionUtils.SelectConnectedRoads();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();


            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Align", GUIStyles.BoxTitleStyle);

                EditorGUILayout.HelpBox("Align the selected intersections including their connected roads.", MessageType.None);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(alignSelectionToTerrainContent))
                    {
                        RoadAlignment roadAlignment = new RoadAlignment(editor);

                        // influence of the roads to the intersections is too big, the height evaluation might suffer => we need to align the connected roads as well
                        List<Component> components = SelectionUtils.GetSelectedIntersectionsWithRoads();
                        roadAlignment.AlignToTerrain( components);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void ResetRotation()
        {
            GameObject target = editor.GetEditorTarget().gameObject;

            if (target == null)
                return;

            target.transform.localRotation = Quaternion.identity;
        }


    }
}
#endif