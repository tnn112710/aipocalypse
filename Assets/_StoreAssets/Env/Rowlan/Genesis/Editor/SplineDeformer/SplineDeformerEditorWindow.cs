#if __MICROVERSE_SPLINES__

using UnityEngine;
using UnityEditor;

namespace Rowlan.Genesis.SplineDeformer
{
    public class SplineDeformerEditorWindow : EditorWindow
    {
        private SplineDeformerEditorWindow editorWindow;
        private SplineDeformerEditor editor;
        private SplineDeformerData data;

        public static bool helpBoxVisible = false;

        [MenuItem( ProjectSetup.MENU)]
        public static void CreateWindow()

        {
            // Get existing open window or if none, make a new one:
            SplineDeformerEditorWindow window = (SplineDeformerEditorWindow)EditorWindow.GetWindow(typeof(SplineDeformerEditorWindow));

            window.titleContent = new GUIContent("Spline Deformer");

            window.Show();
        }


        void OnEnable()
        {
            editorWindow = this;

            ScriptableObjectManager<SplineDeformerData> settingsManager = new ScriptableObjectManager<SplineDeformerData>(ProjectSetup.SETTINGS_DIRECTORY, ProjectSetup.SETTINGS_FILENAME);
            data = settingsManager.GetAsset();

            editor = Editor.CreateEditor(data) as SplineDeformerEditor;

            editor.OnEnable();

        }

        public void OnDisable()
        {
            editor.OnDisable();

            DestroyImmediate(editor);

        }

        void OnFocus()
        {
            // if objects are selected in the hierarchy and eg splines got created
            // then we need to check for those new selected splines
            // note: auto-assignment is checked in the module
            OnSelectionChange();
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
            // bootstrapping check
            if (editor == null)
                return;

            editor.AssignSelectedGameObject();

            this.Repaint();
        }

        #region Header
        private void DrawDialogHeader()
        {
            // common header
            GUIUtils.DrawHeader("Spline Deformer (Beta)", ref helpBoxVisible);

            // help
            if (helpBoxVisible)
            {
                EditorGUILayout.HelpBox(
                    "Spline Deformer for MicroVerse"
                    + "\n"
                    + "Common utilities for spline modification"
                    , MessageType.Info);
            }
        }
        #endregion Header

    }
}
#endif