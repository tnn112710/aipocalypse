using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.BrowserTools
{
    public class BatchModule : IBrowserToolsModule
    {
        private BrowserToolsModule editor;
        private BrowserToolsData editorTarget;
        private SerializedObject serializedObject;

        private BatchController controller1;

        public BatchModule(BrowserToolsModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {
            // controllers
            controller1 = new BatchController(editor);
        }

        public void OnInspectorGUI()
        {
            controller1.OnInspectorGUI();
        }
    }
}