/// <summary>
/// Synchronize MicroVerse Splines with River Auto Material.
/// 
/// This component requires RAM 2019 and the Unity Splines package in your project.
/// 
/// Usage: 
/// + activate the script by adding the scripting define symbol USING_RAM2019
/// + create a Unity spline, closed
/// + add this script to the Spline
/// + create a RAM lake
/// + drag the spline and the lake into the dedicated slots of the inspector
/// + modify the spline, the lake will adjust accordingly
/// 
/// Note: you probably add occlusion and height stamps to flatten the area and remove vegetation.
///       please keep in mind that the order of those stamps is important, they affect future stamps.
///       see the tooltips of those stamps
/// </summary>
namespace Rowlan.Genesis.Integrations.RAM2019
{
#if USING_RAM2019

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

	using System.Collections.Generic;
    using LakeSpline = LakePolygon;

    public class SyncRamLake : SyncLakeSpline
    {

        [Tooltip("The lake spline")]
        public LakeSpline lakeSpline;

#if UNITY_EDITOR
        [CustomEditor(typeof(SyncRamLake))]
        public class SyncRamLakeEditor : SyncLakeSplineEditor
        {

            public new void OnEnable()
            {
                base.OnEnable();

                // auto-detect objects
                if (GetEditorTarget().lakeSpline == null)
                {
                    GetEditorTarget().lakeSpline = GetEditorTarget().GetComponentInChildren<LakeSpline>();
                }

                if (GetEditorTarget().lakeSpline != null && GetEditorTarget().splineContainer != null)
                {
                    // force recreation only if sync is enabled
                    base.CreateLakeSpline(GetEditorTarget().syncEnabled);
                }
            }

            public SyncRamLake GetEditorTarget()
            {
                return ((SyncRamLake)target);
            }

            public new bool PerformInputParameterCheck()
            {
                if (!base.PerformInputParameterCheck())
                    return false;

                if (GetEditorTarget().lakeSpline == null)
                    return false;

                return true;
            }

            /// <summary>
            /// Map the spline points to river points
            /// </summary>
            /// <param name="worldPositions"></param>
            public override void CreateRiverSpline(List<Vector4> worldPositions)
            {
                LakeSpline riverSpline = GetEditorTarget().lakeSpline;

                // consider river gameobject transform in case it's different to the spline transform
                Vector4 offset = GetEditorTarget().transform == riverSpline.transform ? Vector4.zero : -riverSpline.transform.position;

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
                riverSpline.GeneratePolygon();
            }

            public override List<Vector3> GetRiverWorldPositions()
            {
                LakeSpline riverSpline = GetEditorTarget().lakeSpline;

                if (riverSpline == null)
                    return new List<Vector3>();

                // consider river gameobject transform in case it's different to the spline transform
                Vector3 offset = GetEditorTarget().transform == riverSpline.transform ? Vector3.zero : -riverSpline.transform.position;

                List<Vector3> worldPositions = new List<Vector3>();

                foreach (Vector4 riverPoint in riverSpline.splinePoints)
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