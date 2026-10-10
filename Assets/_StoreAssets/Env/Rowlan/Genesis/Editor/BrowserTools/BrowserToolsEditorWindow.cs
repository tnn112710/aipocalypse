using UnityEngine;
using UnityEditor;

namespace Rowlan.Genesis.BrowserTools
{
    public class BrowserToolsEditorWindow : EditorWindow
    {
        private BrowserToolsEditorWindow editorWindow;
        private BrowserToolsEditor editor;
        private BrowserToolsData data;

        public static bool helpBoxVisible = false;

        [MenuItem( ProjectSetup.MENU)]
        public static void CreateWindow()
        {
            // Get existing open window or if none, make a new one:
            BrowserToolsEditorWindow window = (BrowserToolsEditorWindow)EditorWindow.GetWindow(typeof(BrowserToolsEditorWindow));

            window.titleContent = new GUIContent("Browser Tools");

            window.Show();
        }


        void OnEnable()
        {
            editorWindow = this;

            ScriptableObjectManager<BrowserToolsData> settingsManager = new ScriptableObjectManager<BrowserToolsData>(ProjectSetup.SETTINGS_DIRECTORY, ProjectSetup.SETTINGS_FILENAME);
            data = settingsManager.GetAsset();

            editor = Editor.CreateEditor(data) as BrowserToolsEditor;

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
            GUIUtils.DrawHeader("Browser Tools", ref helpBoxVisible);

            // help
            if (helpBoxVisible)
            {
                EditorGUILayout.HelpBox(
                    "Browser Tools for MicroVerse"
                    + "\n"
                    + "Common utilities for the MicroVerse Content Browser"
                    , MessageType.Info);
            }
        }
        #endregion Header

    }
}