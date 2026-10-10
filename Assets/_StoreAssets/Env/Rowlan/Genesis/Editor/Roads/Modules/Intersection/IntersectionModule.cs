#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    public class IntersectionModule : IRoadsModule
    {
        private RoadsModule editor;
        private RoadsData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty mode;

        private GUIContent[] modeButtons;
        private Vector2 scrollPosition = Vector2.zero;

        private IntersectionMode1Controller controller1;
        private IntersectionMode2Controller controller2;

        public IntersectionModule(RoadsModule editor, SerializedObject serializedObject)
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
                new GUIContent( "Align", "Alignment Settings"),
            };

            mode = serializedObject.FindProperty("intersectionMode");

            // controllers
            controller1 = new IntersectionMode1Controller(editor);
            controller2 = new IntersectionMode2Controller(editor);

        }

        public bool IsSupported(GameObject gameObject)
        {
            return gameObject.GetComponent<Intersection>() != null;
        }

        public void OnInspectorGUI()
        {

            EditorGUILayout.LabelField("Intersection", GUIStyles.BoxTitleStyle);

            EditorGUI.BeginChangeCheck();
            {
                mode.intValue = GUILayout.Toolbar(mode.intValue, modeButtons, EditorStyles.miniButton);
            }
            if (EditorGUI.EndChangeCheck())
            {
                ModeChanged((RoadsData.IntersectionMode)mode.intValue);
            }

            // control
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            {
                switch (editorTarget.intersectionMode)
                {
                    case RoadsData.IntersectionMode.Design:
                        controller1.OnInspectorGUI();
                        break;

                    case RoadsData.IntersectionMode.Align:
                        controller2.OnInspectorGUI();
                        break;

                    default:
                        throw new System.Exception("Unsupported mode: " + editorTarget.intersectionMode);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        public HeightStamp GetStamp()
        {
            return editorTarget.gameObject?.GetComponent<HeightStamp>();
        }

        private void ModeChanged(RoadsData.IntersectionMode mode)
        {
        }
    }
}
#endif