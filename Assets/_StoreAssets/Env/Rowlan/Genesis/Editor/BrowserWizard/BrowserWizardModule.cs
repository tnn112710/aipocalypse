#if __MICROVERSE_SPLINES__
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.BrowserWizard
{
    public class BrowserWizardModule
    {
        private BrowserWizardModule editor;
        private BrowserWizardData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty module;

        private IBrowserWizardModule biomesModule;

        private GUIContent[] modeButtons;

  
        public void OnEnable(SerializedObject serializedObject, BrowserWizardData target)
        {
            this.editor = this;
            this.editorTarget = target;
            this.serializedObject = serializedObject;

            this.biomesModule = new BiomesModule(editor, serializedObject);

            Init();

        }

        private void Init()
        {
            modeButtons = new GUIContent[]
            {
                new GUIContent( "Biomes", "Biomes settings"),
            };

            module = serializedObject.FindProperty("module");

        }


        public void OnDisable()
        {
        }

        public BrowserWizardData GetEditorTarget()
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
                ModeChanged((BrowserWizardData.Module)module.intValue);
            }

            // control
            switch (editorTarget.module)
            {
                case BrowserWizardData.Module.Biomes:
                    biomesModule.OnInspectorGUI();
                    break;

                default:
                    throw new System.Exception("Unsupported mode: " + editorTarget.module);
            }

            // apply changes to serializedProperty
            serializedObject.ApplyModifiedProperties();

        }
        #endregion Inspector

        private void ModeChanged(BrowserWizardData.Module mode)
        {
            // nothing to do yet
        }
        public SerializedObject GetSerializedObject()
        {
            return this.serializedObject;
        }
    }

}
#endif