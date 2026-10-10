using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Gameplay
{
    public class GameplayModule
    {
        private GameplayModule editor;
        private GameplayData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty module;

        private IGameplayModule playerModule;
        private IGameplayModule gameManagerModule;

        private GUIContent[] modeButtons;

  
        public void OnEnable(SerializedObject serializedObject, GameplayData target)
        {
            this.editor = this;
            this.editorTarget = target;
            this.serializedObject = serializedObject;

            this.playerModule = new PlayerModule(editor, serializedObject);
            this.gameManagerModule = new GameManagerModule(editor, serializedObject);

            Init();

        }

        private void Init()
        {
            modeButtons = new GUIContent[]
            {
                new GUIContent( "Player", "Player settings"),
                new GUIContent( "Game Manager", "Game settings")
            };

            module = serializedObject.FindProperty("module");

        }


        public void OnDisable()
        {
        }

        public GameplayData GetEditorTarget()
        {
            return editorTarget;
        }

        #region Inspector
        public void OnInspectorGUI()
        {
            serializedObject.Update();

            // modules
            EditorGUI.BeginChangeCheck();
            {
                module.intValue = GUILayout.Toolbar(module.intValue, modeButtons, EditorStyles.miniButton);
            }
            if (EditorGUI.EndChangeCheck())
            {
                ModeChanged((GameplayData.Module)module.intValue);
            }

            // control
            switch (editorTarget.module)
            {
                case GameplayData.Module.Player:
                    playerModule.OnInspectorGUI();
                    break;

                case GameplayData.Module.GameManager:
                    gameManagerModule.OnInspectorGUI();
                    break;

                default:
                    throw new System.Exception("Unsupported mode: " + editorTarget.module);
            }

            // apply changes to serializedProperty
            serializedObject.ApplyModifiedProperties();

        }
        #endregion Inspector

        private void ModeChanged(GameplayData.Module mode)
        {
            // nothing to do yet
        }
    }

}

