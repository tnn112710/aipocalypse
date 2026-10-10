namespace Rowlan.Genesis.Integrations
{
#if USING_RAM2019 || USING_RAM3 || CREST_OCEAN || CREST_5 || USING_KWS

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Splines;
using static Rowlan.Genesis.Integrations.SplinePathExtensions;
#endif

    using UnityEngine.Splines;
	using JBooth.MicroVerseCore;
	using System.Collections.Generic;
	using Unity.Mathematics;
    using static JBooth.MicroVerseCore.SplinePath;

    /// <summary>
    /// Base class for river design
    /// </summary>
    public class SyncRiverSpline : MonoBehaviour
    {

        [Tooltip("Whether syncing the spline with the river is enabled or not")]
        public bool syncEnabled = true;

        [Tooltip("The offset in Y position to the points on the spline curve")]
        public float offsetY = 0.2f;

        [Tooltip("Adjust width in the spline creation. Width must be set on the spline, otherwise it may appear empty")]
        public bool useWidth = false;

        [Tooltip("Width to add additionally to the spline width")]
        public float additionalWidth = 1f;

        [Tooltip("Sample in discrete steps or interpolated. Interpolated is beneficial if you use Bezier curves")]
        public Sampling sampling = Sampling.Discrete;

        [Tooltip("The knots that are created per meter. 0 = low resolution, 1 = high resolution (more points, higher performance impact)")]
        [Range(0.05f,1f)]
        public float interpolationResolution = 0.2f;

        [Tooltip("Sampling by Interpolation: Epsilon being used for removal of points. Higher values means more points removed, means lower resolution")]
        [Range(0.01f, 20f)]
        public float reductionEpsilon = 1.00f;

        /// <summary>
        /// The MicroVerse spline path.
        /// The spline path is required because it stores the widths of the spline.
        /// The width isn't in the container nor in the container's spline
        /// </summary>
        [Tooltip("The Unity / MicroVerse spline to be used")]
        public JBooth.MicroVerseCore.SplinePath splinePath;

#if UNITY_EDITOR
        [CustomEditor(typeof(SyncRiverSpline))]
        public abstract class SyncRiverSplineEditor : Editor
        {
            private GUIContent UpdateButtonContent = EditorGUIUtility.TrTextContent("Update", "Update manually");
            private GUIContent AdjustHeightsButtonContent = EditorGUIUtility.TrTextContent("Adjust Flow Heights", "Ensure successive spline knot y position is equal or lower than previous one");
            private GUIContent AdjustWidthsButtonContent = EditorGUIUtility.TrTextContent("Adjust Flow Widths", "Ensure successive spline width is equal or wider than previous one (eg for Lava flow)");
            private GUIContent CreateUnitySplineContent = EditorGUIUtility.TrTextContent("Create Unity Spline from River", "Set Unity spline from river spline. Active only if sync is not enabled");

            protected SyncRiverSpline editorTarget;
            protected SyncRiverSplineEditor editor;

            public void OnEnable()
            {
                editor = this;
                editorTarget = (SyncRiverSpline)target;

                // install listeners
                Spline.Changed += OnSplineChanged;
                EditorSplineUtility.RegisterSplineDataChanged<float>(OnSplineDataChanged);

                if (editorTarget.splinePath == null)
                {
                    editorTarget.splinePath = editorTarget.GetComponent<JBooth.MicroVerseCore.SplinePath>();
                }
            }

            public void OnDisable()
            {
                // uninstall listeners
                Spline.Changed -= OnSplineChanged;
                EditorSplineUtility.UnregisterSplineDataChanged<float>(OnSplineDataChanged);

            }

            /// <summary>
            /// The spline changed
            /// </summary>
            /// <param name="spline"></param>
            /// <param name="knotIndex"></param>
            /// <param name="modificationType"></param>
            void OnSplineChanged(Spline spline, int knotIndex, SplineModification modificationType)
            {
                CreateRiverSpline(false);
            }

            /// <summary>
            /// The data on the spline changed, eg width
            /// </summary>
            /// <param name="splineData"></param>
            void OnSplineDataChanged(SplineData<float> splineData)
            {
                CreateRiverSpline(false);
            }

            private int GetSplineCount()
            {
                if (editorTarget.splinePath == null || editorTarget.splinePath.spline == null)
                    return 0;

                return editorTarget.splinePath.spline.Splines.Count;

            }
            public override void OnInspectorGUI()
            {
                serializedObject.Update();

                using var changeScope = new EditorGUI.ChangeCheckScope();
                {
                    // consistency check
                    if( GetSplineCount() > 1)
                    {
                        EditorGUILayout.HelpBox("Only 1 spline supported", MessageType.Warning);
					}

                    // use width: info about editing performance
                    if (editorTarget.sampling == Sampling.Interpolate)
                    {
                        EditorGUILayout.HelpBox("Interpolation sampling is enabled. Realtime editing performance depends on the sampling settings.", MessageType.None);
                    }

                    base.DrawDefaultInspector();

                    if (GUILayout.Button( UpdateButtonContent))
                    {
                        CreateRiverSpline(true);
                    }

                    EditorGUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button(AdjustHeightsButtonContent))
                        {
                            AdjustFlowHeights();
                            CreateRiverSpline(true);
                        }

                        if (GUILayout.Button(AdjustWidthsButtonContent))
                        {
                            AdjustFlowWidths();
                            CreateRiverSpline(true);
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    // create from river is only enabled if sync is disabled
                    bool disabled = editorTarget.syncEnabled == true;
                    EditorGUI.BeginDisabledGroup(disabled);
                    {
                        if (GUILayout.Button(CreateUnitySplineContent))
                        {
                            CreateUnitySpline();
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                }

                if (changeScope.changed)
                {
                    CreateRiverSpline(false);
                }

                serializedObject.ApplyModifiedProperties();
            }

            /// <summary>
            /// Get the main spline from the MicroVerse object
            /// </summary>
            /// <returns></returns>
            private Spline GetMainSpline()
            {
                return editorTarget.splinePath?.spline?.Spline;
			}

            public bool PerformInputParameterCheck()
            {
                if (GetMainSpline() == null)
                    return false;

                return true;
            }

            /// <summary>
            /// Create the river spline eg simple or advanced (with width)
            /// </summary>
            public void CreateRiverSpline(bool force)
            {
                if (!PerformInputParameterCheck())
                    return;

                if(editorTarget.useWidth)
                {
                    CreateRiverSplineAdvanced( force);
                }
                else
                {
                    CreateRiverSplineSimple( force);
				}

                MicroVerse.instance?.Invalidate();

            }

            /// <summary>
            /// River spline without width
            /// </summary>
            private void CreateRiverSplineSimple(bool force)
            {
                if (!editorTarget.syncEnabled && !force)
                    return;
                
                // Get the spline points in world space
                List<Vector4> worldPositions = GetWorldPositions();

                CreateRiverSpline(worldPositions);
            }

            /// <summary>
            /// River spline without width
            /// </summary>
            private void CreateRiverSplineAdvanced(bool force)
            {
                if (!editorTarget.syncEnabled && !force)
                    return;

                List<Vector4> worldPositions = GetWorldPositionsAdvanced();

                CreateRiverSpline(worldPositions);
            }

            private List<Vector4> GetWorldPositions()
            {
                Spline unitySpline = GetMainSpline();

                if (unitySpline == null)
                    return new List<Vector4>();

                float offsetY = editorTarget.offsetY;

                // we operate on the normalized position on the spline which is t = [0..1]
                List<float> tNormalizedList = SplinePathExtensions.GetNormalizedT(editorTarget.splinePath.spline, 0, editorTarget.sampling, editorTarget.interpolationResolution);

                // we have a list of t from [0,1] for the entire spline => now sort it
                tNormalizedList.Sort();

                // convert normalized t to local positions
                List<float3> splinePoints = SplinePathExtensions.GetLocalPositions(unitySpline, tNormalizedList);

                // reduce spline points
                List<float3> reducedSplinePoints = SplineUtility.ReducePoints(splinePoints, editorTarget.reductionEpsilon);

                // convert local points to world points
                List<Vector4> worldPositions = SplinePathExtensions.GetWorldPositions(editorTarget.splinePath.transform, reducedSplinePoints, offsetY);

                return worldPositions;
            }

            /// <summary>
            /// World positions including width
            /// </summary>
            /// <returns></returns>
            private List<Vector4> GetWorldPositionsAdvanced()
            {
                Spline unitySpline = GetMainSpline();

                if (unitySpline == null)
                    return new List<Vector4>();

                float offsetY = editorTarget.offsetY;


                // we operate on the normalized position on the spline which is t = [0..1]
                List<float> tNormalizedList = SplinePathExtensions.GetNormalizedT(editorTarget.splinePath?.spline, 0, editorTarget.sampling, editorTarget.interpolationResolution);

                // add width indexes to normalized positions
                List<SplineWidthData> splineWidthDatas = editorTarget.splinePath.splineWidths;

                for (int i = 0; i < splineWidthDatas.Count; i++)
                {
                    SplineData<float> splineData = splineWidthDatas[i].widthData;

                    for (int j = 0; j < splineData.Count; j++)
                    {
                        DataPoint<float> dataPoint = splineData[j];

                        // width data indexes are already normalized, we can use them as they are
                        tNormalizedList.Add(dataPoint.Index);
                    }
                }

                // we have a list of t from [0,1] for the entire spline => now sort it
                tNormalizedList.Sort();

                Matrix4x4 worldMatrix = editorTarget.transform.localToWorldMatrix;

                List<Vector4> worldPositions = new List<Vector4>();

                // evaluate t, consider width
                for (int splineDataIndex = 0; splineDataIndex < splineWidthDatas.Count; splineDataIndex++)
                {
                    SplineData<float> splineData = splineWidthDatas[splineDataIndex].widthData;

                    for (int i = 0; i < tNormalizedList.Count; i++)
                    {
                        // get normalized index
                        float tNormalized = tNormalizedList[i];

                        // get position
                        float3 position = SplineUtility.EvaluatePosition(unitySpline, tNormalized);

                        // get width
                        float width = splineData.Evaluate(unitySpline, tNormalized, new UnityEngine.Splines.Interpolators.LerpFloat());

                        // adjust width
                        width *= 2f;
                        width += editorTarget.additionalWidth;

                        // create river point
                        Vector3 point = worldMatrix.MultiplyPoint(new Vector3(position.x, position.y + offsetY, position.z));

                        worldPositions.Add(new Vector4(point.x, point.y, point.z, width));
                    }
                }

                return worldPositions;
            }

            /// <summary>
            /// Ensure the y position of the knots goes down in successive order
            /// </summary>
            private void AdjustFlowHeights()
            {
                SplinePathExtensions.AdjustFlowHeights(editorTarget.splinePath);
            }

            /// <summary>
            /// Ensure successive spline width is equal or wider than previous one (eg for Lava flow)
            /// </summary>
            private void AdjustFlowWidths()
            {
                SplinePathExtensions.AdjustFlowWidths(editorTarget.splinePath);
            }

            /// <summary>
            /// Create a unity spline from a river spline.
            /// Useful if you auto-generate a river in river and want to work with that one.
            /// </summary>
            private void CreateUnitySpline()
            {
                SplineContainer splineContainer = editorTarget.splinePath?.spline;
                List<Vector3> worldPositions = GetRiverWorldPositions();

                SplinePathExtensions.CreateUnitySpline(splineContainer, worldPositions);
            }

            /// <summary>
            /// Map the spline points to river points
            /// </summary>
            /// <param name="worldPositions"></param>
            public abstract void CreateRiverSpline(List<Vector4> worldPositions);

            /// <summary>
            /// Get world positions of the spline in order to create a unity spline from it
            /// </summary>
            /// <returns></returns>
            public abstract List<Vector3> GetRiverWorldPositions();

        }
#endif
    }
#endif
}