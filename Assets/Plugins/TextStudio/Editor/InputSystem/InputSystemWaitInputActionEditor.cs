using TextStudio.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TextStudio.InputSystem.Editor
{
    [CustomEditor(typeof(InputSystemWaitInputAction))]
    [CanEditMultipleObjects]
    internal sealed class InputSystemWaitInputActionEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-input-wait-action-inspector");
            InputSystemWaitInputAction action = target as InputSystemWaitInputAction;
            TextStudioActionDefinition definition = action != null ? action.ActionDefinition : null;
            VisualElement definitionText = new VisualElement
            {
                name = "ts-input-wait-action-definition"
            };
            definitionText.Add(TextStudioInspectorElements.CreateMutedText(
                definition != null ? definition.Description : "Pauses reveal until input arrives."));
            definitionText.Add(TextStudioInspectorElements.CreateMutedText(
                definition != null && definition.Examples.Count > 0
                    ? "Example: " + definition.Examples[0]
                    : "Example: <waitForInput>"));
            root.Add(definitionText);
            root.Add(TextStudioInspectorElements.CreateSection(
                "Tag",
                "ts-input-wait-action-tag",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_id", "Tag ID")));

            PropertyField mode = TextStudioInspectorElements.CreatePropertyField(serializedObject, "_inputMode", "Input Source");
            Label modeHelp = TextStudioInspectorElements.CreateMutedText(string.Empty);
            PropertyField defaultKey = TextStudioInspectorElements.CreatePropertyField(serializedObject, "_defaultInputKey", "Default Input Key");
            PropertyField defaultAction = TextStudioInspectorElements.CreatePropertyField(serializedObject, "_defaultInputAction", "Default Input Action");
            PropertyField mappings = TextStudioInspectorElements.CreatePropertyField(serializedObject, "_inputActions", "Keyed Input Actions");
            PropertyField fallback = TextStudioInspectorElements.CreatePropertyField(serializedObject, "_fallbackToTextStudioInputProvider", "Fallback To Text Studio Input");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Input",
                "ts-input-wait-action-input",
                mode,
                modeHelp,
                defaultKey,
                defaultAction,
                mappings,
                fallback));

            PropertyField autoEnable = TextStudioInspectorElements.CreatePropertyField(serializedObject, "_autoEnableInputAction", "Auto-enable Input Action");
            PropertyField restore = TextStudioInspectorElements.CreatePropertyField(serializedObject, "_restoreActionAfterFinish", "Restore Disabled State Afterwards");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Wait Behavior",
                "ts-input-wait-action-wait",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_maxSecondsWaiting", "Timeout (≤ 0 = No Timeout)"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_skippable", "Skip Request Can Continue"),
                autoEnable,
                restore));

            SerializedProperty inputMode = serializedObject.FindProperty("_inputMode");
            SerializedProperty autoEnableProperty = serializedObject.FindProperty("_autoEnableInputAction");
            void RefreshVisibility()
            {
                InputSystemWaitInputMode selected = (InputSystemWaitInputMode)inputMode.enumValueIndex;
                bool provider = selected == InputSystemWaitInputMode.TextStudioInputProvider;
                bool keyed = selected == InputSystemWaitInputMode.KeyedInputActions;
                defaultKey.style.display = keyed ? DisplayStyle.Flex : DisplayStyle.None;
                defaultAction.style.display = provider ? DisplayStyle.None : DisplayStyle.Flex;
                mappings.style.display = keyed ? DisplayStyle.Flex : DisplayStyle.None;
                fallback.style.display = provider ? DisplayStyle.None : DisplayStyle.Flex;
                autoEnable.style.display = provider ? DisplayStyle.None : DisplayStyle.Flex;
                bool enableActions = !provider && autoEnableProperty.boolValue;
                restore.style.display = enableActions ? DisplayStyle.Flex : DisplayStyle.None;
                modeHelp.text = selected switch
                {
                    InputSystemWaitInputMode.TextStudioInputProvider =>
                        "Uses Text Studio's active input provider. Choose this for a project-wide Continue/Advance abstraction; no Input Action asset is required.",
                    InputSystemWaitInputMode.DefaultInputAction =>
                        "Waits for one Input Action. Optional fallback lets Text Studio input continue if that action is unavailable.",
                    InputSystemWaitInputMode.KeyedInputActions =>
                        "The tag's input=<key> parameter selects a mapping. Empty or unmatched keys use the Default Input Action, then optional Text Studio fallback.",
                    _ => string.Empty
                };
            }
            root.TrackPropertyValue(inputMode, _ => RefreshVisibility());
            root.TrackPropertyValue(autoEnableProperty, _ => RefreshVisibility());
            RefreshVisibility();
            return root;
        }
    }
}
