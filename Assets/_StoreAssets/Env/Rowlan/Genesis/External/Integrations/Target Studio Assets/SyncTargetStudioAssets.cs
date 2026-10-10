#if UNITY_EDITOR && USING_TARGET_STUDIO_ASSETS

namespace Rowlan.Genesis.Integrations.TargetStudioAssets
{

    using UnityEngine.Splines;
    using UnityEngine;
    using UnityEditor;

    using System.Collections.Generic;
    using JBooth.MicroVerseCore;
    using System.Reflection;
    using UnityEditor.Splines;

    using TS.Generics; // Target Studio
    using System.Linq;

    /// <summary>
    /// Sync script for target studio assets. Convert from unity spline to target studio bezier curves for path and road mesh.
    /// </summary>
    public class SyncTargetStudioAssets : MonoBehaviour
    {
        [Tooltip("Whether automatic syncing the spline is enabled or not")]
        [HideInInspector]
        public bool syncEnabled = false; // disabled for now: invoke only when mouse dragging stopped, otherwise it costs too much performance to generate the mesh

        [Tooltip("Unity spline container")]
        public SplineContainer splineContainer;

        [Space(10)]

        [Tooltip("The checkpoint prefab, eg Assets/RC/Assets/Prefabs/TS/Path/00.prefab")]
        public PathRef pathRef;
        public Path path;
        public Bezier bezierRoad;



        [CustomEditor(typeof(SyncTargetStudioAssets))]
        public class SyncTargetStudioAssetsEditor : Editor
        {
            protected SyncTargetStudioAssets editorTarget;
            protected SyncTargetStudioAssetsEditor editor;

            public SyncTargetStudioAssets GetEditorTarget()
            {
                return ((SyncTargetStudioAssets)target);
            }

            public void OnEnable()
            {
                editor = this;
                editorTarget = (SyncTargetStudioAssets)target;

                // install listeners
                EditorSplineUtility.AfterSplineWasModified += OnSplineModified;
            }

            public void OnDisable()
            {
                // uninstall listeners
                EditorSplineUtility.AfterSplineWasModified -= OnSplineModified;
            }

            void OnSplineModified(Spline spline)
            {
                if (!editorTarget.syncEnabled)
                    return;

                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                
                CreatePathSpline();
                CreateBezierRoadMesh();
            }

            public override void OnInspectorGUI()
            {
                EditorGUILayout.HelpBox("Create path and road mesh for Target Studio assets. This will change your existing setup, ensure you have a backup", MessageType.Info);

                serializedObject.Update();

                EditorGUILayout.PrefixLabel("Settings");

                base.OnInspectorGUI();

                bool pathValid = GetEditorTarget().pathRef != null
                    && GetEditorTarget().path != null
                    && GetEditorTarget().splineContainer != null;

                bool bezierValid = GetEditorTarget().bezierRoad != null
                    && GetEditorTarget().splineContainer != null;

                EditorGUILayout.Space(10);

                EditorGUILayout.PrefixLabel("Unity");

                GUI.enabled = pathValid;
                {
                    if (GUILayout.Button(new GUIContent("Create Unity Spline", "Create a Unity spline from the path")))
                    {
                        CreateUnitySpline();
                    }
                }
                GUI.enabled = true;

                EditorGUILayout.PrefixLabel("Target Studio");

                GUI.enabled = pathValid;
                {
                    if (GUILayout.Button(new GUIContent("Create Path Spline", "Create a path spline from a Unity spline")))
                    {
                        CreatePathSpline();
                    }
                }
                GUI.enabled = true;

                GUI.enabled = bezierValid;
                {
                    if (GUILayout.Button(new GUIContent("Create Road Mesh", "Create bezier road mesh from a Unity spline")))
                    {
                        CreateBezierRoadMesh();
                    }
                }
                GUI.enabled = true;

                EditorGUILayout.PrefixLabel("Setup");

                if (GUILayout.Button( new GUIContent("Auto Setup", "Try filling the inspector's data by using heuristics")))
                {
                    AutoSetup();
                }

                serializedObject.ApplyModifiedProperties();
            }

            public List<Vector3> GetWorldPositions()
            {
                Path path = GetEditorTarget().path;

                if (path == null)
                    return new List<Vector3>();

                // consider path gameobject transform in case it's different to the spline transform
                Vector3 offset = -path.transform.position;

                List<Vector3> worldPositions = new List<Vector3>();

                for (int i = 0; i < path.transform.childCount; i++)
                {
                    Vector3 position = path.transform.GetChild(i).transform.position;

                    // consider offset, eg path gameobject transform in case it's different to the spline transform
                    position += offset;

                    position = path.transform.localToWorldMatrix.MultiplyPoint(position);

                    worldPositions.Add(position);
                }

                return worldPositions;
            }

            public void CreateUnitySpline()
            {
                SplineContainer splineContainer = GetEditorTarget().splineContainer;
                List<Vector3> worldPositions = GetWorldPositions();

                if (splineContainer == null)
                    return;

                // undo
                Undo.RecordObject(splineContainer, "Create Unity Spline");

                Spline unitySpline = splineContainer.Spline;

                if (unitySpline == null)
                    unitySpline = new Spline();
                else
                    unitySpline.Clear();

                foreach (Vector3 position in worldPositions)
                {
                    BezierKnot knot = new BezierKnot(splineContainer.transform.worldToLocalMatrix.MultiplyPoint(position));

                    unitySpline.Add(knot, TangentMode.AutoSmooth);
                }

                splineContainer.Spline = unitySpline;

                EditorUtility.SetDirty(splineContainer);
                MicroVerse.instance?.Invalidate();

                // update the road system
                RoadSystem roadSystem = GetEditorTarget().GetComponent<RoadSystem>();
                if(roadSystem != null)
                {
                    Road[] roads = FindObjectsOfType<Road>();
                    foreach (var r in roads)
                    {
                        r.UpdateConnections(r.GetComponentInParent<RoadSystem>(), true);
                    }
                    roadSystem.ReGenerateRoads();
                    roadSystem.UpdateAll();
                }
            }

