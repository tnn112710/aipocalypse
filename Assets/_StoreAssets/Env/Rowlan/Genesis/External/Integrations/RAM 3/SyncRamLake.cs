/// <summary>
/// Synchronize MicroVerse Splines with River Auto Material.
/// 
/// This component requires RAM 3 and the Unity Splines package in your project.
/// 
/// Usage: 
/// + activate the script by adding the scripting define symbol USING_RAM3
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
namespace Rowlan.Genesis.Integrations.RAM3
{
#if USING_RAM3

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

	using System.Collections.Generic;
    using LakeSpline = NatureManufacture.RAM.LakePolygon;
    using UnityEngine.Splines;

    public class SyncRamLake : SyncLakeSpline
    {

        [Tooltip("The lake spline")]
        public LakeSpline lakeSpline;

#if UNITY_EDITOR
        [CustomEditor(typeof(SyncRamLake))]
        public class SyncRamLakeEditor : SyncLakeSplineEditor
        {
            #region quick mode
            // quick mode: use quick mode while the spline is being modified and the mouse is dragging
            //             in that mode only the mesh shape is quickly updated
            //             after quick mode the full lake will be generated
            //             it's a feature of RAM 3
            //             the parameters are set using event handles on the spline and the scene gui
            private bool isSplineModified = false;
            private bool isDragging = false;
            #endregion quick mode

            void OnSceneGUI()
            {
                // quick mode only relevant if sync is enabled
                if (!GetEditorTarget().syncEnabled)
                    return;

                Event evt = Event.current;

                if (isSplineModified)
                {
                    isDragging = evt.type == EventType.MouseDrag;

                    if (evt.type == EventType.MouseUp)
                    {
                        CreateLakeSpline(true);

                        // reset quick lake update paramters
                        isSplineModified = false;
                    }
                }
            }

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

                Spline.Changed -= OnLakeChanged;
                Spline.Changed += OnLakeChanged;

            }

            public new void OnDisable()
            {
                base.OnDisable();

                Spline.Changed -= OnLakeChanged;

            }

            void OnLakeChanged(Spline spline, int knotIndex, SplineModification modificationType)
            {
                isSplineModified = true;
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
                if (worldPositions.Count == 0)
                    return;

                LakeSpline riverSpline = GetEditorTarget().lakeSpline;

                // consider river gameobject transform in case it's different to the spline transform
                Vector4 offset = GetEditorTarget().transform == riverSpline.transform ? Vector4.zero : -riverSpline.transform.position;

                // clear
                riverSpline.NmSpline.RemoveAllPoints();

                // remove last point if it is too close to the first point, otherwise the RAM spline smoother would write this error in the console: "Wrong lake shape"
                // it's a closed spline, so the last point isn't needed
                float distance = Vector3.Distance(worldPositions[0], worldPositions[worldPositions.Count - 1]);
                if (distance < 0.5f) // arbitrary distance; change if required; half a meter should suffice
                {
                    worldPositions.RemoveAt(worldPositions.Count - 1);
                }

                // add points
                foreach (Vector4 worldPosition in worldPositions)
                {
                    Vector4 point = worldPosition;

                    // consider offset, eg river gameobject transform in case it's different to the spline transform
                    point += offset;

                    riverSpline.NmSpline.AddPoint(point);
                }

                EditorUtility.SetDirty(riverSpline);

                bool quick = isSplineModified && isDragging;
                riverSpline.GeneratePolygon( quick);
            }

            public override List<Vector3> GetRiverWorldPositions()
            {
                LakeSpline riverSpline = GetEditorTarget().lakeSpline;

                if (riverSpline == null)
                    return new List<Vector3>();

                // consider river gameobject transform in case it's different to the spline transform
                Vector3 offset = GetEditorTarget().transform == riverSpline.transform ? Vector3.zero : -riverSpline.transform.position;

                List<Vector3> worldPositions = new List<Vector3>();

                foreach (NatureManufacture.RAM.NmSplinePoint riverPoint in riverSpline.NmSpline.Points)
                {
                    Vector3 position = new Vector3(riverPoint.Position.x, riverPoint.Position.y, riverPoint.Position.z);

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