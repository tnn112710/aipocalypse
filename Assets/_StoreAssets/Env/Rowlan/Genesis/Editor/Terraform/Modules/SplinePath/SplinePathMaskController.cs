using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Terraform
{
#if __MICROVERSE_SPLINES__
    using UnityEngine.Splines;

    public class SplinePathMaskController
    {

        private TerraformModule editor;

        private GameObject gameObject;

        public SplinePathMaskController(TerraformModule editor)
        {
            this.editor = editor;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                GUI.backgroundColor = HasGameObject() ? GUIStyles.DefaultBackgroundColor : GUIStyles.ErrorBackgroundColor;
                {
                    gameObject = EditorGUILayout.ObjectField(new GUIContent( "GameObject"), gameObject, typeof(GameObject), true) as GameObject;

                    if (!HasGameObject())
                    {
                        EditorGUILayout.HelpBox("Please provide a gameobject for bounds calculation", MessageType.Info);
                    }

                }
                GUI.backgroundColor = GUIStyles.DefaultBackgroundColor;

                EditorGUILayout.Space();

                bool prevEnabled = GUI.enabled;
                GUI.enabled = HasGameObject();
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("Create Spline Area"))
                        {
                            CreateSplineArea();
                        }

                    }
                    EditorGUILayout.EndHorizontal();
                }
                GUI.enabled = prevEnabled;
            }
            EditorGUILayout.EndVertical();
        }

        private bool HasGameObject()
        {
            return gameObject != null;
        }

        private void CreateSplineArea()
        {

            if (!(BoundsUtils.CalculateBounds(gameObject, out Bounds worldBounds)))
                return;

            SplineContainer splineContainer = editor.GetEditorTarget().gameObject?.GetComponent<SplineContainer>();

            if (splineContainer == null)
                return;

            Spline spline = splineContainer.Spline;

            // don't align, spline is already at the bounds of the gameobject
            bool autoAlign = false;

            int subdivisionIterations = 0;
            float scale = 1.0f;

            SplineShape splineShape = new SplineShape(
                splineContainer,
                spline,
                worldBounds,
                editor.GetEditorTarget().splineSettings.deformationAngle,
                subdivisionIterations,
                editor.GetEditorTarget().splineSettings.subdivisionRange,
                scale,
                autoAlign);

            splineShape.Apply(worldBounds);

            Invalidate();
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
    }
#endif
}