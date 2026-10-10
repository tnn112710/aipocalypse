#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.Roads
{
    public class GeneralDesigner : IRoadDesigner
    {
        private RoadDesignController controller;

        public GeneralDesigner(RoadDesignController controller)
        {
            this.controller = controller;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Hierarchy", GUIStyles.BoxTitleStyle);

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button(new GUIContent("Clear Inbetween", "Remove knots between first and last knot")))
                {
                    ClearInbetween();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Spline", GUIStyles.BoxTitleStyle);

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button(new GUIContent("Subdivide", "Subdivide equally distributed")))
                {
                    Subdivide();
                }
                if (GUILayout.Button(new GUIContent("Unsubdivide", "Unsubdivide equally distributed")))
                {
                    Unsubdivide();
                }
                if (GUILayout.Button(new GUIContent("Unroll", "Remove forward rotation on spline knots, ie set Z to 0")))
                {
                    Unroll();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (ProjectSetup.DEVELOPER_MODE)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Development", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(new GUIContent("Log", "Logging for development")))
                    {
                        LogSplineData();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

        }



        private void ClearInbetween()
        {

            if (controller.GetEditor().GetUndoableSpline("Clear Inbetween", out Road road, out SplineContainer splineContainer, out Spline spline))
            {
                if (RoadSplineUtils.GetSelectedKnotsBounds(splineContainer, spline, out BezierKnot firstKnot, out BezierKnot lastKnot, out int firstKnotIndex, out int lastKnotIndex))
                {
                    RoadSplineUtils.ClearInBetweenSelection(splineContainer, spline, firstKnotIndex, lastKnotIndex);
                }
            }
        }

        private void Subdivide()
        {
            if (controller.GetEditor().GetUndoableSpline("Subdivide", out Road road, out SplineContainer splineContainer, out Spline spline))
            {
                if (RoadSplineUtils.GetSelectedKnotsBounds(splineContainer, spline, out BezierKnot firstKnot, out BezierKnot lastKnot, out int firstKnotIndex, out int lastKnotIndex))
                {
                    RoadSplineUtils.SubdivideEqualDistribution(splineContainer, spline, firstKnotIndex, lastKnotIndex);
                }
            }

        }

        private void Unsubdivide()
        {
            if (controller.GetEditor().GetUndoableSpline("Unsubdivide", out Road road, out SplineContainer splineContainer, out Spline spline))
            {
                if (RoadSplineUtils.GetSelectedKnotsBounds(splineContainer, spline, out BezierKnot firstKnot, out BezierKnot lastKnot, out int firstKnotIndex, out int lastKnotIndex))
                {
                    RoadSplineUtils.UnsubdivideEqualDistribution(splineContainer, spline, firstKnotIndex, lastKnotIndex);
                }
            }
        }

        private void Unroll()
        {
            if (controller.GetEditor().GetUndoableSpline("Unroll", out Road road, out SplineContainer splineContainer, out Spline spline))
            {
                if (RoadSplineUtils.GetSelectedKnotsBounds(splineContainer, spline, out BezierKnot firstKnot, out BezierKnot lastKnot, out int firstKnotIndex, out int lastKnotIndex))
                {
                    RoadSplineUtils.Unroll(spline, firstKnotIndex, lastKnotIndex);
                }
            }
        }

        private void LogSplineData()
        {
            if (controller.GetEditor().GetUndoableSpline("Log Spline Data", out Road road, out SplineContainer splineContainer, out Spline spline))
            {
                if (RoadSplineUtils.GetSelectedKnotsBounds(splineContainer, spline, out BezierKnot firstKnot, out BezierKnot lastKnot, out int firstKnotIndex, out int lastKnotIndex))
                {
                    Debug.Log($"Selection bounds: {firstKnotIndex}: {firstKnot.Position} -> {lastKnotIndex}: {lastKnot.Position}");

                    SplineUtility.GetNearestPoint(spline, firstKnot.Position, out float3 nearestFirst, out float tFirst);
                    SplineUtility.GetNearestPoint(spline, lastKnot.Position, out float3 nearestLast, out float tLast);


                    Debug.Log($"t of first and last: {tFirst} -> {tLast}");

                }
                else
                {
                    Debug.Log("No spline matching knot bound request");
                }
            }
            else
            {
                Debug.Log("No spline data for operations");
            }
        }
    }
}
#endif