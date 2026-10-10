using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Gameplay
{
    public class GameManagerController
    {
        private GameplayModule editor;

        public GameManagerController(GameplayModule editor)
        {
            this.editor = editor;

        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Application", GUIStyles.BoxTitleStyle);

                EditorGUILayout.HelpBox("Tools for play mode: F5=Restart, ESC=Quit Game, Hide cursor in build or edit play mode. See the gameobject for details after it's been created.", MessageType.None);

                if (GUILayout.Button("Add Application Tools"))
                {
                    AddApplicationTools();
                }

            }
            EditorGUILayout.EndVertical();
        }

        private void AddApplicationTools()
        {
            GameObject go = new GameObject("Application Tools");

            go.AddComponent<ApplicationTools>();

            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);

            Selection.activeObject = go;
        }
    }
}