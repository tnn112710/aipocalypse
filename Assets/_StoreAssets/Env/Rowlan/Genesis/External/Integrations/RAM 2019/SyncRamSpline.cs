/// <summary>
/// Synchronize MicroVerse Splines with River Auto Material.
/// 
/// This component requires RAM 2019 and the Unity Splines package and MicroVerse Splines in your project.
/// 
/// Usage: 
/// + activate the script by adding the scripting define symbol USING_RAM2019
/// + create a Unity spline
/// + add this script to the Spline
/// + create a RAM river
/// + drag the spline and the river into the dedicated slots of the inspector
/// + modify the spline, the river will adjust accordingly
/// + simple and advanced version. the advanced version uses width (must have with set on the spline, otherwise river will appear empty)
/// </summary>
namespace Rowlan.Genesis.Integrations.RAM2019
{
#if USING_RAM2019

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

	using System.Collections.Generic;
    using RiverSpline = RamSpline;

    public class SyncRamSpline : SyncRiverSpline
    {
        [Tooltip("The river spline")]
        public RiverSpline riverSpline;

#if UNITY_EDITOR
        [CustomEditor(typeof(SyncRamSpline))]
        public class SyncRamSplineEditor : SyncRiverSplineEditor
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

            public SyncRamSpline GetEditorTarget()
            {
                return ((SyncRamSpline)target);
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
                riverSpline.RemovePoints();

                // add points
                foreach (Vector4 worldPosition in worldPositions)
                {
                    Vector4 point = worldPosition;

                    // consider offset, eg river gameobject transform in case it's different to the spline transform
                    point += offset;

                    riverSpline.AddPoint(point);
                }

                EditorUtility.SetDirty(riverSpline);
                riverSpline.GenerateSpline();

            }

            public override List<Vector3> GetRiverWorldPositions()
            {
                RiverSpline riverSpline = GetEditorTarget().riverSpline;

                if (riverSpline == null)
                    return new List<Vector3>();

                // consider river gameobject transform in case it's different to the spline transform
                Vector3 offset = base.editorTarget.transform == riverSpline.transform ? Vector3.zero : -riverSpline.transform.position;

                List<Vector3> worldPositions = new List<Vector3>();

                foreach (Vector4 riverPoint in riverSpline.controlPoints)
                {
                    Vector3 position = new Vector3(riverPoint.x, riverPoint.y, riverPoint.z);

                    // consider offset, eg river gameobject transform in case it's different to the spline transform
                    position += offset;

                    position = riverSpline.transform.localToWorldMatrix.MultiplyPoint(position);

                    worldPositions.Add(position);

                }

                return worldPositions;
            }

        }
#endif
    }
#endif
}