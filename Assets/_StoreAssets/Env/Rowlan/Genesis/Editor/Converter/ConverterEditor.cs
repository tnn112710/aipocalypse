using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Converter
{
    [ExecuteInEditMode]
    [CustomEditor(typeof(ConverterData))]
    public class ConverterEditor : Editor
    {
        private ConverterEditor editor;
        private ConverterData editorTarget;

        private ConverterModule module;

        public ConverterEditor()
        {
            this.module = new ConverterModule();
        }

        public void OnEnable()
        {
            editor = this;
            editorTarget = (ConverterData)target;

            module.OnEnable(serializedObject, editorTarget);

        }

        public void OnDisable()
        {
            module.OnDisable();
        }
        public override void OnInspectorGUI()
        {
            module.OnInspectorGUI();
        }

    }
}

