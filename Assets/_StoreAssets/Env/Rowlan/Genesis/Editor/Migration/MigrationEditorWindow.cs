using UnityEngine;
using UnityEditor;

namespace Rowlan.Genesis.Migration
{
    public class MigrationEditorWindow : EditorWindow
    {
        private MigrationEditorWindow editorWindow;
        private MigrationEditor editor;
        private MigrationData data;

        public static bool helpBoxVisible = false;

        [MenuItem( ProjectSetup.MENU)]
        public static void CreateWindow()
        {
            // Get existing open window or if none, make a new one:
            MigrationEditorWindow window = (MigrationEditorWindow)EditorWindow.GetWindow(typeof(MigrationEditorWindow));

            window.titleContent = new GUIContent("Migration");

            window.Show();
        }


        void OnEnable()
        {
            editorWindow = this;

            ScriptableObjectManager<MigrationData> settingsManager = new ScriptableObjectManager<MigrationData>(ProjectSetup.SETTINGS_DIRECTORY, ProjectSetup.SETTINGS_FILENAME);
            data = settingsManager.GetAsset();

            editor = Editor.CreateEditor(data) as MigrationEditor;

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

            // show experimental
            GUIUtils.DrawExperimentalHeader();

            // editor
            editor.OnInspectorGUI();
        }


        #region Header
        private void DrawDialogHeader()
        {
            // common header
            GUIUtils.DrawHeader("Migration", ref helpBoxVisible);

            // help
            if (helpBoxVisible)
            {
                EditorGUILayout.HelpBox(
                    "Migration for MicroVerse"
                    + "\n"
                    + "Common utilities for MicroVerse migration"
                    , MessageType.Info);
            }
        }
        #endregion Header

    }
}