/// <summary>
/// Synchronize MicroVerse Splines with KWS.
/// 
/// Requirement: Create scripting define symbol: USING_KWS
/// 
/// This component requires KWS and the Unity Splines package and MicroVerse Splines in your project.
/// 
/// Usage: 
/// + create a Unity Spline, reset Transform
/// + add the Spline Path component to the Unity Spline
/// + add this script to the Unity Spline
/// + create a KWS river spline (Effects > Water System and then Water System > Mesh > Render Mode > River) outside of the MicroVerse gameobject, reset Transform
/// + drag the spline gameobject and the river gameobject into the dedicated slots of the sync spline inspector
/// + modify the spline, the river will adjust accordingly
/// 
/// + note: width is not implemented yet
/// 
/// </summary>
namespace Rowlan.Genesis.Integrations.KriptoWaterSystem
{
#if USING_KWS

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

	using System.Collections.Generic;
    using RiverSpline = KWS.WaterSystem;
    using UnityEngine.UIElements;

    public class SyncKwsSpline : SyncRiverSpline
    {
        [Tooltip("The river spline")]
        public RiverSpline riverSpline;

#if UNITY_EDITOR
        [CustomEditor(typeof(SyncKwsSpline))]
        public class SyncKwsSplineEditor : SyncRiverSplineEditor
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

            public SyncKwsSpline GetEditorTarget()
            {
                return ((SyncKwsSpline)target);
            }

            public override void OnInspectorGUI()
            {
                EditorGUILayout.HelpBox("KWS River Synchronization (Experimental)", MessageType.None);

                if (GetEditorTarget().useWidth)
                {
                    EditorGUILayout.HelpBox("Use Width is enabled, but it's not implemented yet. The feature is still experimental. Please disable Use Width.", MessageType.Error);
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
                // Vector4 offset = base.editorTarget.transform == riverSpline.transform ? Vector4.zero : -riverSpline.transform.position;
                // note: not required for KWS
                Vector4 offset = Vector4.zero;

                KWS.SplineScriptableData data = riverSpline.Settings.SplineScriptableData;

                if( data == null)
                {
                    data = ScriptableObject.CreateInstance<KWS.SplineScriptableData>();
                    riverSpline.Settings.SplineScriptableData = data;
                }

                // clear
                data.Splines.Clear();

                KWS.SplineScriptableData.Spline spline = new KWS.SplineScriptableData.Spline();
                data.Splines.Add( spline);

                foreach (Vector4 worldPosition in worldPositions)
                {
                    Vector3 position = worldPosition + offset;

                    var splineID = spline.ID;
                    var newPoint = new KWS.SplineScriptableData.SplinePoint(splineID, position);

                    newPoint.Width += editorTarget.additionalWidth;

                    spline.SplinePoints.Add( newPoint);
                    
                }

                riverSpline.Settings.SplineScriptableData = data;
                riverSpline.ForceUpdateWaterSettings();// WaterTab.Mesh);

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

                KWS.SplineScriptableData data = riverSpline.Settings.SplineScriptableData;

                if (data.Splines.Count == 0)
                    return new List<Vector3>();

                KWS.SplineScriptableData.Spline spline = data.Splines[0];

                for (int i = 0; i < spline.SplinePoints.Count; i++)
                {
                    Vector3 position = spline.SplinePoints[i].WorldPosition;

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