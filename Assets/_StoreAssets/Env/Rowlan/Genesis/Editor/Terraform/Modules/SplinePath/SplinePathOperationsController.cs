using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Terraform
{
    using JBooth.MicroVerseCore;
    using System.Linq;
    using Unity.Mathematics;
#if __MICROVERSE_SPLINES__
    using UnityEngine.Splines;

    public class SplinePathOperationsController
    {
        private TerraformModule editor;

        private SplineAlignment splineAlignment;

        private SerializedProperty deformationAngle;
        private SerializedProperty subdivisionRange;
        private SerializedProperty douglasPeuckerReductionTolerance;

        private float resizePercent = 0.1f;

        public SplinePathOperationsController(TerraformModule editor)
        {
            this.editor = editor;

            this.splineAlignment = new SplineAlignment(null);

            SerializedProperty settings = editor.GetSerializedObject().FindProperty("splineSettings");

            deformationAngle = settings.FindPropertyRelative("deformationAngle");
            subdivisionRange = settings.FindPropertyRelative("subdivisionRange");
            douglasPeuckerReductionTolerance = settings.FindPropertyRelative("douglasPeuckerReductionTolerance");
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Alignment", GUIStyles.BoxTitleStyle);

                if (GUILayout.Button("Align to Terrain", GUILayout.Height(32)))
                {
                    // TODO: undo                {

                    splineAlignment.AlignSpline(GetSplineContainer());
                }

                if (GUILayout.Button("Center Main Handle"))
                {
                    CenterMainHandle();

                    SceneView.lastActiveSceneView.Repaint();
                }

            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            {

                EditorGUILayout.LabelField("Subdivision", GUIStyles.BoxTitleStyle);

                // deformation
                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();
                    
                    if (GUILayout.Button("Max"))
                    {
                        deformationAngle.vector2Value = new Vector2(-20, 20);
                    }

                    if (GUILayout.Button("None"))
                    {
                        deformationAngle.vector2Value = new Vector2(0, 0);
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(deformationAngle);

                // subdivision range
                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Max"))
                    {
                        subdivisionRange.vector2Value = new Vector2(0.2f, 0.8f);
                    }

                    if (GUILayout.Button("Middle"))
                    {
                        subdivisionRange.vector2Value = new Vector2(0.5f, 0.5f);
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(subdivisionRange);

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Subdivide"))
                    {
                        // TODO: undo

                        Spline spline = GetSpline();

                        Subdivide(spline, true);

                        Invalidate();
                    }

                    if (GUILayout.Button("Unsubdivide"))
                    {
                        // TODO: undo

                        Spline spline = GetSpline();

                        Unsubdivide(spline, true);

                        Invalidate();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            // douglas peucker
            EditorGUILayout.BeginVertical( "box");
            {
                EditorGUILayout.LabelField("Node Reduction", GUIStyles.BoxTitleStyle);

                EditorGUILayout.PropertyField(douglasPeuckerReductionTolerance, new GUIContent("Reduction Tolerance", "Douglas Peucker node reduction tolerance. 0 = disabled."));

                bool prevEnabled = GUI.enabled;
                GUI.enabled = douglasPeuckerReductionTolerance.floatValue > 0f;

                if ( GUILayout.Button( "Apply Douglas Peucker"))
                {
                    Spline spline = GetSpline();

                    ApplyDouglasPeucker( spline, true);

                    Invalidate();
                }

                GUI.enabled = prevEnabled;

                if (GUILayout.Button("Convex Hull"))
                {
                    Spline spline = GetSpline();

                    ApplyConvexHull(spline, true);

                    Invalidate();
                }
            }
            EditorGUILayout.EndVertical();


            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Scale", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Grow"))
                    {
                        // TODO: undo
                        GrowSpline();

                        Invalidate();
                    }

                    if (GUILayout.Button("Shrink"))
                    {
                        // TODO: undo

                        ShrinkSpline();

                        Invalidate();

                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

        }

        private Spline GetSpline()
        {
            return editor.GetEditorTarget().gameObject?.GetComponent<SplineContainer>().Spline;
        }

        private void Subdivide(Spline spline, bool autoAlign)
        {
            Vector2 deformationAngle = editor.GetEditorTarget().splineSettings.deformationAngle;
            Vector2 subdivisionRange = editor.GetEditorTarget().splineSettings.subdivisionRange;

            SplineCreator.SubdivideSpline(spline, subdivisionRange, deformationAngle);

            if (autoAlign)
            {
                splineAlignment.AlignAllSplines();
            }
        }

        private void Unsubdivide(Spline spline, bool autoAlign)
        {
            SplineCreator.UnsubdivideSpline(spline);

            if (autoAlign)
            {
                splineAlignment.AlignAllSplines();
            }
        }

        private void ApplyDouglasPeucker(Spline spline, bool autoAlign)
        {
            SplineUtils.ApplyDouglasPeucker(spline, douglasPeuckerReductionTolerance.floatValue);

            if (autoAlign)
            {
                splineAlignment.AlignAllSplines();
            }
        }

        private void ApplyConvexHull(Spline spline, bool autoAlign)
        {
            SplineUtils.ApplyConvexHull(spline);

            if (autoAlign)
            {
                splineAlignment.AlignAllSplines();
            }
        }

        private void Invalidate()
        {
            EditorUtility.SetDirty(GetSplineContainer()?.transform);
            MicroVerse.instance.Invalidate();

            SceneView.lastActiveSceneView.Repaint();
        }

        private SplineContainer GetSplineContainer()
        {
            return editor.GetEditorTarget().gameObject?.GetComponent<SplineContainer>();
        }

        private void CenterMainHandle()
        {
            SplineUtils.CenterMainHandle(GetSplineContainer());
        }

        private void GrowSpline()
        {
            SplineUtils.GrowSpline(GetSplineContainer(), resizePercent);
        }

        private void ShrinkSpline()
        {
            SplineUtils.ShrinkSpline(GetSplineContainer(), resizePercent);
        }



    }
#endif
}