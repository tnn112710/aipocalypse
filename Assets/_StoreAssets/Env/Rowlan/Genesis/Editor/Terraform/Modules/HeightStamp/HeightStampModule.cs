using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Terraform
{
    public class HeightStampModule : ITerraformModule
    {
        private TerraformModule editor;
        private TerraformData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty mode;

        private GUIContent[] modeButtons;
        private Vector2 scrollPosition = Vector2.zero;

        private HeightStampMode1Controller controller1;


        public HeightStampModule(TerraformModule editor, SerializedObject serializedObject)
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
                new GUIContent( "Transform", "Modify the transform of the stamp"),
            };

            mode = serializedObject.FindProperty("heightStampMode");

            // controllers
            controller1 = new HeightStampMode1Controller(editor);

        }

        public bool IsSupported(GameObject gameObject)
        {
            return gameObject.GetComponent<HeightStamp>() != null;
        }

        public void OnInspectorGUI()
        {

            EditorGUILayout.LabelField("Height Stamp", GUIStyles.BoxTitleStyle);

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
                switch (editorTarget.heightStampMode)
                {
                    case TerraformData.HeightStampMode.Transform:
                        controller1.OnInspectorGUI();
                        break;

                    default:
                        // set a proper default; can't throw an exception because users may have selected one mode that got removed
                        // and hence the exception would be thrown, they couldn't get out of it anymore unless they removed the scriptable object
                        editorTarget.heightStampMode = TerraformData.HeightStampMode.Transform;
                        //throw new System.Exception("Unsupported mode: " + editorTarget.heightStampMode);
                        break;
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