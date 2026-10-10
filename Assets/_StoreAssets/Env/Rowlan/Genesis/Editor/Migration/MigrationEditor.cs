using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Migration
{
    [ExecuteInEditMode]
    [CustomEditor(typeof(MigrationData))]
    public class MigrationEditor : Editor
    {
        private MigrationEditor editor;
        private MigrationData editorTarget;

        private MigrationModule module;

        public MigrationEditor()
        {
            this.module = new MigrationModule();
        }

        public void OnEnable()
        {
            editor = this;
            editorTarget = (MigrationData)target;

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

