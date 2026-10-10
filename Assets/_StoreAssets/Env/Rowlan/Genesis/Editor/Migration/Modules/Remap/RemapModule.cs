using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Migration
{
    public class RemapModule : IMigrationModule
    {
        private MigrationModule editor;
        private MigrationData editorTarget;
        private SerializedObject serializedObject;

        private RemapController controller1;

        public RemapModule(MigrationModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {

            // controllers
            controller1 = new RemapController(editor);

        }

        public void OnInspectorGUI()
        {
            controller1.OnInspectorGUI();
        }


    }
}