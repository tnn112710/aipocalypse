using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Terraform
{
    [ExecuteInEditMode]
    [CustomEditor(typeof(TerraformData))]
    public class TerraformEditor : Editor
    {
        private TerraformEditor editor;
        private TerraformData editorTarget;

        private TerraformModule module;

        public TerraformEditor()
        {
            this.module = new TerraformModule();
        }
        public void OnEnable()
        {
            editor = this;
            editorTarget = (TerraformData)target;

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

