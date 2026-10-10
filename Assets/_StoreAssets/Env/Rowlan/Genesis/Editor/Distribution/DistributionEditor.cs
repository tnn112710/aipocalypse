using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Distribution
{
    [ExecuteInEditMode]
    [CustomEditor(typeof(DistributionData))]
    public class DistributionEditor : Editor
    {
        private DistributionEditor editor;
        private DistributionData editorTarget;

        private DistributionModule module;

        public DistributionEditor()
        {
            this.module = new DistributionModule();
        }
        public void OnEnable()
        {
            editor = this;
            editorTarget = (DistributionData)target;

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

