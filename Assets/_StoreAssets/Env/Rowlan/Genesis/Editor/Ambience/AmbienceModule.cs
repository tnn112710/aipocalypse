using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Ambience
{
    public class AmbienceModule
    {

        private AmbienceModule editor;
        private AmbienceData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty module;

        private IAmbienceModule postProcessingModule;
        private IAmbienceModule timeOfDayModule;

        private GUIContent[] modeButtons;

  
        public void OnEnable(SerializedObject serializedObject, AmbienceData target)
        {
            this.editor = this;
            this.editorTarget = target;
            this.serializedObject = serializedObject;

            this.postProcessingModule = new PostProcessingModule(editor, serializedObject);
            this.timeOfDayModule = new TimeOfDayModule(editor, serializedObject);

            Init();

        }

        private void Init()
        {
            modeButtons = new GUIContent[]
            {
                new GUIContent( "Post Processing", "Post Processing Settings"),
                new GUIContent( "Time of Day", "Directional Light Settings")
            };

            module = serializedObject.FindProperty("module");

        }


        public void OnDisable()
        {
        }

        public AmbienceData GetEditorTarget()
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
                ModeChanged((AmbienceData.Module)module.intValue);
            }

            // control
            switch (editorTarget.module)
            {
                case AmbienceData.Module.PostProcessing:
                    postProcessingModule.OnInspectorGUI();
                    break;

                case AmbienceData.Module.TimeOfDay:
                    timeOfDayModule.OnInspectorGUI();
                    break;

                default:
                    throw new System.Exception("Unsupported mode: " + editorTarget.module);
            }

            // apply changes to serializedProperty
            serializedObject.ApplyModifiedProperties();

        }
        #endregion Inspector

        private void ModeChanged(AmbienceData.Module mode)
        {
            // nothing to do yet
        }


    }

}

