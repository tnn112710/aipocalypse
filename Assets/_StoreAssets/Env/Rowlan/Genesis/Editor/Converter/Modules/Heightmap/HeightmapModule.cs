using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Converter
{
    public class HeightmapModule : IConverterModule
    {
        private ConverterModule editor;
        private ConverterData editorTarget;
        private SerializedObject serializedObject;

        private HeightmapController controller1;

        public HeightmapModule(ConverterModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {

            // controllers
            controller1 = new HeightmapController(editor);

        }

        public void OnInspectorGUI()
        {
            controller1.OnInspectorGUI();
        }


    }
}