using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Gameplay
{
    [ExecuteInEditMode]
    [CustomEditor(typeof(GameplayData))]
    public class GameplayEditor : Editor
    {
        private GameplayEditor editor;
        private GameplayData editorTarget;

        private GameplayModule module;

        public GameplayEditor()
        {
            this.module = new GameplayModule();
        }

        public void OnEnable()
        {
            editor = this;
            editorTarget = (GameplayData)target;

            module.OnEnable(serializedObject, editorTarget);

        }

        public void OnDisable()
        {
            module.OnDisable();
        }
        public override void OnInspectorGUI()
        {
            module.OnInspectorGUI();
        }

    }
}

