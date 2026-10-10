using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Converter
{
    public class TerrainModule : IConverterModule
    {
        private ConverterModule editor;
        private ConverterData editorTarget;
        private SerializedObject serializedObject;

        private TerrainController terrainController;

        public TerrainModule(ConverterModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {

            // controllers
            terrainController = new TerrainController(editor);

        }

        public void OnInspectorGUI()
        {
            terrainController.OnInspectorGUI();
        }


    }
}