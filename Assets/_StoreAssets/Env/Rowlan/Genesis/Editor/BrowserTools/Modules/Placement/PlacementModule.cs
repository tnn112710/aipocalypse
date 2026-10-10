using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.BrowserTools
{
    public class PlacementModule : IBrowserToolsModule
    {
        private BrowserToolsModule editor;
        private BrowserToolsData editorTarget;
        private SerializedObject serializedObject;

        private PlacementController controller1;

        public PlacementModule(BrowserToolsModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {

            // controllers
            controller1 = new PlacementController(editor);

        }

        public void OnInspectorGUI()
        {

            // EditorGUILayout.LabelField("Game Manager", GUIStyles.BoxTitleStyle);

            controller1.OnInspectorGUI();

        }


    }
}