using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.ReGenesis
{
    public class BiomesMode2Controller
    {
        private ReGenesisModule editor;

        public enum Mode
        {
            Biomes,
            Ground,
            Alignment
        }

        private GUIContent[] modeButtons;

        private Mode mode = Mode.Biomes;

        private BiomeConverter biomesConverter;
        private GroundConverter groundConverter;
        private AlignmentTools alignmentTools;

        public BiomesMode2Controller(ReGenesisModule editor)
        {
            this.editor = editor;

            SerializedProperty settings = editor.GetSerializedObject().FindProperty("biomesSettings");
            
            modeButtons = new GUIContent[]
            {
                new GUIContent( "Biomes", "Biome setup"),
                new GUIContent( "Ground", "Ground objects setup, eg houses"),
                new GUIContent( "Alignment", "Alignment tools for prefabs"),
            };

            biomesConverter = new BiomeConverter(editor);
            groundConverter = new GroundConverter(editor);
            alignmentTools = new AlignmentTools(editor);
        }

        public void OnInspectorGUI()
        {

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Action", GUIStyles.BoxTitleStyle);

                mode = (Mode)GUILayout.Toolbar((int)mode, modeButtons, EditorStyles.miniButton);

                #region Properties

                if (ReGenesisEditorWindow.helpBoxVisible)
                {
                    EditorGUILayout.HelpBox("Perform action on the gameobject and its children in the hierarchy.", MessageType.None);
                }

                #endregion Properties

                #region Action

                if (ReGenesisEditorWindow.helpBoxVisible)
                {
                    EditorGUILayout.HelpBox("Perform dedicated automated changes on the selected gameobject.", MessageType.None);
                }

                switch(mode)
                {
                    case Mode.Biomes:
                        biomesConverter.OnInspectorGUI();
                        break;

                    case Mode.Ground:
                        groundConverter.OnInspectorGUI();
                        break;

                    case Mode.Alignment:
                        alignmentTools.OnInspectorGUI();
                        break;
                }

                #endregion Action
            }
            EditorGUILayout.EndVertical();
        }



    }
}