#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using static JBooth.MicroVerseCore.Intersection;

namespace Rowlan.Genesis.Roads
{
    public class IntersectionMode1Controller
    {
        private static GUIContent spawnAllRoadsContent = new GUIContent("Spawn All Roads", "Spawn all roads of the intersection");
        private static GUIContent clearSpawnedRoadsContent = new GUIContent("Clear Spawned Roads", "Remove all roads from the intersection");

        private RoadsModule editor;

        private float faceAngleLimit = 30f;
        private AutoConnector.Priority priority = AutoConnector.Priority.FaceAngle;

        public IntersectionMode1Controller(RoadsModule editor)
        {
            this.editor = editor;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                #region Creation
                EditorGUILayout.LabelField("Create", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(spawnAllRoadsContent))
                    {
                        SpawnAllRoads();
                    }

                    if (GUILayout.Button(clearSpawnedRoadsContent))
                    {
                        ClearSpawnedRoads();
                    }

                }
                EditorGUILayout.EndHorizontal();

                #endregion Creation

                #region Connection
                EditorGUILayout.LabelField("Connect", GUIStyles.BoxTitleStyle);

                EditorGUILayout.HelpBox("Select 2 Intersections with free connection points to have them connected depending on the given paramters", MessageType.None);

                priority = (AutoConnector.Priority) EditorGUILayout.EnumPopup("Priority", priority);
                faceAngleLimit = EditorGUILayout.FloatField("Face Angle Limit", faceAngleLimit);

                EditorGUILayout.BeginHorizontal();
                {
                    bool prevGuiEnabled = GUI.enabled;
                    GUI.enabled = CanAutoConnect();
                    {
                        if (GUILayout.Button("Auto Connect"))
                        {
                            AutoConnectMultiple();
                        }
                    }
                    GUI.enabled = prevGuiEnabled;
                }
                EditorGUILayout.EndHorizontal();
                #endregion Connection

                #region Merge
                // TODO: not finished yet
                if (ProjectSetup.DEVELOPER_MODE)
                {
                    EditorGUILayout.LabelField("Merge", GUIStyles.BoxTitleStyle);

                    EditorGUILayout.HelpBox("Select 1 road and after that 1 intersection. If 2 free connection points were found, the closest ones to the road can be used to merge the intersection with the road.", MessageType.None);

                    EditorGUILayout.BeginHorizontal();
                    {
                        bool prevGuiEnabled = GUI.enabled;
                        GUI.enabled = CanAutoMerge();
                        {
                            if (GUILayout.Button("Auto Merge"))
                            {
                                AutoMerge();
                            }
                        }
                        GUI.enabled = prevGuiEnabled;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                #endregion Merge

            }
            EditorGUILayout.EndVertical();
        }

        private Intersection GetIntersection()
        {
            GameObject target = editor.GetEditorTarget().gameObject;

            Intersection intersection = target?.GetComponent<Intersection>();

            return intersection;
        }

        private void SpawnAllRoads()
        {
            RoadSystem roadSystem = editor.GetRoadSystem();

            if (!roadSystem)
                return;

            List<Intersection> intersections = GetSelectedIntersections();

            foreach (Intersection intersection in intersections)
            {
                foreach (Intersection.ConnectionPoint pnt in intersection.connectionPoints)
                {
                    if (pnt.container != null)
                        continue;

                    IntersectionEditor editor = Editor.CreateEditor(intersection) as IntersectionEditor;
                    editor.CreateRoad(roadSystem, intersection, pnt, false);
                    Editor.DestroyImmediate(editor);
                }

                UpdateIntersection(intersection);
            }

        }

        private void ClearSpawnedRoads()
        {
            RoadSystem roadSystem = editor.GetRoadSystem();

            if (!roadSystem)
                return;

            // undo doesn't work because the road is spawned as separate object in a different hierarchy
            // Undo.RegisterCompleteObjectUndo(target, "Clear Spawned Roads");

            List<Intersection> intersections = GetSelectedIntersections();

            foreach(Intersection intersection in intersections)
            {
                IntersectionUtils.ClearSpawnedRoads(roadSystem, intersection);
            }

            editor.UpdateRoadSystem();
        }

        private bool CanAutoConnect()
        {
            return GetSelectedIntersections().Count > 1;
        }

        private List<Intersection> GetSelectedIntersections()
        {
            List<Intersection> intersections = new List<Intersection>();

            foreach (GameObject selectedGo in Selection.gameObjects)
            {
                Intersection intersection = selectedGo.GetComponent<Intersection>();

                if (intersection == null)
                    continue;

                intersections.Add(intersection);
            }

            return intersections;
        }

        private List<Road> GetSelectedRoads()
        {
            List<Road> roads = new List<Road>();

            foreach (GameObject selectedGo in Selection.gameObjects)
            {
                Road road = selectedGo.GetComponent<Road>();

                if (road == null)
                    continue;

                roads.Add(road);
            }

            return roads;
        }

        private void AutoConnectMultiple()
        {

            RoadSystem roadSystem = editor.GetRoadSystem();

            List<Intersection> intersections = GetSelectedIntersections();

            if( intersections.Count < 2 )
            {
                Debug.LogError("At least 2 intersections required");
                return;
            }

            AutoConnector autoConnector = new AutoConnector(roadSystem, intersections, priority, faceAngleLimit);
            autoConnector.AutoConnect();

            editor.UpdateRoadSystem();
        }

        private void UpdateIntersection( Intersection intersection )
        {
            intersection.UpdateConnections();

            editor.UpdateRoadSystem();
        }

        #region Merge
        private bool CanAutoMerge()
        {
            return GetSelectedIntersections().Count == 1 && GetSelectedRoads().Count == 1;
        }

        private void AutoMerge()
        {
            List<Intersection> intersections = GetSelectedIntersections();
            List<Road> roads = GetSelectedRoads();

            if (intersections.Count != 1 && roads.Count != 1)
            {
                Debug.LogError("1 intersection and 1 road required");
                return;
            }

            // find the road that's overlapping with the intersection
            RoadSystem roadSystem = editor.GetRoadSystem();

            if (!roadSystem)
                return;

            Intersection intersection = intersections[0];
            Road road = roads[0];

            Spline spline = road.splineContainer.Spline;
            if (spline == null)
            {
                Debug.LogError($"Spline on road {road.name} missing");
                return;
            }

            RoadMerge roadMerge = new RoadMerge(intersection, road);
            if( roadMerge.Evaluate())
            {
                roadMerge.Merge();
            }
        }

        #endregion Merge

    }
}
#endif