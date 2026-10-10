using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.ReGenesis
{
    [ExecuteInEditMode]
    [CustomEditor(typeof(ReGenesisData))]
    public class ReGenesisEditor : Editor
    {
        private ReGenesisEditor editor;
        private ReGenesisData editorTarget;

        private ReGenesisModule module;

        public ReGenesisEditor()
        {
            this.module = new ReGenesisModule();
        }
        public void OnEnable()
        {
            editor = this;
            editorTarget = (ReGenesisData)target;

            module.OnEnable(serializedObject, editorTarget);

            if (editorTarget.autoAssignment)
            {
                AssignSelectedGameObject();
            }

        }

        public void OnDisable()
        {
            module.OnDisable();
        }
        public override void OnInspectorGUI()
        {
            module.OnInspectorGUI();
        }

        public void AssignSelectedGameObject()
        {
            module.AssignSelectedGameObject();
        }

    }
}

