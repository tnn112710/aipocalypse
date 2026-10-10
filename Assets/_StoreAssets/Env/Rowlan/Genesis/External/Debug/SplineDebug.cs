#if __MICROVERSE_SPLINES__
/// <summary>
/// Debug helper to see data of the spline in the scene
/// </summary>
namespace Rowlan.Genesis.SplineDebug
{
    using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEditor.Splines;
#endif

    using System.Collections.Generic;
    using Unity.Mathematics;
    using UnityEngine.Splines;
    using SplinePath = JBooth.MicroVerseCore.SplinePath;
    using static JBooth.MicroVerseCore.SplinePath;

    [ExecuteInEditMode]
    public class SplineDebug : MonoBehaviour
    {
        private struct DebugInfo
        {
            public Vector3 position;
            public string text;
        }

        public SplinePath splinePath;

        [Header("Knots")]

        public bool debugKnots = true;

        [Space]

        public bool splineIndex = false;
        public bool knotIndex = false;
        public bool localPosition = false;
        public bool worldPosition = false;
        public bool distance = true;
        public bool normalized = true;
        public bool knot = true;

        [Header("Widths")]

        public bool debugWidths = true;

        [Space]

        public bool splineWidthIndex = false;
        public bool widthDataIndex = false;

#if UNITY_EDITOR
        [CustomEditor(typeof(SplineDebug))]
        public class SplineDebugEditor : Editor
        {
            SplineDebug editorTarget;
            SplineDebugEditor editor;

            public void OnEnable()
            {
                editor = this;
                editorTarget = (SplineDebug)target;

                Spline.Changed += OnSplineChanged;
                EditorSplineUtility.RegisterSplineDataChanged<float>(SplineDataChanged);

                if (editorTarget.splinePath == null)
                {
                    editorTarget.splinePath = editorTarget.GetComponent<SplinePath>();
                }

                ShowDebugInfo();
            }

            public void OnDisable()
            {
                Spline.Changed -= OnSplineChanged;
                EditorSplineUtility.UnregisterSplineDataChanged<float>(SplineDataChanged);

            }

            void SplineDataChanged(SplineData<float> splineData)
            {
                ShowDebugInfo();
            }

            void OnSplineChanged(Spline spline, int knotIndex, SplineModification modificationType)
            {
                ShowDebugInfo();
            }


            List<DebugInfo> debugInfos = new List<DebugInfo>();

            void OnSceneGUI()
            {
                foreach (DebugInfo di in debugInfos)
                {
                    Handles.Label(di.position, di.text);
                }
            }

            public override void OnInspectorGUI()
            {
                serializedObject.Update();

                using var changeScope = new EditorGUI.ChangeCheckScope();
                {
                    base.DrawDefaultInspector();
                                        
                }
                if (changeScope.changed)
                {
                    ShowDebugInfo();
                }

                serializedObject.ApplyModifiedProperties();
            }

            private void ShowDebugInfo()
            {
                if (!editorTarget.splinePath)
                    return;

                SplineContainer splineContainer = editorTarget.splinePath?.spline;

                if (splineContainer == null)
                    return;

                // debug data object
                this.debugInfos = new List<DebugInfo>();

                if (editorTarget.debugKnots)
                {
                    List<DebugInfo> knotDebugInfos = GetSplineKnotDebugInfos(splineContainer);
                    this.debugInfos.AddRange(knotDebugInfos);
                }

                if( editorTarget.debugWidths)
                {
                    List<DebugInfo> widthDebugInfos = GetSplineWidthDebugInfos(editorTarget.splinePath, splineContainer);
                    this.debugInfos.AddRange(widthDebugInfos);
                }

            }

