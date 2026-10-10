using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Terraform
{
#if __MICROVERSE_SPLINES__

    public class SplinePathModule : ITerraformModule
    {

        private TerraformModule editor;
        private TerraformData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty mode;

        private GUIContent[] modeButtons;
        private Vector2 scrollPosition = Vector2.zero;

        private SplinePathOperationsController operationsController;
        private SplinePathShapeController shapeController;
        private SplinePathMaskController maskController;

        public SplinePathModule(TerraformModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {
            modeButtons = new GUIContent[]
            {
                new GUIContent( "Operations", "Perform operations on the spline"),
                new GUIContent( "Shape", "Change the general shape of the spline"),
                new GUIContent( "Mask", "Create a spline area for masking"),
            };

            mode = serializedObject.FindProperty("splineMode");

            // controllers
            operationsController = new SplinePathOperationsController(editor);
            shapeController = new SplinePathShapeController(editor);
            maskController = new SplinePathMaskController(editor);

        }

        public bool IsSupported(GameObject gameObject)
        {
            return gameObject.GetComponent<SplinePath>() != null || gameObject.GetComponent<SplineArea>() != null;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Spline Path", GUIStyles.BoxTitleStyle);

            EditorGUI.BeginChangeCheck();
            {
                mode.intValue = GUILayout.Toolbar(mode.intValue, modeButtons, EditorStyles.miniButton);
            }
            if (EditorGUI.EndChangeCheck())
            {
                ModeChanged((TerraformData.SplineMode)mode.intValue);
            }

            // info
            EditorGUILayout.HelpBox("Hint: Switch to the spline's edit mode to see the knots", MessageType.None);

            // control
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            {
                switch (editorTarget.splineMode)
                {
                    case TerraformData.SplineMode.Operations:
                        operationsController.OnInspectorGUI();
                        break;

                    case TerraformData.SplineMode.Shape:
                        shapeController.OnInspectorGUI();
                        break;

                    case TerraformData.SplineMode.Mask:
                        maskController.OnInspectorGUI();
                        break;

                    default:
                        throw new System.Exception("Unsupported mode: " + editorTarget.splineMode);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        public SplinePath GetStamp()
        {
            return editorTarget.gameObject?.GetComponent<SplinePath>();
        }

        private void ModeChanged(TerraformData.SplineMode mode)
        {
        }
    }
#else
    public class SplinePathModule : ITerraformModule
    {
        public SplinePathModule(TerraformModule editor, SerializedObject serializedObject)
        {
        }

        public bool IsSupported(GameObject gameObject)
        {
            return false;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("MicroVerse module not available", MessageType.Warning);
        }
    }
#endif
}