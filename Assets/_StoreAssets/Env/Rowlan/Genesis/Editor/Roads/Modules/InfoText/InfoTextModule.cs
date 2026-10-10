#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    /// <summary>
    /// Display an info text instead of an inspector.
    /// Just to not have a warning show up in the roads inspector when a supported object is being used
    /// </summary>
    public class InfoTextModule : IRoadsModule
    {
        // content to display. text is used as header, tooltip is used as info text
        private static GUIContent emptyContent = new GUIContent("", "");
        private static GUIContent gridDesignerContent = new GUIContent("Grid Designer", "Please use the settings in the Inspector");

        private RoadsModule editor;
        private RoadsData editorTarget;
        private SerializedObject serializedObject;

        private Vector2 scrollPosition = Vector2.zero;
        private GUIContent infoContent = emptyContent;

        public InfoTextModule(RoadsModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {
            if (editorTarget.gameObject?.GetComponent<GridDesigner>() != null)
            {
                infoContent = gridDesignerContent;
            }

        }

        public bool IsSupported(GameObject gameObject)
        {
            return gameObject.GetComponent<GridDesigner>() != null;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.LabelField(infoContent.text, GUIStyles.BoxTitleStyle);

            GUILayout.BeginVertical();
            {
                // control
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                {
                    EditorGUILayout.HelpBox(infoContent.tooltip, MessageType.None);
                }
                EditorGUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
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