            private List<DebugInfo> GetSplineKnotDebugInfos(SplineContainer container)
            {
                List<DebugInfo> debugInfos = new List<DebugInfo>();

                Matrix4x4 worldMatrix = editorTarget.transform.localToWorldMatrix;

                for (int splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
                {
                    Spline spline = container.Splines[splineIndex];

                    for (int knotIndex = 0; knotIndex < spline.Count; knotIndex++)
                    {
                        BezierKnot knot = spline[knotIndex];

                        float distanceValue = spline.ConvertIndexUnit(knotIndex, PathIndexUnit.Knot, PathIndexUnit.Distance);
                        float normalizedValue = spline.ConvertIndexUnit(knotIndex, PathIndexUnit.Knot, PathIndexUnit.Normalized);
                        float knotValue = spline.ConvertIndexUnit(knotIndex, PathIndexUnit.Knot, PathIndexUnit.Knot);

                        Vector3 worldPosition = worldMatrix.MultiplyPoint(knot.Position);

                        string text = "";

                        if (editorTarget.splineIndex)
                            text += $"\nspline index={splineIndex}";

                        if (editorTarget.knotIndex)
                            text += $"\nknot index={knotIndex}";

                        if (editorTarget.localPosition)
                            text += $"\nknot position={knot.Position}";

                        if (editorTarget.worldPosition)
                            text += $"\nworld position={worldPosition}";

                        if (editorTarget.distance)
                            text += $"\ndistance={distanceValue}";

                        if (editorTarget.normalized)
                            text += $"\nnormalized={normalizedValue}";

                        if (editorTarget.knot)
                            text += $"\nknot={knotValue}";

                        if (text.StartsWith("\n"))
                            text = text.Substring(1);

                        // create debug structure
                        DebugInfo debugInfo = new DebugInfo();

                        debugInfo.position = worldPosition;
                        debugInfo.text = text;

                        debugInfos.Add(debugInfo);
                    }
                }

                return debugInfos;
            }

            /// <summary>
            /// Get the main spline from the MicroVerse object
            /// </summary>
            /// <returns></returns>
            private Spline GetMainSpline()
            {
                return editorTarget.splinePath?.spline?.Spline;
            }

            private List<DebugInfo> GetSplineWidthDebugInfos(SplinePath splinePath, SplineContainer container)
            {
                // only main spline supported for width
                Spline spline = GetMainSpline();

                List<DebugInfo> debugInfos = new List<DebugInfo>();

                Matrix4x4 worldMatrix = editorTarget.transform.localToWorldMatrix;

                List<SplineWidthData> splineWidthDatas = splinePath.splineWidths;

                for( int i=0; i < splineWidthDatas.Count; i++) {

                    SplineData<float> splineData = splineWidthDatas[i].widthData;

                    for( int j=0; j < splineData.Count; j++)
                    {
                        DataPoint<float> dataPoint = splineData[j];

                        float normalizedIndex = SplineUtility.GetNormalizedInterpolation(spline, dataPoint.Index, splineData.PathIndexUnit);
                        spline.Evaluate(normalizedIndex, out float3 position, out float3 tangent, out float3 up);

                        float value = dataPoint.Value;

                        Vector3 worldPosition = worldMatrix.MultiplyPoint(position);

                        string text = "";

                        if( editorTarget.splineWidthIndex)
                         text += $"\nspline width index={i}";

                        if( editorTarget.widthDataIndex)
                            text += $"\nwidth data index={j}";

                        if (editorTarget.worldPosition)
                            text += $"\nworld position={worldPosition}";

                        text += $"\ndatapoint index={dataPoint.Index}";

                        text += $"\nnormalized index={normalizedIndex}";

                        text += $"\nwidth={value}";

                        if (text.StartsWith("\n"))
                            text = text.Substring(1);

                        // create debug structure
                        DebugInfo debugInfo = new DebugInfo();

                        debugInfo.position = worldPosition;
                        debugInfo.text = text;

                        debugInfos.Add(debugInfo);

                    }
                }

                return debugInfos;
            }
        }
#endif
    }
}
#endif