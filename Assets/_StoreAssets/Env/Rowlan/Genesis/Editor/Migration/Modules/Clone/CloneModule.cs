using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Migration
{
    public class CloneModule : IMigrationModule
    {
        private MigrationModule editor;
        private MigrationData editorTarget;
        private SerializedObject serializedObject;

        private CloneModuleController controller1;

        public CloneModule(MigrationModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {

            // controllers
            controller1 = new CloneModuleController(editor);

        }

        public void OnInspectorGUI()
        {
            controller1.OnInspectorGUI();
        }


    }
}