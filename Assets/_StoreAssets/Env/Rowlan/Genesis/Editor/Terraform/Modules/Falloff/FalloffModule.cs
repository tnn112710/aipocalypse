using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Terraform
{
    public class FalloffModule : ITerraformModule
    {
        private TerraformModule editor;
        private TerraformData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty mode;

        private GUIContent[] modeButtons;
        private Vector2 scrollPosition = Vector2.zero;

        private FalloffController controller1;

        public FalloffModule(TerraformModule editor, SerializedObject serializedObject)
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
                new GUIContent( "Falloff", "Settings related to falloff"),
            };

            mode = serializedObject.FindProperty("falloffMode");

            // controllers
            controller1 = new FalloffController(editor);

        }

        public bool IsSupported(GameObject gameObject)
        {
            return gameObject.GetComponent<FalloffOverride>() != null;
        }

        public void OnInspectorGUI()
        {

            EditorGUILayout.LabelField("Falloff", GUIStyles.BoxTitleStyle);

            EditorGUI.BeginChangeCheck();
            {
                mode.intValue = GUILayout.Toolbar(mode.intValue, modeButtons, EditorStyles.miniButton);
            }
            if (EditorGUI.EndChangeCheck())
            {
                ModeChanged((TerraformData.HeightStampMode)mode.intValue);
            }

            // control
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            {
                switch (editorTarget.falloffMode)
                {
                    case TerraformData.FalloffMode.Hierarchy:
                        controller1.OnInspectorGUI();
                        break;

                    default:
                        throw new System.Exception("Unsupported mode: " + editorTarget.heightStampMode);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        public HeightStamp GetStamp()
        {
            return editorTarget.gameObject?.GetComponent<HeightStamp>();
        }

        private void ModeChanged(TerraformData.HeightStampMode mode)
        {
        }
    }
}