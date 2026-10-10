using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.BrowserTools
{
    public class ScreenshotModule : IBrowserToolsModule
    {
        private BrowserToolsModule editor;
        private BrowserToolsData editorTarget;
        private SerializedObject serializedObject;

        private ScreenshotController controller1;

        public ScreenshotModule(BrowserToolsModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {

            // controllers
            controller1 = new ScreenshotController(editor);

        }

        public void OnInspectorGUI()
        {

            // EditorGUILayout.LabelField("Game Manager", GUIStyles.BoxTitleStyle);

            controller1.OnInspectorGUI();

        }


    }
}