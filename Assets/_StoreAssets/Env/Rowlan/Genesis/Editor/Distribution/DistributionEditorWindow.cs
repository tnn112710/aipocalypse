using UnityEngine;
using UnityEditor;

namespace Rowlan.Genesis.Distribution
{
    public class DistributionEditorWindow : EditorWindow
    {
        private DistributionEditorWindow editorWindow;
        private DistributionEditor editor;
        private DistributionData data;

        public static bool helpBoxVisible = false;

        [MenuItem( ProjectSetup.MENU)]
        public static void CreateWindow()
        {
            // Get existing open window or if none, make a new one:
            DistributionEditorWindow window = (DistributionEditorWindow)EditorWindow.GetWindow(typeof(DistributionEditorWindow));

            window.titleContent = new GUIContent("Distribution");

            window.Show();
        }


        void OnEnable()
        {
            editorWindow = this;

            ScriptableObjectManager<DistributionData> settingsManager = new ScriptableObjectManager<DistributionData>(ProjectSetup.SETTINGS_DIRECTORY, ProjectSetup.SETTINGS_FILENAME);
            data = settingsManager.GetAsset();

            editor = Editor.CreateEditor(data) as DistributionEditor;

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

        /// <summary>
        /// Analyze selection, assign gameobject
        /// </summary>
        void OnSelectionChange()
        {
            editor.AssignSelectedGameObject();

            this.Repaint();
        }

        #region Header
        private void DrawDialogHeader()
        {
            // common header
            GUIUtils.DrawHeader("Distribution", ref helpBoxVisible);

            // help
            if (helpBoxVisible)
            {
                EditorGUILayout.HelpBox(
                    "Tools for distributing content in MicroVerse stamps."
                    + "\n"
                    + "Please assign a gameobject within the MicroVerse hierarchy or select multiple ones and have Auto Assignment checked."
                    , MessageType.Info);
            }
        }
        #endregion Header
    }
}