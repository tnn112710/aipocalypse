using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Converter
{
    public class ConverterModule
    {
        private ConverterModule editor;
        private ConverterData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty module;

        private IConverterModule terrainModule;
        private IConverterModule heightmapModule;
        private IConverterModule assetsModule;

        private GUIContent[] modeButtons;

  
        public void OnEnable(SerializedObject serializedObject, ConverterData target)
        {
            this.editor = this;
            this.editorTarget = target;
            this.serializedObject = serializedObject;

            this.terrainModule = new TerrainModule(editor, serializedObject);
            this.heightmapModule = new HeightmapModule(editor, serializedObject);
            this.assetsModule = new AssetsModule(editor, serializedObject);

            Init();

        }

        private void Init()
        {
            modeButtons = new GUIContent[]
            {
                new GUIContent( "Terrain", "Terrain Converter"),
                new GUIContent( "Heightmap", "Heightmap Converter"),
                new GUIContent( "Assets", "Specific Assets Converter"),
            };

            module = serializedObject.FindProperty("module");

        }


        public void OnDisable()
        {
        }

        public ConverterData GetEditorTarget()
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
                ModeChanged((ConverterData.Module)module.intValue);
            }

            // control
            switch (editorTarget.module)
            {
                case ConverterData.Module.Terrain:
                    terrainModule.OnInspectorGUI();
                    break;

                case ConverterData.Module.Heightmap:
                    heightmapModule.OnInspectorGUI();
                    break;

                case ConverterData.Module.Assets:
                    assetsModule.OnInspectorGUI();
                    break;

                default:
                    throw new System.Exception("Unsupported mode: " + editorTarget.module);
            }

            // apply changes to serializedProperty
            serializedObject.ApplyModifiedProperties();

        }
        #endregion Inspector

        private void ModeChanged(ConverterData.Module mode)
        {
            // nothing to do yet
        }

        public SerializedObject GetSerializedObject()
        {
            return this.serializedObject;
        }
    }

}

