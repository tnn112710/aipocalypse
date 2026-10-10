using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Migration
{
    public class MigrationModule
    {
        private MigrationModule editor;
        private MigrationData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty module;

        private IMigrationModule cloneModule;
        private IMigrationModule migrationModule;

        private GUIContent[] modeButtons;

  
        public void OnEnable(SerializedObject serializedObject, MigrationData target)
        {
            this.editor = this;
            this.editorTarget = target;
            this.serializedObject = serializedObject;

            this.cloneModule = new CloneModule(editor, serializedObject);
            this.migrationModule = new RemapModule(editor, serializedObject);

            Init();

        }

        private void Init()
        {
            modeButtons = new GUIContent[]
            {
                new GUIContent( "Clone", "Clone Content Collection"),
                new GUIContent( "Remap", "Remap guids in presets eg from Built-in to URP"),
            };

            module = serializedObject.FindProperty("module");

        }


        public void OnDisable()
        {
        }

        public MigrationData GetEditorTarget()
        {
            return editorTarget;
        }

        #region Inspector
        public void OnInspectorGUI()
        {
            serializedObject.Update();

            // modules
            EditorGUI.BeginChangeCheck();
            {
                module.intValue = GUILayout.Toolbar(module.intValue, modeButtons, EditorStyles.miniButton);
            }
            if (EditorGUI.EndChangeCheck())
            {
                ModeChanged((MigrationData.Module)module.intValue);
            }

            // control
            switch (editorTarget.module)
            {

                case MigrationData.Module.Clone:
                    cloneModule.OnInspectorGUI();
                    break;

                case MigrationData.Module.Migration:
                    migrationModule.OnInspectorGUI();
                    break;

                default:
                    throw new System.Exception("Unsupported mode: " + editorTarget.module);
            }

            // apply changes to serializedProperty
            serializedObject.ApplyModifiedProperties();

        }
        #endregion Inspector

        private void ModeChanged(MigrationData.Module mode)
        {
            // nothing to do yet
        }

        public SerializedObject GetSerializedObject()
        {
            return this.serializedObject;
        }
    }

}

