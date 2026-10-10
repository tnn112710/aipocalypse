#if __MICROVERSE_SPLINES__
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.BrowserWizard
{
    [ExecuteInEditMode]
    [CustomEditor(typeof(BrowserWizardData))]
    public class BrowserWizardEditor : Editor
    {
        private BrowserWizardEditor editor;
        private BrowserWizardData editorTarget;

        private BrowserWizardModule module;

        public BrowserWizardEditor()
        {
            this.module = new BrowserWizardModule();
        }

        public void OnEnable()
        {
            editor = this;
            editorTarget = (BrowserWizardData)target;

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
#endif