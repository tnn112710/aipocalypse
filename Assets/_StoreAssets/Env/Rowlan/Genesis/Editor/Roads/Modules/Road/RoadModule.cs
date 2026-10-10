#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    public class RoadModule : IRoadsModule
    {
        private RoadsModule editor;
        private RoadsData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty mode;

        private GUIContent[] modeButtons;
        private Vector2 scrollPosition = Vector2.zero;

        private RoadDesignController controllerDesign;
        private RoadAlignController controllerAlign;
        private RoadObjectsController controllerObjects;
        private RoadSplitController controllerSplit;

        public RoadModule(RoadsModule editor, SerializedObject serializedObject)
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
                new GUIContent( "Objects", "Prefab instantiation along road"),
                new GUIContent( "Align", "Alignment Settings"),
                new GUIContent( "Split", "Split Road"),

            };

            mode = serializedObject.FindProperty("roadMode");

            // controllers
            controllerDesign = new RoadDesignController(editor);
            controllerAlign = new RoadAlignController(editor);
            controllerObjects = new RoadObjectsController(editor);
            controllerSplit = new RoadSplitController(editor);

        }

        public bool IsSupported(GameObject gameObject)
        {
            return gameObject.GetComponent<Road>() != null;
        }

        public void OnInspectorGUI()
        {

            EditorGUILayout.LabelField("Road", GUIStyles.BoxTitleStyle);

            EditorGUI.BeginChangeCheck();
            {
                mode.intValue = GUILayout.Toolbar(mode.intValue, modeButtons, EditorStyles.miniButton);
            }
            if (EditorGUI.EndChangeCheck())
            {
                ModeChanged((RoadsData.RoadMode)mode.intValue);
            }

            // control
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            {
                switch (editorTarget.roadMode)
                {
                    case RoadsData.RoadMode.Design:
                        controllerDesign.OnInspectorGUI();
                        break;

                    case RoadsData.RoadMode.Objects:
                        controllerObjects.OnInspectorGUI();
                        break;

                    case RoadsData.RoadMode.Align:
                        controllerAlign.OnInspectorGUI();
                        break;

                    case RoadsData.RoadMode.Split:
                        controllerSplit.OnInspectorGUI();
                        break;

                    default:
                        throw new System.Exception("Unsupported mode: " + editorTarget.roadMode);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        public HeightStamp GetStamp()
        {
            return editorTarget.gameObject?.GetComponent<HeightStamp>();
        }

        private void ModeChanged(RoadsData.RoadMode mode)
        {
        }
    }
}
#endif