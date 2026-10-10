#if VEGETATION_STUDIO_PRO
/// <summary>
/// Synchronize MicroVerse Splines with Biome Mask Area.
/// 
/// This component requires Vegetation Studio Pro and the Unity Splines package in your project.
/// 
/// Usage: 
/// + the scripting define symbol VEGETATION_STUDIO_PRO which is implicitly set by VSP activates the script
/// + create a Unity spline
/// + add this script to the Spline
/// + create a Biome Mask Area
/// + drag the spline and the biome mask area into the dedicated slots of the inspector
/// + modify the spline, the biome mask area will adjust accordingly
/// 
/// Note: Biome Mask Area is polygon based, so you may want to switch the spline knots to Linear to get exact matches for the area
/// </summary>
namespace Rowlan.Genesis.Integrations.VegetationStudioPro
{

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

    using UnityEngine.Splines;
	using JBooth.MicroVerseCore;
	using AwesomeTechnologies.VegetationSystem.Biomes;
	using System.Collections.Generic;

	public class SyncBiomeMaskArea : MonoBehaviour
    {
        [Tooltip("Whether syncing the spline with the Biome Mask Area is enabled or not")]
        public bool syncEnabled = true;

        [Tooltip("The Biome Mask Area")]
        public BiomeMaskArea mask;

        /// <summary>
        /// The Unity spline container.
        /// </summary>
        [Tooltip("The Unity spline container to be used")]
        public SplineContainer splineContainer;

#if UNITY_EDITOR
        [CustomEditor(typeof(SyncBiomeMaskArea))]
        public class SyncBiomeMaskAreaEditor : Editor
        {
            private GUIContent UpdateButtonContent = EditorGUIUtility.TrTextContent("Update", "Update manually");

            SyncBiomeMaskArea editorTarget;
            SyncBiomeMaskAreaEditor editor;

            public void OnEnable()
            {
                editor = this;
                editorTarget = (SyncBiomeMaskArea)target;

                Spline.Changed += OnSplineChanged;

                if( editorTarget.mask == null)
                {
                    editorTarget.mask = editorTarget.GetComponentInChildren<BiomeMaskArea>();
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

                BiomeMaskArea biomeMaskArea = editorTarget.mask;
                Spline unitySpline = GetMainSpline();

                if (biomeMaskArea == null || unitySpline == null)
                    return;

                List<Vector3> positions = GetPositions(unitySpline);

                biomeMaskArea.Nodes.Clear();
                biomeMaskArea.AddNodesToEnd(positions.ToArray());

                EditorUtility.SetDirty(biomeMaskArea);

                BiomeMaskUtils.UpdateMask(biomeMaskArea);

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

                return positions;
            }
        }
#endif
    }
}
#endif