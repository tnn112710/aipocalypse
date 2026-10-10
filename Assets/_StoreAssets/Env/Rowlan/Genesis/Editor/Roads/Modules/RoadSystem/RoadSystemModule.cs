#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    public class RoadSystemModule : IRoadsModule
    {
        private RoadsModule editor;
        private RoadsData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty mode;

        private GUIContent[] modeButtons;
        private Vector2 scrollPosition = Vector2.zero;

        private RoadSystemDesignController controllerDesign;
        private RoadSystemBatchController controllerBatch;
        private RoadSystemGlobalController controllerGlobal;

        public RoadSystemModule(RoadsModule editor, SerializedObject serializedObject)
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
                new GUIContent( "Design", "Design Settings"),
                new GUIContent( "Batch", "Batch Operations"),
                new GUIContent( "Global", "Global Operations"),
            };

            mode = serializedObject.FindProperty("roadSystemMode");

            // controllers
            controllerDesign = new RoadSystemDesignController(editor);
            controllerBatch = new RoadSystemBatchController(editor);
            controllerGlobal = new RoadSystemGlobalController(editor);

        }

        public bool IsSupported(GameObject gameObject)
        {
            return gameObject.GetComponent<RoadSystem>() != null;
        }

        public void OnInspectorGUI()
        {

            EditorGUILayout.LabelField("Road System", GUIStyles.BoxTitleStyle);

            EditorGUI.BeginChangeCheck();
            {
                mode.intValue = GUILayout.Toolbar(mode.intValue, modeButtons, EditorStyles.miniButton);
            }
            if (EditorGUI.EndChangeCheck())
            {
                ModeChanged((RoadsData.RoadSystemMode)mode.intValue);
            }

            // control
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            {
                switch (editorTarget.roadSystemMode)
                {
                    case RoadsData.RoadSystemMode.Design:
                        controllerDesign.OnInspectorGUI();
                        break;

                    case RoadsData.RoadSystemMode.Batch:
                        controllerBatch.OnInspectorGUI();
                        break;

                    case RoadsData.RoadSystemMode.Global:
                        controllerGlobal.OnInspectorGUI();
                        break;

                    default:
                        Debug.LogError("Unsupported mode: " + editorTarget.roadSystemMode);
                        break;
                }
            }
            EditorGUILayout.EndScrollView();
        }

        public HeightStamp GetStamp()
        {
            return editorTarget.gameObject?.GetComponent<HeightStamp>();
        }

        private void ModeChanged(RoadsData.RoadSystemMode mode)
        {
        }
    }
}
#endif