            public void CreatePathSpline()
            {
                Path trackPath = GetEditorTarget().path;

                SplineContainer splineContainer = GetEditorTarget().splineContainer;

                if (splineContainer == null)
                    return;

                if (trackPath == null)
                    return;

                // undo
                Undo.RegisterFullObjectHierarchyUndo(trackPath.gameObject, "Create Path Spline");

                // remove all transform children (checkpoints)
                for (int i = trackPath.transform.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(trackPath.transform.GetChild(i).gameObject);
                }

                // clear checkpoints
                trackPath.checkpoints.Clear();

                foreach (BezierKnot knot in splineContainer.Spline)
                {
                    Vector3 point = splineContainer.transform.TransformPoint(knot.Position); // to world space
                    Vector3 localPos = trackPath.transform.InverseTransformPoint(point); // to track path local space

                    GameObject newCheckpoint = PrefabUtility.InstantiatePrefab(GetEditorTarget().pathRef.prefabCheckpoint, trackPath.transform) as GameObject;
                    newCheckpoint.transform.localPosition = localPos;

                    trackPath.checkpoints.Add(newCheckpoint.transform);
                }

            }

            /// <summary>
            /// Update the bezier road mesh
            /// </summary>
            /// <param name="bezierRoad"></param>
            /// <param name="trackPath"></param>
            /// <param name="splineContainer"></param>
            /// <param name="worldPositions"></param>
            public void CreateBezierRoadMesh()
            {
                Bezier bezierRoad = GetEditorTarget().bezierRoad;
                SplineContainer splineContainer = GetEditorTarget().splineContainer;

                if (splineContainer == null)
                    return;

                if (bezierRoad == null)
                    return;

                // undo
                Undo.RegisterFullObjectHierarchyUndo(bezierRoad.gameObject, "Create Bezier Road");

                List<PointDescription> pointsList = new List<PointDescription>();

                int curveCount = splineContainer.Spline.GetCurveCount();
                for(  int i=0; i < curveCount; i++)
                {
                    BezierCurve curve = splineContainer.Spline.GetCurve(i);

                    // P1 and P3 are identical
                    if (i == 0)
                        pointsList.Add(new PointDescription(TransformPoint(curve.P0, splineContainer, bezierRoad), Quaternion.identity));

                    pointsList.Add(new PointDescription(TransformPoint(curve.P1, splineContainer, bezierRoad), Quaternion.identity));

                    pointsList.Add(new PointDescription(TransformPoint(curve.P2, splineContainer, bezierRoad), Quaternion.identity));
                    pointsList.Add(new PointDescription(TransformPoint(curve.P3, splineContainer, bezierRoad), Quaternion.identity));
                }

                bezierRoad.pointsList = pointsList;

                UpdateRoadMesh(bezierRoad, splineContainer);
            }

            /// <summary>
            /// Transform point from spline container space to bezier road space
            /// </summary>
            /// <param name="point"></param>
            /// <param name="splineContainer"></param>
            /// <param name="bezierRoad"></param>
            /// <returns></returns>
            private Vector3 TransformPoint( Vector3 point, SplineContainer splineContainer, Bezier bezierRoad)
            {
                point = splineContainer.transform.TransformPoint(point); // to world space
                point = bezierRoad.transform.InverseTransformPoint(point); // to bezier local space
                return point;
            }

            /// <summary>
            /// Update the road mesh via reflection
            /// </summary>
            /// <param name="bezierRoad"></param>
            /// <param name="trackPath"></param>
            /// <param name="splineContainer"></param>
            public void UpdateRoadMesh( Bezier bezierRoad, SplineContainer splineContainer)
            {
                if (bezierRoad == null)
                    return;

                // see BezierEditor.cs
                int knotCount = bezierRoad.pointsList.Count / 3;

                RoadMeshGen.ReturnTotalCurveDistance(bezierRoad);
                Editor editor = Editor.CreateEditor(bezierRoad);
                BezierEditor bezierEditor = (BezierEditor) editor;

                // determine editor
                System.Type editorType = editor.GetType();

                // get method info
                MethodInfo updateDataMethod = editorType.GetMethod("UpdateRoad", BindingFlags.NonPublic | BindingFlags.Instance);

                if (updateDataMethod == null)
                {
                    EditorUtility.DisplayDialog("Error", "Protected method 'UpdateRoad' not found.", "OK");
                    return;
                }

                // invoke the protected method
                updateDataMethod.Invoke(editor, new object[] { 0, knotCount, true, true, null, null });

                // update inspector
                EditorUtility.SetDirty(bezierRoad);
                Repaint();
            }

            /// <summary>
            /// Try filling in the inspector with required data
            /// </summary>
            private void AutoSetup()
            {
                if (GetEditorTarget().pathRef == null)
                    GetEditorTarget().pathRef = FindObjectOfType<PathRef>();

                if (GetEditorTarget().path == null)
                    GetEditorTarget().path = FindObjectOfType<Path>();

                if (GetEditorTarget().bezierRoad == null)
                    GetEditorTarget().bezierRoad = FindObjectOfType<Bezier>();

                if (GetEditorTarget().splineContainer == null)
                    GetEditorTarget().splineContainer = GetEditorTarget().transform.GetComponent<SplineContainer>();
            }
        }
    }
}
#endif
