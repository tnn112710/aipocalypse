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
            base.OnInspectorGUI();
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
            base.OnInspectorGUI();
        }
    }
}
#endif
