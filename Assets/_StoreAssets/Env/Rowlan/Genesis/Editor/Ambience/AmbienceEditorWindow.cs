using UnityEngine;
using UnityEditor;

namespace Rowlan.Genesis.Ambience
{
    public class AmbienceEditorWindow : EditorWindow
    {
        private AmbienceEditorWindow editorWindow;
        private AmbienceEditor editor;
        private AmbienceData data;

        public static bool helpBoxVisible = false;

        [MenuItem( ProjectSetup.MENU)]
        public static void CreateWindow()
        {
            // Get existing open window or if none, make a new one:
            AmbienceEditorWindow window = (AmbienceEditorWindow)EditorWindow.GetWindow(typeof(AmbienceEditorWindow));

            window.titleContent = new GUIContent( "Visual Ambience");

            window.Show();
        }


        void OnEnable()
        {
            editorWindow = this;

            ScriptableObjectManager<AmbienceData> settingsManager = new ScriptableObjectManager<AmbienceData>(ProjectSetup.SETTINGS_DIRECTORY, ProjectSetup.SETTINGS_FILENAME);
            data = settingsManager.GetAsset();

            editor = Editor.CreateEditor(data) as AmbienceEditor;

            editor.OnEnable();

        }

        public void OnDisable()
        {
            editor.OnDisable();

            DestroyImmediate(editor);

        }

        void OnGUI()
        {
            if (editor == null)
                return;

            // header
            DrawDialogHeader();

            // editor
            editor.OnInspectorGUI();
        }


        #region Header
        private void DrawDialogHeader()
        {
            // common header
            GUIUtils.DrawHeader("Visual Ambience", ref helpBoxVisible);

            // help
            if (helpBoxVisible)
            {
                EditorGUILayout.HelpBox(
                    "Ambience for MicroVerse"
                    + "\n"
                    + "Common utilities for post processing and lighting"
                    , MessageType.Info);
            }
        }
        #endregion Header

    }
}