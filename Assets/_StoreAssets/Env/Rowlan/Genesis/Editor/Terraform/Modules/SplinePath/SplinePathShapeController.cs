using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Terraform
{
#if __MICROVERSE_SPLINES__
    using UnityEngine.Splines;

    public class SplinePathShapeController
    {
        // using property because of the propertydrawer
        private SerializedProperty deformationAngle;
        private SerializedProperty subdivisionRange;

        public float scale = 0.6f;
        public int subdivisionIterations = 2;

        private TerraformModule editor;

        public SplinePathShapeController(TerraformModule editor)
        {
            this.editor = editor;

            SerializedProperty settings = editor.GetSerializedObject().FindProperty("splineSettings");
            deformationAngle = settings.FindPropertyRelative("deformationAngle");
            subdivisionRange = settings.FindPropertyRelative("subdivisionRange");
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Spline Shape", GUIStyles.BoxTitleStyle);

                EditorGUILayout.PropertyField(deformationAngle);
                subdivisionIterations = EditorGUILayout.IntSlider(new GUIContent("Subdivision Iterations", "Number of times a subdivision will be performed"), subdivisionIterations, 0, 5);
                EditorGUILayout.PropertyField(subdivisionRange);
                scale = EditorGUILayout.Slider(new GUIContent("Scale", "The scale, relative to the terrain"), scale, 0f, 1f);

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Line"))
                    {
                        CreateSplineShape(PathSettings.Shape.Line);
                        MicroVerse.instance.Invalidate();
                    }

                    if (GUILayout.Button("Rectangle"))
                    {
                        CreateSplineShape(PathSettings.Shape.Rectangle);
                        MicroVerse.instance.Invalidate();
                    }

                    if (GUILayout.Button("Rounded Rectangle"))
                    {
                        CreateSplineShape(PathSettings.Shape.RoundedRectangle);
                        MicroVerse.instance.Invalidate();
                    }

                    if (GUILayout.Button("Circle"))
                    {
                        CreateSplineShape(PathSettings.Shape.Circle);

                    }

                    if (GUILayout.Button("Random"))
                    {
                        CreateSplineShape(PathSettings.Shape.Random);

                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("GameObject Tools", GUIStyles.BoxTitleStyle);

                EditorGUILayout.HelpBox(new GUIContent("Add a raycast shape creator script to the gameobject.\nUse the gameobject's inspector for the further modification of the spline."));
                EditorGUILayout.HelpBox(new GUIContent("This feature is experimental and may be extended or removed later."));

                if (GUILayout.Button("Add Raycast Shape Creator"))
                {
                    AddRaycastShapeCreator();
                }
            }
            EditorGUILayout.EndVertical();
        }


        private void CreateSplineShape(PathSettings.Shape shape)
        {
            SplineContainer splineContainer = editor.GetEditorTarget().gameObject?.GetComponent<SplineContainer>();

            if (splineContainer == null)
                return;

            Spline spline = splineContainer.Spline;
            bool autoAlign = true;

            SplineShape splineShape = new SplineShape(
                splineContainer,
                spline,
                MicroVerse.instance.GetTerrainWorldBounds(),
                editor.GetEditorTarget().splineSettings.deformationAngle, 
                subdivisionIterations,
                editor.GetEditorTarget().splineSettings.subdivisionRange,
                scale,
                autoAlign);

            splineShape.Apply(shape);

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


        private void AddRaycastShapeCreator()
        {
            SplineContainer splineContainer = GetSplineContainer();

            if (splineContainer == null)
                return;

            splineContainer.transform.gameObject.AddComponent<RaycastShapeCreator>();
        }
    }
#endif
}