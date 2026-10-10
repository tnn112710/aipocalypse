#if CREST_5
using WaveHarmonic.Crest.Splines;
#endif

/// <summary>
/// Synchronize MicroVerse Splines with Crest.
/// 
/// This component requires Crest and the Unity Splines package and MicroVerse Splines in your project.
/// 
/// Usage: 
/// + create a Crest river spline
/// + add this script to the Crest river spline
/// + add a microverse spline path component to the Crest river
/// + drag the spline and the river into the dedicated slots of the inspector
/// + modify the spline, the river will adjust accordingly
/// 
/// + note: width is prepared, but not supported yet. Crest doesn't provide a public api for that yet
/// 
/// </summary>
namespace Rowlan.Genesis.Integrations.Crest
{
#if CREST_5

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

	using System.Collections.Generic;
    using RiverSpline = Spline;
    using UnityEngine.UIElements;

    public class SyncCrestSpline : SyncRiverSpline
    {
        [Tooltip("The river spline")]
        public RiverSpline riverSpline;

#if UNITY_EDITOR
        [CustomEditor(typeof(SyncCrestSpline))]
        public class SyncCrestSplineEditor : SyncRiverSplineEditor
        {


            public new void OnEnable()
            {
                base.OnEnable();

                // auto-detect objects
                if (GetEditorTarget().riverSpline == null)
                {
                    GetEditorTarget().riverSpline = editorTarget.GetComponentInChildren<RiverSpline>();
                }

                if (GetEditorTarget().riverSpline != null && editorTarget.splinePath != null)
                {
                    base.CreateRiverSpline(GetEditorTarget().syncEnabled);
                }
            }

            public SyncCrestSpline GetEditorTarget()
            {
                return ((SyncCrestSpline)target);
            }

            public override void OnInspectorGUI()
            {
                EditorGUILayout.HelpBox("Crest River Synchronization (Experimental)", MessageType.None);

                if (GetEditorTarget().useWidth)
                {
                    EditorGUILayout.HelpBox("Use Width is enabled, but Crest doesn't have a public API for it yet. Please adjust the widths manually for now.", MessageType.Error);
                }

                base.OnInspectorGUI();
            }

            public new bool PerformInputParameterCheck()
            {
                if (!base.PerformInputParameterCheck())
                    return false;

                if (GetEditorTarget().riverSpline == null)
                    return false;

                return true;
            }

            /// <summary>
            /// Map the spline points to river points
            /// </summary>
            /// <param name="worldPositions"></param>
            public override void CreateRiverSpline(List<Vector4> worldPositions)
            {
                RiverSpline riverSpline = GetEditorTarget().riverSpline;

                // consider river gameobject transform in case it's different to the spline transform
                Vector4 offset = base.editorTarget.transform == riverSpline.transform ? Vector4.zero : -riverSpline.transform.position;

                // clear
                for (int i = riverSpline.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject go = riverSpline.transform.GetChild(i).gameObject;
                    GameObject.DestroyImmediate(go);
                }

                foreach (Vector4 worldPosition in worldPositions)
                {
                    var newPoint = new GameObject();
                    newPoint.name = "SplinePoint";
                    newPoint.AddComponent<SplinePoint>();
                    newPoint.transform.parent = riverSpline.transform;

                    newPoint.transform.position = worldPosition;

                    /* crest has multiplier instead of actual width; besides the property isn't public => commenting that out for now
                    SplinePointData data = newPoint.GetComponent<SplinePointData>();
                    data._radiusMultiplier = worldPosition.w;
                    */
                }

                EditorUtility.SetDirty(riverSpline);

            }

            public override List<Vector3> GetRiverWorldPositions()
            {
                RiverSpline riverSpline = GetEditorTarget().riverSpline;

                if (riverSpline == null)
                    return new List<Vector3>();

                // consider river gameobject transform in case it's different to the spline transform
                Vector3 offset = base.editorTarget.transform == riverSpline.transform ? Vector3.zero : -riverSpline.transform.position;

                List<Vector3> worldPositions = new List<Vector3>();

                for (int i = 0; i < riverSpline.transform.childCount; i++)
                {
                    Vector3 position = riverSpline.transform.GetChild(i).transform.position;

                    // consider offset, eg river gameobject transform in case it's different to the spline transform
                    position += offset;

                    //position = riverSpline.transform.localToWorldMatrix.MultiplyPoint(position);

                    worldPositions.Add(position);
                }

                return worldPositions;
            }

        }
#endif
    }
#endif
}