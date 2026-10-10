#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.Roads
{
    public class RoadSplitController
    {
        private RoadsModule editor;

        public RoadSplitController(RoadsModule editor)
        {
            this.editor = editor;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Split a road at the selected knot\n\n* enter spline edit mode\n* select a single knot between first and last knot, add one if necessary\n* split road\n\nThe road gameobject will be cloned and both splines will be set to the split data on the source gameobject and the clone.", MessageType.None);

            EditorGUILayout.BeginVertical("box");
            {
                // EditorGUILayout.LabelField("Split", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    bool enabled = SplineSelection.Count == 1;

                    GUI.enabled = enabled;
                    {
                        if (GUILayout.Button("Split Road"))
                        {
                            SplitRoad();
                        }
                    }
                    GUI.enabled = true;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void SplitRoad()
        {
            RoadSystem roadSystem = editor.GetRoadSystem();

            if (!roadSystem)
                return;

            if (editor.GetUndoableSpline("Split", out Road road, out SplineContainer splineContainer, out Spline spline))
            {
                int splineIndex = RoadSplineUtils.GetSplineIndex(splineContainer, spline);
                List<BezierKnot> selectedKnots = RoadSplineUtils.GetSelectedKnots(splineContainer, splineIndex);

                if (selectedKnots.Count != 1)
                {
                    Debug.LogError("Exactly 1 knot must be selected");
                    return;
                }

                BezierKnot selectedKnot = selectedKnots[0];
                int knotIndex = spline.IndexOf(selectedKnot);
                if ( knotIndex == 0 || knotIndex == spline.Count - 1)
                {
                    // Debug.LogError("Selected knot mustn't be first or last. Select a knot inbetween");
                    EditorUtility.DisplayDialog("Error", "Selected knot mustn't be first or last. Select a knot inbetween. Add one if necessary.", "Ok");

                    return;
                }

                Intersection[] intersections = IntersectionUtils.GetAllIntersections( roadSystem);

                Intersection sourceI = null;
                Intersection targetI = null;

                Intersection.ConnectionPoint sourceCp = null;
                Intersection.ConnectionPoint targetCp = null;

                foreach (Intersection intersection in intersections)
                {
                    IntersectionUtils.UpdateIntersection(roadSystem, intersection);
                }

                foreach (Intersection intersection in intersections)
                {
                    foreach (Intersection.ConnectionPoint pnt in intersection.connectionPoints)
                    {
                        if (pnt.road == null)
                            continue;

                        if (pnt.road == road)
                        {
                            if(pnt.front)
                            {
                                sourceI = intersection;
                                sourceCp = pnt;
                            }
                            else
                            {
                                targetI = intersection;
                                targetCp = pnt;
                            }
                        }
                    }
                }

                if( sourceCp == null || targetCp == null )
                {
                    Debug.LogError("No matching connection points found");
                    return;
                }

                // duplicate road gameobject
                GameObject sourceGo = road.gameObject;
                Transform parent = sourceGo.transform.parent;
                GameObject clone = GameObject.Instantiate(sourceGo, parent);

                // rename clone
                clone.name = sourceGo.name + " (Split)";
                Undo.RegisterCreatedObjectUndo(clone, "Split Road");

                // split spline on source object
                SplineKnotIndex splitData = new SplineKnotIndex(splineIndex, knotIndex);
                splineContainer.SplitSplineOnKnot(splitData);

                // transfer split spline
                SplineContainer targetSplineContainer = clone.GetComponent<SplineContainer>();
                targetSplineContainer.Spline = splineContainer.Splines[1];
                splineContainer.RemoveSplineAt(1);

                EditorUtility.SetDirty(sourceI);
                EditorUtility.SetDirty(targetI);

                roadSystem.ReGenerateRoads();
                roadSystem.UpdateAll();

                // update source intersection
                IntersectionUtils.UpdateIntersection(roadSystem, sourceI);

                // update the target as well for spline alignment
                IntersectionUtils.UpdateIntersection(roadSystem, targetI);

                editor.UpdateRoadSystem();

            }
        }
    }
}
#endif