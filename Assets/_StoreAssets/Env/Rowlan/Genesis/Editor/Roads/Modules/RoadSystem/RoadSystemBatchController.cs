#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    public class RoadSystemBatchController
    {
        private RoadsModule editor;

        JitterModule jitterModule;
        SerializedProperty jitterSettings;

        public RoadSystemBatchController(RoadsModule editor)
        {
            this.editor = editor;

            jitterSettings = editor.GetSerializedObject().FindProperty("roadSystemSettings.jitterSettings");
            jitterModule = new JitterModule(GetRoadSystem(), JitterModule.FilterType.Selection, jitterSettings, null);
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Perform operations on the selected intersections and roads", MessageType.None);

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Select", GUIStyles.BoxTitleStyle);

                EditorGUILayout.HelpBox("Select the specified item and the road system. This is required in case you have auto assignment active. Otherwise the current operation panel would change", MessageType.None);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Intersections"))
                    {
                        SetSelection(new Type[] { typeof(Intersection)});
                    }

                    if (GUILayout.Button("Roads"))
                    {
                        SetSelection(new Type[] { typeof(Road) });
                    }

                    if (GUILayout.Button("Intersections & Roads"))
                    {
                        SetSelection(new Type[] { typeof(Intersection), typeof(Road) });
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Align", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Align Selection to Terrain", GUILayout.Height(30)))
                    {
                        RoadAlignment roadAlignment = new RoadAlignment(editor);

                        // influence of the roads to the intersections is too big, the height evaluation might suffer => we need to align the connected roads as well
                        List<Component> components = SelectionUtils.GetSelectedIntersectionsWithRoads();
                        roadAlignment.AlignToTerrain(components);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            jitterModule.OnInspectorGUI();

        }

        /// <summary>
        /// Select the specified types including the current road system
        /// </summary>
        /// <param name="types"></param>
        private void SetSelection(Type[] types)
        {
            List<GameObject> selectedItems = new List<GameObject>();

            RoadSystem roadSystem = GetRoadSystem();

            if (roadSystem == null)
                return;

            // select road system as well, otherwise we'll lose the form because of the auto assignment; select it last, so it becomes the active one
            selectedItems.Add(roadSystem.gameObject);

            foreach (Type type in types) 
            {
                List<Component> items = roadSystem.GetComponentsInChildren(type).ToList();
                List<GameObject> gameObjects = items.Select(x => x.gameObject).ToList();

                selectedItems.AddRange(gameObjects);

            }

            Selection.objects = selectedItems.ToArray();
            Selection.activeGameObject = roadSystem.gameObject;

        }

        /// <summary>
        /// Get all items of the specified type and return a list of their gameobjects
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="roadSystem"></param>
        /// <returns></returns>
        private List<GameObject> GetGameObjects<T>( RoadSystem roadSystem)
        {
            List<Component> objects = roadSystem?.GetComponentsInChildren( typeof(T)).ToList();
            List<GameObject> gameObjects = objects.Select(x => x.gameObject).ToList();

            return gameObjects;
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