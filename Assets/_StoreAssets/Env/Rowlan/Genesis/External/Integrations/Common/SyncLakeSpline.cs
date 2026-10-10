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

    /// <summary>
    /// Base class for lake design
    /// </summary>
    public class SyncLakeSpline : MonoBehaviour
    {
        [Tooltip("Whether syncing the spline with the RAM object is enabled or not. By default disabled, lakes can have lots of points which cost performance when the RAM mesh is created")]
        public bool syncEnabled = false;

        [Tooltip("The offset in Y position to the points on the spline curve")]
        public float offsetY = 0.2f;

        [Tooltip("Sample in discrete steps or interpolated. Interpolated is beneficial if you use Bezier curves")]
        public Sampling sampling = Sampling.Discrete;

        [Tooltip("Sampling by Interpolation: The knots that are created per meter. 0 = low resolution, 1 = high resolution (more points, higher performance impact)")]
        [Range(0.05f,1f)]
        public float interpolationResolution = 0.2f;

        [Tooltip("Sampling by Interpolation: Epsilon being used for removal of points. Higher values means more points removed, means lower resolution")]
        [Range( 0.01f, 20f)]
        public float reductionEpsilon = 5.00f;

        /// <summary>
        /// The Unity spline container.
        /// In opposite to SyncRamRiver we can use the container here, we don't have to access any width data for the lake.
        /// </summary>
        [Tooltip("The Unity spline container to be used")]
        public SplineContainer splineContainer;

#if UNITY_EDITOR
        [CustomEditor(typeof(SyncLakeSpline))]
        public abstract class SyncLakeSplineEditor : Editor
        {
            private GUIContent UpdateButtonContent = EditorGUIUtility.TrTextContent("Update", "Update manually");
            private GUIContent AdjustHeightsButtonContent = EditorGUIUtility.TrTextContent("Adjust Lake Heights", "Ensure knots have same height");
            private GUIContent CreateUnitySplineContent = EditorGUIUtility.TrTextContent("Create Unity Spline from River", "Set Unity spline from river spline. Active only if sync is not enabled");

            SyncLakeSpline editorTarget;
            SyncLakeSplineEditor editor;

            public void OnEnable()
            {
                editor = this;
                editorTarget = (SyncLakeSpline)target;

                // install listeners
                Spline.Changed += OnSplineChanged;
                EditorSplineUtility.RegisterSplineDataChanged<float>(OnSplineDataChanged);

                // auto-detect objects


                if (editorTarget.splineContainer == null)
                {
                    editorTarget.splineContainer = editorTarget.GetComponent<SplineContainer>();
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
                CreateLakeSpline( false);
            }

            /// <summary>
            /// The data on the spline changed, eg width
            /// </summary>
            /// <param name="splineData"></param>
            void OnSplineDataChanged(SplineData<float> splineData)
            {
                CreateLakeSpline(false);
            }

            private int GetSplineCount()
            {
                if (editorTarget.splineContainer == null || editorTarget.splineContainer.Spline == null)
                    return 0;

                return editorTarget.splineContainer.Splines.Count;

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
                        CreateLakeSpline( true);
                    }

                    EditorGUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button(AdjustHeightsButtonContent))
                        {
                            AdjustLakeHeights();
                            CreateLakeSpline( true);
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    // create from lake is only enabled if sync is disabled
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
                    CreateLakeSpline( false);
                }

                serializedObject.ApplyModifiedProperties();
            }

            /// <summary>
            /// Get the main spline from the MicroVerse object
            /// </summary>
            /// <returns></returns>
            private Spline GetMainSpline()
            {
                return editorTarget.splineContainer?.Spline;
			}

            public bool PerformInputParameterCheck()
            {
                if (GetMainSpline() == null)
                    return false;

                return true;
            }

            /// <summary>
            /// Create the spline eg simple or advanced (with width)
            /// </summary>
            public void CreateLakeSpline(bool force)
            {
                if (!PerformInputParameterCheck())
                    return;

                if (!editorTarget.syncEnabled && !force)
                    return;

                List<Vector4> worldPositions = GetWorldPositions();

                CreateRiverSpline(worldPositions);

                MicroVerse.instance?.Invalidate();

            }

            private List<Vector4> GetWorldPositions()
            {
                Spline unitySpline = GetMainSpline();

                if (unitySpline == null)
                    return new List<Vector4>();

                float offsetY = editorTarget.offsetY;

                // we operate on the normalized position on the spline which is t = [0..1]
                List<float> tNormalizedList = SplinePathExtensions.GetNormalizedT(editorTarget.splineContainer, 0, editorTarget.sampling, editorTarget.interpolationResolution);

                // we have a list of t from [0,1] for the entire spline => now sort it
                tNormalizedList.Sort();

                // convert normalized t to local positions
                List<float3> splinePoints = SplinePathExtensions.GetLocalPositions(unitySpline, tNormalizedList);

                // reduce spline points
                List<float3> reducedSplinePoints = SplineUtility.ReducePoints(splinePoints, editorTarget.reductionEpsilon);

                // convert local points to world points
                List<Vector4> worldPositions = SplinePathExtensions.GetWorldPositions(editorTarget.splineContainer.transform, reducedSplinePoints, offsetY);

                return worldPositions;
            }

            /// <summary>
            /// Ensure the y position of all knots is the same (minimum y position of all knots)
            /// </summary>
            private void AdjustLakeHeights()
            {
                SplinePathExtensions.AdjustLakeHeights(editorTarget.splineContainer);
            }

            /// <summary>
            /// Create a unity spline from a spline.
            /// Useful if you auto-generate a lake in eg RAM and want to work with that one.
            /// </summary>
            private void CreateUnitySpline()
            {
                SplineContainer splineContainer = editorTarget.splineContainer;
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