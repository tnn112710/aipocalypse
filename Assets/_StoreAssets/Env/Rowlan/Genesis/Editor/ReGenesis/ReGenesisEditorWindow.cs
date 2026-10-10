using UnityEngine;
using UnityEditor;

namespace Rowlan.Genesis.ReGenesis
{
    public class ReGenesisEditorWindow : EditorWindow
    {
        private ReGenesisEditorWindow editorWindow;
        private ReGenesisEditor editor;
        private ReGenesisData data;

        public static bool helpBoxVisible = false;

        [MenuItem( ProjectSetup.MENU)]
        public static void CreateWindow()
        {
            // Get existing open window or if none, make a new one:
            ReGenesisEditorWindow window = (ReGenesisEditorWindow)EditorWindow.GetWindow(typeof(ReGenesisEditorWindow));

            window.titleContent = new GUIContent("ReGenesis");

            window.Show();
        }


        void OnEnable()
        {
            editorWindow = this;

            ScriptableObjectManager<ReGenesisData> settingsManager = new ScriptableObjectManager<ReGenesisData>(ProjectSetup.SETTINGS_DIRECTORY, ProjectSetup.SETTINGS_FILENAME);
            data = settingsManager.GetAsset();

            editor = Editor.CreateEditor(data) as ReGenesisEditor;

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
            GUIUtils.DrawHeader("ReGenesis", ref helpBoxVisible);

            // help
            if (helpBoxVisible)
            {
                EditorGUILayout.HelpBox(
                    "Tools for randomizing MicroVerse objects."
                    + "\n"
                    + "Please assign a gameobject within the MicroVerse hierarchy or select multiple ones and have Auto Assignment checked."
                    , MessageType.Info);
            }
        }
        #endregion Header
    }
}