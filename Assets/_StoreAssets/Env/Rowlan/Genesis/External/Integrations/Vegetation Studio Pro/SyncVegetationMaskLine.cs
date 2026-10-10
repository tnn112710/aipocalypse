#if VEGETATION_STUDIO_PRO
/// <summary>
/// Synchronize MicroVerse Splines with Vegetation Mask Line.
/// 
/// This component requires Vegetation Studio Pro and the Unity Splines package in your project.
/// 
/// Usage: 
/// + the scripting define symbol VEGETATION_STUDIO_PRO which is implicitly set by VSP activates the script
/// + create a Unity spline
/// + add this script to the Spline
/// + create a Vegetation Mask Line
/// + drag the spline and the vegetation mask line into the dedicated slots of the inspector
/// + modify the spline, the vegetation mask line will adjust accordingly
/// 
/// Note: Vegetation Mask Line is polygon based, so you may want to switch the spline knots to Linear to get exact matches for the area
/// </summary>
namespace Rowlan.Genesis.Integrations.VegetationStudioPro
{

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

    using UnityEngine.Splines;
	using JBooth.MicroVerseCore;
	using AwesomeTechnologies;
	using System.Collections.Generic;

	public class SyncVegetationMaskLine : MonoBehaviour
    {
        [Tooltip("Whether syncing the spline with the Vegetation Mask Line is enabled or not")]
        public bool syncEnabled = true;

        [Tooltip("The Vegetation Mask Line")]
        public VegetationMaskLine mask;

        /// <summary>
        /// The Unity spline container.
        /// </summary>
        [Tooltip("The Unity spline container to be used")]
        public SplineContainer splineContainer;

#if UNITY_EDITOR
        [CustomEditor(typeof(SyncVegetationMaskLine))]
        public class SyncVegetationMaskLineEditor : Editor
        {
            private GUIContent UpdateButtonContent = EditorGUIUtility.TrTextContent("Update", "Update manually");

            SyncVegetationMaskLine editorTarget;
            SyncVegetationMaskLineEditor editor;

            public void OnEnable()
            {
                editor = this;
                editorTarget = (SyncVegetationMaskLine)target;

                Spline.Changed += OnSplineChanged;

                if( editorTarget.mask == null)
                {
                    editorTarget.mask = editorTarget.GetComponentInChildren<VegetationMaskLine>();
                }

                if (editorTarget.splineContainer == null)
                {
                    editorTarget.splineContainer = editorTarget.GetComponent<SplineContainer>();
                }

                if (editorTarget.mask != null && editorTarget.splineContainer != null)
                {
                    CreateMask();
                }
            }

            public void OnDisable()
            {
                Spline.Changed -= OnSplineChanged;
            }

            void OnSplineChanged(Spline spline, int knotIndex, SplineModification modificationType)
            {
                CreateMask();
            }

            public override void OnInspectorGUI()
            {
                serializedObject.Update();

                using var changeScope = new EditorGUI.ChangeCheckScope();
                {
                    base.DrawDefaultInspector();

                    if (GUILayout.Button(UpdateButtonContent))
                    {
                        CreateMask();
                    }
                }
                if (changeScope.changed)
                {
                    CreateMask();
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

            private void CreateMask()
            {
                if (!editorTarget.syncEnabled)
                    return;

                VegetationMaskLine vegetationMaskLine = editorTarget.mask;
                Spline unitySpline = GetMainSpline();

                if (vegetationMaskLine == null || unitySpline == null)
                    return;

                List<Vector3> positions = GetPositions(unitySpline);

                vegetationMaskLine.Nodes.Clear();
                vegetationMaskLine.AddNodesToEnd(positions.ToArray());

                EditorUtility.SetDirty(vegetationMaskLine);

                vegetationMaskLine.UpdateVegetationMask();
                //VegetationStudioProUtils.RefreshVegetation();

                MicroVerse.instance?.Invalidate();

            }

            private List<Vector3> GetPositions(Spline unitySpline)
            {
                List<Vector3> positions = new List<Vector3>();
                Matrix4x4 matrix = editorTarget.transform.localToWorldMatrix;

                foreach (BezierKnot knot in unitySpline)
                {
                    Vector3 position = matrix.MultiplyPoint(new Vector3(knot.Position.x, knot.Position.y, knot.Position.z));

                    positions.Add(position);
                }

                // closed path: connect last with first position
                if (unitySpline.Closed && positions.Count > 1)
                {
                    // add first position as last position
                    positions.Add(positions[0]);
                }

                return positions;
            }
        }
#endif
    }
}
#endif