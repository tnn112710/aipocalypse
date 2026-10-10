using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.BrowserTools
{
    public class BrowserToolsModule
    {
        private BrowserToolsModule editor;
        private BrowserToolsData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty module;

        private IBrowserToolsModule screenshotModule;
        private IBrowserToolsModule presetsModule;
        private IBrowserToolsModule placementModule;
        private IBrowserToolsModule validationModule;
        private IBrowserToolsModule batchModule;

        private GUIContent[] modeButtons;

  
        public void OnEnable(SerializedObject serializedObject, BrowserToolsData target)
        {
            this.editor = this;
            this.editorTarget = target;
            this.serializedObject = serializedObject;

            this.screenshotModule = new ScreenshotModule(editor, serializedObject);
            this.presetsModule = new PresetsModule(editor, serializedObject);
            this.placementModule = new PlacementModule(editor, serializedObject);
            this.validationModule = new ValidationModule(editor, serializedObject);
            this.batchModule = new BatchModule(editor, serializedObject);

            Init();

        }

        private void Init()
        {
            modeButtons = new GUIContent[]
            {
                new GUIContent( "Screenshot", "Screenshot settings"),
                new GUIContent( "Presets", "Presets settings"),
                new GUIContent( "Placement", "Placement settings"),
                new GUIContent( "Validation", "Validation settings"),
                new GUIContent( "Batch", "Batch processing"),

            };

            module = serializedObject.FindProperty("module");

        }


        public void OnDisable()
        {
        }

        public BrowserToolsData GetEditorTarget()
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
                ModeChanged((BrowserToolsData.Module)module.intValue);
            }

            // control
            switch (editorTarget.module)
            {
                case BrowserToolsData.Module.Screenshot:
                    screenshotModule.OnInspectorGUI();
                    break;

                case BrowserToolsData.Module.Presets:
                    presetsModule.OnInspectorGUI();
                    break;

                case BrowserToolsData.Module.Placement:
                    placementModule.OnInspectorGUI();
                    break;

                case BrowserToolsData.Module.Validation:
                    validationModule.OnInspectorGUI();
                    break;

                case BrowserToolsData.Module.Batch:
                    batchModule.OnInspectorGUI();
                    break;

                default:
                    throw new System.Exception("Unsupported mode: " + editorTarget.module);
            }

            // apply changes to serializedProperty
            serializedObject.ApplyModifiedProperties();

        }
        #endregion Inspector

        private void ModeChanged(BrowserToolsData.Module mode)
        {
            // nothing to do yet
        }
        public SerializedObject GetSerializedObject()
        {
            return this.serializedObject;
        }
    }

}

