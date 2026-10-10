using UnityEngine;
using UnityEditor;

namespace Rowlan.Genesis.Converter
{
    public class ConverterEditorWindow : EditorWindow
    {
        private ConverterEditorWindow editorWindow;
        private ConverterEditor editor;
        private ConverterData data;

        public static bool helpBoxVisible = false;

        [MenuItem( ProjectSetup.MENU)]
        public static void CreateWindow()
        {
            // Get existing open window or if none, make a new one:
            ConverterEditorWindow window = (ConverterEditorWindow)EditorWindow.GetWindow(typeof(ConverterEditorWindow));

            window.titleContent = new GUIContent("Converter");

            window.Show();
        }


        void OnEnable()
        {
            editorWindow = this;

            ScriptableObjectManager<ConverterData> settingsManager = new ScriptableObjectManager<ConverterData>(ProjectSetup.SETTINGS_DIRECTORY, ProjectSetup.SETTINGS_FILENAME);
            data = settingsManager.GetAsset();

            editor = Editor.CreateEditor(data) as ConverterEditor;

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
            GUIUtils.DrawHeader("Converter", ref helpBoxVisible);

            // help
            if (helpBoxVisible)
            {
                EditorGUILayout.HelpBox(
                    "Converter for MicroVerse"
                    + "\n"
                    + "Common utilities for MicroVerse conversion"
                    , MessageType.Info);
            }
        }
        #endregion Header

    }
}