using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Converter
{
    public class AssetsModule : IConverterModule
    {
        private ConverterModule editor;
        private ConverterData editorTarget;
        private SerializedObject serializedObject;

        private AssetsController controller1;

        public AssetsModule(ConverterModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {

            // controllers
            controller1 = new AssetsController(editor);

        }

        public void OnInspectorGUI()
        {
            controller1.OnInspectorGUI();
        }


    }
}