using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Ambience
{
    public class PostProcessingController
    {
        private AmbienceModule editor;

        Hdrp.PostProcessingSetupEditor hdrpPostProcessingEditor;
        Urp.PostProcessingSetupEditor urpPostProcessingEditor;
        Legacy.PostProcessingSetupEditor legacyPostProcessingEditor;

        public PostProcessingController(AmbienceModule editor)
        {
            this.editor = editor;

            hdrpPostProcessingEditor = new Hdrp.PostProcessingSetupEditor(editor);
            urpPostProcessingEditor = new Urp.PostProcessingSetupEditor(editor);
            legacyPostProcessingEditor = new Legacy.PostProcessingSetupEditor(editor);
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Setup", GUIStyles.BoxTitleStyle);

                OnInspectorGUIModules();

            }
            EditorGUILayout.EndVertical();

        }

        private void OnInspectorGUIModules()
        {

            int scriptingDefineSymbolCount = 0;

#if USING_HDRP

            hdrpPostProcessingEditor.OnInspectorGUI();
            scriptingDefineSymbolCount++;

#endif

#if USING_URP

            urpPostProcessingEditor.OnInspectorGUI();
            scriptingDefineSymbolCount++;

#endif

#if USING_LEGACY

            legacyPostProcessingEditor.OnInspectorGUI();
            scriptingDefineSymbolCount++;

#endif
            if (scriptingDefineSymbolCount == 0)
            {
                EditorGUILayout.HelpBox("No supported post-processing package found", MessageType.Warning);
            }

            if (scriptingDefineSymbolCount > 1)
            {
                EditorGUILayout.HelpBox("Multiple post processing modes active! (Example: URP and Built-in Post Processing installed in the same project)", MessageType.Error);
            }
        }
    }
}