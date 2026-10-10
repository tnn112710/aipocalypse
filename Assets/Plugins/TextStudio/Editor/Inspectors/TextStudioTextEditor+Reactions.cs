using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal partial class TextStudioTextEditor
    {
        const string ReactionTestEditModeMessage =
            "Reaction testing is available in Play Mode. Enter Play Mode to send signals and cancel active reactions.";

        VisualElement CreateReactionTestSection(TextStudioText hub)
        {
            VisualElement root = CreateSection("Test Reactions");
            root.name = "ts-reaction-test-controls";
            HelpBox editModeNotice = CreateHelpBox(ReactionTestEditModeMessage, HelpBoxMessageType.Info);
            editModeNotice.name = "ts-reaction-test-edit-mode";
            root.Add(editModeNotice);

            VisualElement playBody = new VisualElement { name = "ts-reaction-test-play-mode" };
            root.Add(playBody);
            Label restriction = CreateAlignedHintLabel(string.Empty, "ts-reaction-test-restriction");
            VisualElement restrictionRow = CreateAlignedHintRow(restriction);
            playBody.Add(restrictionRow);
            DropdownField signal = new DropdownField("Signal")
            {
                name = "ts-reaction-test-signal",
                tooltip = "Named trigger sent with PlayReaction. Reactions set to When: Signal respond when their Signal Name matches exactly or is blank."
            };
            signal.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            playBody.Add(signal);
            TextField custom = new TextField("Custom Signal")
            {
                name = "ts-reaction-test-custom-signal",
                tooltip = "Exact signal name to send. Leading and trailing spaces are removed."
            };
            custom.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            playBody.Add(custom);
            Label signalInfo = CreateAlignedHintLabel(string.Empty, "ts-reaction-test-signal-info");
            VisualElement signalInfoRow = CreateAlignedHintRow(signalInfo);
            playBody.Add(signalInfoRow);

            List<string> names = new List<string>();
            List<string> discovered = new List<string>();
            bool acceptsAnyName = false;
            bool customSelected = false;
            bool customChosenByUser = false;
            bool refreshingChoices = false;
            string selectedName = null;
            string customLabel = "Custom...";

            bool CanTest() => Application.isPlaying && targets.Length == 1 && hub != null && hub.isActiveAndEnabled;

            string ResolveSignal() => customSelected
                ? (custom.value ?? string.Empty).Trim()
                : selectedName ?? string.Empty;

            Button trigger = TextStudioInspectorElements.CreateButton("Trigger Signal",
                "Send the selected signal to this Play Mode component.", () =>
                {
                    string name = ResolveSignal();
                    if (CanTest() && name.Length > 0)
                        hub.PlayReaction(name);
                });
            trigger.name = "ts-reaction-test-trigger";
            Button cancel = TextStudioInspectorElements.CreateButton("Cancel Active",
                "Stop this component's active Reactions and release their owned outputs.", () =>
                {
                    if (CanTest())
                        hub.CancelReactions();
                });
            cancel.name = "ts-reaction-test-cancel";
            VisualElement buttons = new VisualElement();
            buttons.AddToClassList("ts-button-row");
            buttons.Add(trigger);
            buttons.Add(cancel);
            playBody.Add(buttons);
            HelpBox captureDiagnostic = CreateHelpBox(string.Empty, HelpBoxMessageType.Warning);
            captureDiagnostic.name = "ts-reaction-capture-diagnostic";
            captureDiagnostic.style.display = DisplayStyle.None;
            playBody.Add(captureDiagnostic);

            void UpdateChoices()
            {
                customLabel = "Custom...";
                for (int suffix = 2; TextStudioReactionInspectorContext.IndexOfOrdinal(names, customLabel) >= 0; suffix++)
                    customLabel = "Custom (" + suffix + ")...";
                if (customSelected && !customChosenByUser && names.Count > 0)
                    customSelected = false;
                if (!customSelected && (selectedName == null ||
                                        TextStudioReactionInspectorContext.IndexOfOrdinal(names, selectedName) < 0))
                {
                    selectedName = names.Count > 0 ? names[0] : null;
                    customSelected = selectedName == null;
                }
                List<string> choices = new List<string>(names) { customLabel };
                refreshingChoices = true;
                try
                {
                    signal.choices = choices;
                    signal.SetValueWithoutNotify(customSelected ? customLabel : selectedName);
                }
                finally
                {
                    refreshingChoices = false;
                }
                string info = names.Count == 0 ? "This recipe has no named signal triggers." : string.Empty;
                if (acceptsAnyName)
                    info += (info.Length > 0 ? " " : string.Empty) +
                            "A Signal reaction with a blank Signal Name responds to any name you send.";
                signalInfo.text = info;
                signalInfoRow.style.display = info.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            void RefreshSignals(bool force)
            {
                TextStudioReactionInspectorContext.CollectSignalTriggers(
                    TextStudioReactionInspectorContext.ResolveEffectiveLayers(hub), discovered, out bool anyName);
                bool changed = force || anyName != acceptsAnyName || discovered.Count != names.Count;
                for (int i = 0; !changed && i < names.Count; i++)
                    changed = !string.Equals(names[i], discovered[i], System.StringComparison.Ordinal);
                if (!changed)
                    return;
                names.Clear();
                names.AddRange(discovered);
                acceptsAnyName = anyName;
                UpdateChoices();
            }

            void Refresh(bool forceChoices)
            {
                bool playing = Application.isPlaying;
                editModeNotice.style.display = playing ? DisplayStyle.None : DisplayStyle.Flex;
                playBody.style.display = playing ? DisplayStyle.Flex : DisplayStyle.None;
                RefreshSignals(forceChoices);
                bool single = targets.Length == 1;
                string restrictionText = !single
                    ? "Select one Text Studio component to trigger or cancel its Reactions."
                    : hub == null || !hub.isActiveAndEnabled
                        ? "Enable this component and its GameObject to trigger or cancel Reactions."
                        : string.Empty;
                restriction.text = restrictionText;
                restrictionRow.style.display = restrictionText.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                custom.style.display = customSelected ? DisplayStyle.Flex : DisplayStyle.None;
                bool available = CanTest();
                trigger.SetEnabled(available && ResolveSignal().Length > 0);
                cancel.SetEnabled(available);
                string diagnostic = single && hub != null ? hub.LastReactionCaptureDiagnostic : null;
                bool hasDiagnostic = !string.IsNullOrEmpty(diagnostic);
                captureDiagnostic.style.display = hasDiagnostic ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasDiagnostic)
                {
                    string message = "Last outgoing capture: " + diagnostic;
                    if (captureDiagnostic.text != message) captureDiagnostic.text = message;
                }
            }

            signal.RegisterValueChangedCallback(evt =>
            {
                if (refreshingChoices) return;
                customSelected = evt.newValue == customLabel;
                customChosenByUser = customSelected;
                if (!customSelected) selectedName = evt.newValue;
                Refresh(false);
            });
            custom.RegisterValueChangedCallback(_ => Refresh(false));
            void OnPlayModeChanged(PlayModeStateChange _)
            {
                if (this != null && root.panel != null)
                    Refresh(true);
            }
            root.RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.playModeStateChanged += OnPlayModeChanged);
            root.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.playModeStateChanged -= OnPlayModeChanged);
            TextStudioInspectorElements.SchedulePeriodicRefresh(root, () =>
            {
                if (Application.isPlaying && CanRunScheduledInspectorUpdate(root, hub))
                    Refresh(false);
            }, 250);
            Refresh(true);
            return root;
        }

        void CreateReactionAsset() => CreateReactionAsset(serializedObject, false);

        void CreateProfileReactionAsset(TextStudioBehaviorProfile profile) =>
            CreateReactionAsset(new SerializedObject(profile), true);

        void CreateReactionAsset(SerializedObject owner, bool rebuildTab)
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Reactions", "Text Studio Reactions",
                "asset", "Choose where to save the reusable reaction recipe.");
            if (string.IsNullOrEmpty(path)) return;
            TextStudioReactionAsset asset = ScriptableObject.CreateInstance<TextStudioReactionAsset>();
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create Text Studio Reactions");
            owner.Update();
            owner.FindProperty("_reactions.CustomAsset").objectReferenceValue = asset;
            owner.FindProperty("_reactions.Source").enumValueIndex = (int)TextStudioReactionSource.Custom;
            owner.ApplyModifiedProperties();
            EditorGUIUtility.PingObject(asset);
            if (rebuildTab)
                BuildUitkActiveTab((TextStudioText)target);
        }
    }
}
