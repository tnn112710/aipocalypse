#if !TEXTSTUDIO_DISABLE_TMP_INSPECTOR_ENHANCEMENT
using TMPro;
using TMPro.EditorUtilities;
using UnityEditor;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextMeshPro), true)]
    [CanEditMultipleObjects]
    internal sealed class TextStudioTextMeshProEditor : TMP_EditorPanel
    {
        // Unity's native host must own TMP's Object Selector command routing.
        public override void OnInspectorGUI()
        {
            TextStudioTMPInspectorHeader.DrawSetupControlsForTargets(targets);
            if (!TextStudioTMPTextInputProviders.TryGetProvider(targets, out ITextStudioTMPTextInputProvider provider))
            {
                base.OnInspectorGUI();
                return;
            }
            if (IsMixSelectionTypes())
                return;

            serializedObject.Update();
            bool styleChanged = TextStudioTMPInspectorHeader.DrawDrivenTextInput(
                m_TextProp, m_IsRightToLeftProp, m_TextStyleHashCodeProp,
                m_StyleNames, m_Styles, m_TextStyleIndexLookup, provider);
            m_HavePropertiesChanged |= styleChanged;
            DrawMainSettings();
            DrawExtraSettings();
            EditorGUILayout.Space();
            if (serializedObject.ApplyModifiedProperties() || m_HavePropertiesChanged)
            {
                if (styleChanged)
                    TextStudioTMPInspectorHeader.RefreshDrivenTextStyles(targets);
                m_TextComponent.havePropertiesChanged = true;
                m_HavePropertiesChanged = false;
            }
        }
    }

    [CustomEditor(typeof(TextMeshProUGUI), true)]
    [CanEditMultipleObjects]
    internal sealed class TextStudioTextMeshProUGUIEditor : TMP_EditorPanelUI
    {
        // Unity's native host must own TMP's Object Selector command routing.
        public override void OnInspectorGUI()
        {
            TextStudioTMPInspectorHeader.DrawSetupControlsForTargets(targets);
            if (!TextStudioTMPTextInputProviders.TryGetProvider(targets, out ITextStudioTMPTextInputProvider provider))
            {
                base.OnInspectorGUI();
                return;
            }
            if (IsMixSelectionTypes())
                return;

            serializedObject.Update();
            bool styleChanged = TextStudioTMPInspectorHeader.DrawDrivenTextInput(
                m_TextProp, m_IsRightToLeftProp, m_TextStyleHashCodeProp,
                m_StyleNames, m_Styles, m_TextStyleIndexLookup, provider);
            m_HavePropertiesChanged |= styleChanged;
            DrawMainSettings();
            DrawExtraSettings();
            EditorGUILayout.Space();
            if (serializedObject.ApplyModifiedProperties() || m_HavePropertiesChanged)
            {
                if (styleChanged)
                    TextStudioTMPInspectorHeader.RefreshDrivenTextStyles(targets);
                m_TextComponent.havePropertiesChanged = true;
                m_HavePropertiesChanged = false;
            }
        }
    }
}
#endif
