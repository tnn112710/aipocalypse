using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.ReGenesis
{
    public class HeightStampModule : IReGenesisModule
    {
        private ReGenesisModule editor;
        private ReGenesisData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty mode;

        private GUIContent[] modeButtons;
        private Vector2 scrollPosition = Vector2.zero;

        private HeightStampMode1Controller controller1;
        private HeightStampMode2Controller controller2;


        public HeightStampModule(ReGenesisModule editor, SerializedObject serializedObject)
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
                new GUIContent( "Transform", "Modify the transform of the stamp"),
                new GUIContent( "Heightmap", "Modify the heightmap texture"),
            };

            mode = serializedObject.FindProperty("heightStampMode");

            // controllers
            controller1 = new HeightStampMode1Controller(editor);
            controller2 = new HeightStampMode2Controller(editor);

        }

        public bool IsSupported(GameObject gameObject)
        {
            return gameObject.GetComponent<HeightStamp>() != null;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Height Stamp", GUIStyles.BoxTitleStyle);

                GUILayout.FlexibleSpace();

                if (EditorGUILayout.LinkButton("Get Free Stamps"))
                {
                    Application.OpenURL("https://assetstore.unity.com/packages/tools/terrain/stampit-collection-examples-218286");
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            {
                mode.intValue = GUILayout.Toolbar(mode.intValue, modeButtons, EditorStyles.miniButton);
            }
            if (EditorGUI.EndChangeCheck())
            {
                ModeChanged((ReGenesisData.HeightStampMode)mode.intValue);
            }

            // control
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            {
                switch (editorTarget.heightStampMode)
                {
                    case ReGenesisData.HeightStampMode.Transform:
                        controller1.OnInspectorGUI();
                        break;

                    case ReGenesisData.HeightStampMode.Heightmap:
                        controller2.OnInspectorGUI();
                        break;

                    default:
                        throw new System.Exception("Unsupported mode: " + editorTarget.heightStampMode);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        public HeightStamp GetStamp()
        {
            return editorTarget.gameObject?.GetComponent<HeightStamp>();
        }

        private void ModeChanged(ReGenesisData.HeightStampMode mode)
        {
        }
    }
}