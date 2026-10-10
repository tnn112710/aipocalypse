using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Distribution
{
    public class FilterModule : IDistributionModule
    {
        private DistributionModule editor;
        private DistributionData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty mode;

        private GUIContent[] modeButtons;
        private Vector2 scrollPosition = Vector2.zero;

        private TextureFilterController textureFilterController;


        public FilterModule(DistributionModule editor, SerializedObject serializedObject)
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
                new GUIContent( "Filter", "Modify the filter settings of the stamp"),
            };

            mode = serializedObject.FindProperty("filterMode");

            // controllers
            textureFilterController = new TextureFilterController(editor);

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
                ModeChanged((DistributionData.FilterMode)mode.intValue);
            }

            // control
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            {
                switch (editorTarget.filterMode)
                {
                    case DistributionData.FilterMode.Texture:
                        textureFilterController.OnInspectorGUI();
                        break;

                    default:
                        editorTarget.filterMode = DistributionData.FilterMode.Texture;
                        break;
                }
            }
            EditorGUILayout.EndScrollView();
        }

        public Stamp[] GetStamp()
        {
            return editorTarget.gameObject?.GetComponentsInChildren<Stamp>();
        }

        private void ModeChanged(DistributionData.FilterMode mode)
        {
        }
    }
}