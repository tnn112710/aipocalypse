using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.ReGenesis
{
    public class BiomesModule : IReGenesisModule
    {
        private ReGenesisModule editor;
        private ReGenesisData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty mode;

        private GUIContent[] modeButtons;
        private Vector2 scrollPosition = Vector2.zero;

        private BiomesMode1Controller controller1;
        private BiomesMode2Controller controller2;


        public BiomesModule(ReGenesisModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {
            modeButtons = new GUIContent[]
            {
                new GUIContent( "Settings", "Modify the settings of the stamp"),
                new GUIContent( "Automate", "Automate changes"),
            };

            mode = serializedObject.FindProperty("biomesMode");

            // controllers
            controller1 = new BiomesMode1Controller(editor);
            controller2 = new BiomesMode2Controller(editor);

        }

        public bool IsSupported(GameObject gameObject)
        {
            return gameObject.GetComponentInParent<MicroVerse>() != null;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Stamps", GUIStyles.BoxTitleStyle);

            EditorGUI.BeginChangeCheck();
            {
                mode.intValue = GUILayout.Toolbar(mode.intValue, modeButtons, EditorStyles.miniButton);
            }
            if (EditorGUI.EndChangeCheck())
            {
                ModeChanged((ReGenesisData.BiomesMode)mode.intValue);
            }

            // control
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            {
                switch (editorTarget.biomesMode)
                {
                    case ReGenesisData.BiomesMode.Settings:
                        controller1.OnInspectorGUI();
                        break;

                    case ReGenesisData.BiomesMode.Automate:
                        controller2.OnInspectorGUI();
                        break;

                    default:
                        editorTarget.biomesMode = ReGenesisData.BiomesMode.Settings;
                        break;
                }
            }
            EditorGUILayout.EndScrollView();
        }

        public Stamp[] GetStamp()
        {
            return editorTarget.gameObject?.GetComponentsInChildren<Stamp>();
        }

        private void ModeChanged(ReGenesisData.BiomesMode mode)
        {
        }
    }
}