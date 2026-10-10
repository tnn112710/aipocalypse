/// <summary>
/// Synchronize EasyRoads3D roads with MicroVerse Splines.
/// 
/// This component requires EasyRoads3D and the Unity Splines package and MicroVerse Splines in your project.
/// 
/// Notes:
/// 
/// + the unity splines and easy roads splines differ a bit in shape. you may need to add additional unity spline knots for better alignment
/// 
/// Usage: 
/// 
/// + create a road network with a road (is of type modular road)
/// + create a unity spline
/// + add this script to the spline
///   spline and the first road from the Road Network is assigned automatically and the road is created along the spline
/// + modify the spline, the road will adjust accordingly
/// </summary>
namespace Rowlan.Genesis.Integrations.EasyRoads3D
{
#if EASYROADS3D_PRO

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

    using UnityEngine.Splines;
	using JBooth.MicroVerseCore;
	using System.Collections.Generic;
    using EasyRoads3Dv3;

    public class SyncEasyRoadsSpline : MonoBehaviour
    {
        [Header("EasyRoads3D Integration (v0.9 Beta)")]
        [Space]

        [Tooltip("Whether automatic syncing the road with the spline is enabled or not. Use manual update in case you have performance issues")]
        public bool syncEnabled = true;

        [Tooltip("The EasyRoads3D road from the road network")]
        public ERModularRoad modularRoad;

        /// <summary>
        /// The MicroVerse spline path.
        /// The spline path is required because it stores the widths of the spline.
        /// The width isn't in the container nor in the container's spline
        /// </summary>
        [Tooltip("The Unity / MicroVerse spline path to be used")]
        public JBooth.MicroVerseCore.SplinePath splinePath;

        [Tooltip("The offset in Y position to the points on the spline curve")]
        public float offsetY = 0.2f;

#if UNITY_EDITOR
        [CustomEditor(typeof(SyncEasyRoadsSpline))]
        public class SyncEasyRoadsSplineEditor : Editor
        {
            private GUIContent UpdateButtonContent = EditorGUIUtility.TrTextContent("Manual Update", "Force manual update");

            SyncEasyRoadsSpline editorTarget;
            SyncEasyRoadsSplineEditor editor;

            ERRoadNetwork erRoadNetwork;

            public void OnEnable()
            {
                editor = this;
                editorTarget = (SyncEasyRoadsSpline)target;

                erRoadNetwork = new ERRoadNetwork();

                // auto-detect objects, get first road
                if (editorTarget.modularRoad == null)
                {
                    editorTarget.modularRoad = FindObjectOfType(typeof(ERModularRoad)) as ERModularRoad;
                }

                // get the spline of the gameobject in which this script is located
                if( editorTarget.splinePath == null)
                {
                    editorTarget.splinePath = editorTarget.GetComponent<JBooth.MicroVerseCore.SplinePath>();
                }

                // initially create the road
                if (editorTarget.syncEnabled)
                {
                    CreateRoad();
                }

                // spline event handler
                Spline.Changed += OnSplineChanged;

            }

            public void OnDisable()
            {
                Spline.Changed -= OnSplineChanged;
            }

            void OnSplineChanged(Spline spline, int knotIndex, SplineModification modificationType)
            {

                if( modificationType == SplineModification.KnotModified)
                {
                    UpdateRoad(knotIndex);
                }
                else
                {
                    CreateRoad();
                }
                
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

                EditorGUILayout.HelpBox("Create a road along a spline path. For better alignment add multiple knots. Adjust the width of height, trench, tree clearing and detail clearing on the spline path", MessageType.Info);

                using var changeScope = new EditorGUI.ChangeCheckScope();
                {
                    // consistency check
                    if( GetSplineCount() > 1)
                    {
                        EditorGUILayout.HelpBox("Only 1 spline supported", MessageType.Warning);
					}

                    base.DrawDefaultInspector();

                    if (GUILayout.Button( UpdateButtonContent))
                    {
                        CreateRoad( true);
                    }
                }

                if (changeScope.changed)
                {
                    CreateRoad();
                }

                serializedObject.ApplyModifiedProperties();
            }

            private void CreateRoad( bool forceCreation = false)
            {
                if (CheckCreateRoad( forceCreation, out Spline unitySpline, out ERRoad road))
                {
                    float offsetY = editorTarget.offsetY;

                    road.Clear();

                    List<Vector3> points = new List<Vector3>();

                    Matrix4x4 worldMatrix = editorTarget.transform.localToWorldMatrix;

                    foreach (BezierKnot knot in unitySpline.Knots)
                    {
                        Vector3 point = worldMatrix.MultiplyPoint(new Vector3(knot.Position.x, knot.Position.y + offsetY, knot.Position.z));

                        points.Add(point);
                    }

                    road.AddMarkers(points.ToArray());

                    road.Refresh();

                    MicroVerse.instance?.Invalidate();
                }
            }

            private bool CheckCreateRoad( bool forceCreation, out Spline unitySpline, out ERRoad road)
            {
                unitySpline = editorTarget.splinePath?.spline?.Spline;
                road = editorTarget.modularRoad == null ? null : erRoadNetwork.GetRoadByGameObject(editorTarget.modularRoad.gameObject);

                if (!editorTarget.syncEnabled && !forceCreation)
                    return false;

                if (editorTarget.modularRoad == null)
                    return false;

                if (unitySpline == null || road == null)
                    return false;

                return true;
            }

            private void UpdateRoad( int knotIndex, bool forceCreation = false)
            {
                if (CheckUpdateRoad(knotIndex, forceCreation, out Spline unitySpline, out ERRoad road))
                {
                    float offsetY = editorTarget.offsetY;

                    Matrix4x4 worldMatrix = editorTarget.transform.localToWorldMatrix;

                    BezierKnot knot = unitySpline[knotIndex];

                    Vector3 point = worldMatrix.MultiplyPoint(new Vector3(knot.Position.x, knot.Position.y + offsetY, knot.Position.z));

                    road.SetMarkerPosition(knotIndex, point);

                    //road.Refresh();

                    MicroVerse.instance?.Invalidate();
                }
            }

            private bool CheckUpdateRoad(int knotIndex, bool forceCreation, out Spline unitySpline, out ERRoad road)
            {
                unitySpline = editorTarget.splinePath?.spline?.Spline;
                road = editorTarget.modularRoad == null ? null : erRoadNetwork.GetRoadByGameObject(editorTarget.modularRoad.gameObject);

                if (!editorTarget.syncEnabled && !forceCreation)
                    return false;

                if (editorTarget.modularRoad == null)
                    return false;

                if (unitySpline == null || road == null)
                    return false;

                if (knotIndex > unitySpline.Count)
                {
                    Debug.LogError($"Invalid knot index {knotIndex} > {unitySpline.Count}");
                    return false;
                }

                if (unitySpline.Count != road.GetMarkerCount())
                {
                    Debug.LogError($"Spline count {unitySpline.Count} != Marker count {road.GetMarkerCount()}");
                    return false;
                }

                return true;

            }
        }
#endif
    }
#endif
}