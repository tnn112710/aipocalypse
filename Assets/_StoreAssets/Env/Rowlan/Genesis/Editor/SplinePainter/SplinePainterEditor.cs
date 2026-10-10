#if __MICROVERSE_SPLINES__
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.SplinePainter
{
    [CustomEditor(typeof(SplinePainter))]
    public class SplineDrawerEditor : Editor
    {
        const int LeftMouseButton = 0;

        SplineDrawerEditor editor;
        SplinePainter editorTarget;

        SplineContainer container;
        Spline spline;

        float strokeDeltaThreshold;

        List<float3> stroke = new List<float3>(1024);
        List<float3> reducedStroke = new List<float3>(512);
        List<Vector3> handleStroke = new List<Vector3>(1024);

        public void OnEnable()
        {
            editor = this;
            editorTarget = (SplinePainter)target;

            if (editorTarget.container == null)
            {
                editorTarget.container = editorTarget.GetComponent<SplineContainer>();
            }

            container = editorTarget.container;
            spline = editorTarget.container.Spline;

            strokeDeltaThreshold = editorTarget.strokeDeltaThreshold;

            // subscribe to scene gui changes
            SceneView.beforeSceneGui -= OnSceneGUIEvent;
            SceneView.beforeSceneGui += OnSceneGUIEvent;

        }

        public void OnDisable()
        {
            // unsubscribe from scene gui changes
            SceneView.beforeSceneGui -= OnSceneGUIEvent;

        }

        public override void OnInspectorGUI()
        {
            if (!ProjectSetup.DEVELOPER_MODE)
                return;

            base.OnInspectorGUI();

            serializedObject.Update();
            if (GUILayout.Button("Clear"))
            {
                spline?.Clear();

                stroke.Clear();
                reducedStroke.Clear();
                handleStroke.Clear();
            }
            serializedObject.ApplyModifiedProperties();
        }
        private void OnSceneGUIEvent(SceneView sceneView)
        {
            // perform method only when the mouse is really in the sceneview; the scene view would register other events as well
            var isMouseInSceneView = new Rect(0, 0, sceneView.position.width, sceneView.position.height).Contains(Event.current.mousePosition);
            if (!isMouseInSceneView)
                return;

            // keyboard + mouse case
            if (Event.current.isMouse && Event.current.shift)
            {
                // left button = 0; right = 1; middle = 2
                if (Event.current.button == LeftMouseButton)
                {
                    // drag case
                    if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag)
                    {
                        //if (Event.current.shift)
                        {
                            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

                            if (Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, Mathf.Infinity))
                            {
                                if (Event.current.type == EventType.MouseDown)
                                {
                                    stroke.Clear();
                                    handleStroke.Clear();

                                    stroke.Add(hit.point);
                                    handleStroke.Add(hit.point);

                                }
                                else
                                {
                                    Vector3 lastKnotWorldPosition = stroke[stroke.Count - 1];
                                    float distance = Vector3.Distance(lastKnotWorldPosition, hit.point);

                                    if (distance > strokeDeltaThreshold)
                                    {
                                        stroke.Add(hit.point);
                                        handleStroke.Add(hit.point);
                                    }
                                }
                            }

                            Event.current.Use();

                        }
                    }
                    if (Event.current.type == EventType.MouseUp)
                    {
                        RebuildSpline();

                        stroke.Clear();
                        reducedStroke.Clear();
                        handleStroke.Clear();

                    }
                }
            }

        }

        private void OnSceneGUI()
        {
            if (Event.current.type == EventType.Repaint)
            {
                Handles.color = Color.white;
                Handles.DrawAAPolyLine(20, handleStroke.ToArray());

            }

        }
        void RebuildSpline()
        {
            List<float3> samples = stroke.Select(x => (float3)container.transform.InverseTransformPoint(x) + new float3(0, editorTarget.heightOffset, 0)).ToList();

            // Before setting spline knots, reduce the number of sample points.
            SplineUtility.ReducePoints(samples, reducedStroke, editorTarget.pointReductionEpsilon);

            Spline spline = editorTarget.container.Spline;

            // Assign the reduced sample positions to the Spline knots collection. Here we are constructing new
            // BezierKnots from a single position, disregarding tangent and rotation. The tangent and rotation will be
            // calculated automatically in the next step wherein the tangent mode is set to "Auto Smooth."
            spline.Knots = reducedStroke.Select(x => new BezierKnot(x));

            SplineRange splineRange = new SplineRange(0, spline.Count);

            // Sets the tangent mode for all knots in the spline to "Auto Smooth."
            spline.SetTangentMode(splineRange, TangentMode.AutoSmooth);

            // Sets the tension parameter for all knots. Note that the "Tension" parameter is only applicable to
            // "Auto Smooth" mode knots.
            spline.SetAutoSmoothTension(splineRange, editorTarget.splineTension);

            Debug.Log($"Input Sample Count: {stroke.Count}\nSpline Knot Count: {reducedStroke.Count}");

        }
    }
}
#endif
