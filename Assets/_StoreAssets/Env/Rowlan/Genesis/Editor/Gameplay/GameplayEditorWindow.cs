using UnityEngine;
using UnityEditor;

namespace Rowlan.Genesis.Gameplay
{
    public class GameplayEditorWindow : EditorWindow
    {
        private GameplayEditorWindow editorWindow;
        private GameplayEditor editor;
        private GameplayData data;

        public static bool helpBoxVisible = false;

        [MenuItem( ProjectSetup.MENU)]
        public static void CreateWindow()
        {
            // Get existing open window or if none, make a new one:
            GameplayEditorWindow window = (GameplayEditorWindow)EditorWindow.GetWindow(typeof(GameplayEditorWindow));

            window.titleContent = new GUIContent("Gameplay");

            window.Show();
        }


        void OnEnable()
        {
            editorWindow = this;

            ScriptableObjectManager<GameplayData> settingsManager = new ScriptableObjectManager<GameplayData>(ProjectSetup.SETTINGS_DIRECTORY, ProjectSetup.SETTINGS_FILENAME);
            data = settingsManager.GetAsset();

            editor = Editor.CreateEditor(data) as GameplayEditor;

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
            GUIUtils.DrawHeader("Gameplay", ref helpBoxVisible);

            // help
            if (helpBoxVisible)
            {
                EditorGUILayout.HelpBox(
                    "Gameplay for MicroVerse"
                    + "\n"
                    + "Common utilities for setting up gameplay"
                    , MessageType.Info);
            }
        }
        #endregion Header

    }
}