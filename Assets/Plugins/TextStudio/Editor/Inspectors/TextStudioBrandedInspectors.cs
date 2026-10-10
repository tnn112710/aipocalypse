using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioSingleEventSystemGuard))]
    [CanEditMultipleObjects]
    internal sealed class TextStudioSingleEventSystemGuardEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-single-event-system-guard-inspector");
            root.Add(TextStudioInspectorElements.CreateMutedText(
                "Sample scenes can be loaded additively. This guard keeps the active Event System and removes duplicate sample Event Systems so UI input remains predictable."));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Fallback",
                "ts-single-event-system-guard-settings",
                TextStudioInspectorElements.CreatePropertyField(
                    serializedObject,
                    "_fallbackDelayFrames",
                    "Activation Delay",
                    tooltip: "Frames to wait before enabling this scene's Event System when no active Event System is available.")));
            return root;
        }
    }

    [CustomEditor(typeof(TextStudioDialogueSource))]
    [CanEditMultipleObjects]
    internal sealed class TextStudioDialogueSourceEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-dialogue-source-inspector");
            root.Add(TextStudioInspectorElements.CreateMutedText(
                "Connects dialogue, subtitle, and narrative systems to a Text Studio target."));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Target And Line",
                "ts-dialogue-source-target",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_target", "Text Studio Target"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_restartRevealOnLine", "Restart Reveal On Line"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_clearTextOnDisable", "Clear On Disable")));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Markup Preprocessing",
                "ts-dialogue-source-preprocessors",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_markupPreprocessors", "Preprocessors")));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Events",
                "ts-dialogue-source-events",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_onLineShown", "Line Shown"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_onLineComplete", "Line Complete"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_onAdvanceRequested", "Advance Requested")));
            return root;
        }
    }

    [CustomPropertyDrawer(typeof(TextStudioMarkerBinding))]
    internal sealed class TextStudioMarkerBindingDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = new VisualElement();
            SerializedProperty eventName = property.FindPropertyRelative("_routeId");
            TextField nameField = new TextField("Event Name") { bindingPath = eventName.propertyPath, tooltip = eventName.tooltip };
            nameField.labelElement.style.minWidth = 80;
            nameField.labelElement.style.width = 80;
            root.Add(nameField);
            SerializedProperty payload = property.FindPropertyRelative("_payloadMode");
            EnumField sendField = new EnumField("Send", (TextStudioMarkerPayloadMode)payload.enumValueIndex) { bindingPath = payload.propertyPath, tooltip = payload.tooltip };
            sendField.labelElement.style.minWidth = 80;
            sendField.labelElement.style.width = 80;
            root.Add(sendField);
            root.Add(new PropertyField(property.FindPropertyRelative("_onMarker"), "On Event"));
            return root;
        }
    }

    [CustomEditor(typeof(TextStudioMarkerEventRouter))]
    [CanEditMultipleObjects]
    internal sealed class TextStudioMarkerEventRouterEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-marker-event-router-inspector");
            root.Add(TextStudioInspectorElements.CreateMutedText("Uses the Text Studio Text on this object or its nearest parent."));
            Label example = TextStudioInspectorElements.CreateMutedText("For <event=door,open>, use event name door. The callback receives open when sending arguments.");
            example.enableRichText = false;
            root.Add(TextStudioInspectorElements.CreateSection(
                "Named Events",
                "ts-marker-event-router-bindings",
                example,
                TextStudioInspectorElements.CreateMutedText("Add an entry for each event name that needs a different response. Each entry can call several listeners."),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_bindings", "Events")));
            Foldout everyMarker = new Foldout { text = "Every Marker (optional)", name = "ts-marker-event-router-any", value = false };
            foreach (UnityEngine.Object inspected in targets)
                if (inspected is TextStudioMarkerEventRouter router && router.OnAnyMarker.GetPersistentEventCount() > 0)
                    everyMarker.value = true;
            everyMarker.tooltip = "A catch-all callback for every marker, including markers already handled by a named event.";
            everyMarker.Add(TextStudioInspectorElements.CreateMutedText("Runs for every marker, in addition to any matching named event. Leave empty when only specific events need a response."));
            everyMarker.Add(TextStudioInspectorElements.CreatePropertyField(serializedObject, "_anyMarkerPayloadMode", "Send"));
            everyMarker.Add(TextStudioInspectorElements.CreatePropertyField(serializedObject, "_onAnyMarker", "On Every Marker"));
            root.Add(everyMarker);
            root.Add(TextStudioInspectorElements.CreateMutedText("To test in Edit Mode, enable Run Actions in Preview on the text and choose Editor And Runtime on the UnityEvent listener. Dynamic string passes the event arguments; a static string uses the value you enter."));
            return root;
        }
    }

    [CustomEditor(typeof(TextStudioTypewriterEventRelay))]
    [CanEditMultipleObjects]
    internal sealed class TextStudioTypewriterEventRelayEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-typewriter-event-relay-inspector");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Typewriter Event Relay",
                "ts-typewriter-event-relay-summary",
                TextStudioInspectorElements.CreateMutedText("Forwards Text Studio reveal, wait, marker, and message events to inspector-authored UnityEvents.")));
            root.Add(TextStudioInspectorElements.CreateMutedText("Uses the Text Studio Text on this object or its nearest parent."));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Lifecycle",
                "ts-typewriter-event-relay-lifecycle",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_onTypewriterStarted", "Typewriter Started"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_onTextShown", "Text Shown"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_onTextDisappeared", "Text Disappeared")));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Reveal And Waits",
                "ts-typewriter-event-relay-reveal",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_onCharacterVisible", "Character Visible"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_onWaitStarted", "Wait Started"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_onWaitFinished", "Wait Finished")));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Messages",
                "ts-typewriter-event-relay-messages",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_onMessage", "Message")));
            return root;
        }
    }

    [CustomEditor(typeof(TextStudioPageNavigator))]
    [CanEditMultipleObjects]
    internal sealed class TextStudioPageNavigatorEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-page-navigator-inspector");
            TextStudioPageNavigator navigator = target as TextStudioPageNavigator;
            if (navigator != null && !Application.isPlaying)
                navigator.Refresh();

            root.Add(TextStudioInspectorElements.CreateSection(
                "Page Navigator",
                "ts-page-navigator-target",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_textStudio", "Text Studio Target"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_tmpText", "TMP Text"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_followReveal", "Follow Reveal")));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Events",
                "ts-page-navigator-events",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_onPageChanged", "Page Changed")));
            Button previous = TextStudioInspectorElements.CreateButton(
                "Previous Page",
                "Show the previous page.",
                () => MovePage(-1));
            previous.name = "ts-page-navigator-previous-page";
            Button next = TextStudioInspectorElements.CreateButton(
                "Next Page",
                "Show the next page.",
                () => MovePage(1));
            next.name = "ts-page-navigator-next-page";
            VisualElement controls = TextStudioInspectorElements.CreateButtonRow(previous, next);
            controls.name = "ts-page-navigator-page-controls";

            Label status = CreatePageStatusLabel(root, previous, next);
            root.Add(TextStudioInspectorElements.CreateSection(
                "Runtime",
                "ts-page-navigator-runtime",
                status,
                controls));
            return root;
        }

        Label CreatePageStatusLabel(VisualElement inspectorRoot, Button previous, Button next)
        {
            Label status = TextStudioInspectorElements.CreateMutedText("Page state updates while the component is active.", "ts-page-navigator-status");
            UpdatePageStatus(status, previous, next);
            TextStudioInspectorElements.SchedulePeriodicRefresh(inspectorRoot, () =>
            {
                TextStudioPageNavigator navigator = target as TextStudioPageNavigator;
                if (navigator != null && !Application.isPlaying)
                    navigator.Refresh();
                UpdatePageStatus(status, previous, next);
            }, 250);
            return status;
        }

        void MovePage(int delta)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                TextStudioPageNavigator navigator = targets[i] as TextStudioPageNavigator;
                if (navigator == null)
                    continue;

                navigator.Refresh();
                navigator.GoToPage(navigator.CurrentPage + delta);

                if (navigator.Target != null)
                    navigator.Target.ForceMeshRefresh(0f);
                else if (navigator.TMPText != null)
                    navigator.TMPText.ForceMeshUpdate();

                navigator.Refresh();
            }
        }

        void UpdatePageStatus(Label status, Button previous, Button next)
        {
            TextStudioPageNavigator navigator = target as TextStudioPageNavigator;
            if (navigator == null)
            {
                status.text = "Page state is unavailable.";
                previous.SetEnabled(false);
                next.SetEnabled(false);
                return;
            }

            status.text = "Current page " + navigator.CurrentPage + " of " + navigator.PageCount + ".";
            previous.SetEnabled(navigator.CurrentPage > 1);
            next.SetEnabled(navigator.CurrentPage < navigator.PageCount);
        }
    }

    [CustomEditor(typeof(TextStudioAutoScrollFollower))]
    [CanEditMultipleObjects]
    internal sealed class TextStudioAutoScrollFollowerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-auto-scroll-follower-inspector");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Auto Scroll Follower",
                "ts-auto-scroll-follower-target",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_textStudio", "Text Studio Target"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_tmpText", "TMP Text"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_scrollRect", "Scroll Rect"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_padding", "Viewport Padding")));
            return root;
        }
    }

    [CustomEditor(typeof(TextStudioTagExpansionSheet))]
    [CanEditMultipleObjects]
    internal sealed class TextStudioTagExpansionSheetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-tag-expansion-sheet-inspector");
            Label summary = TextStudioInspectorElements.CreateMutedText(string.Empty, "ts-tag-expansion-sheet-summary-text");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Tag Expansion Sheet",
                "ts-tag-expansion-sheet-summary",
                summary));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Entries",
                "ts-tag-expansion-sheet-entries",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_entries", "Expansions")));
            SerializedProperty entries = serializedObject.FindProperty("_entries");
            root.TrackPropertyValue(entries, _ => UpdateTagExpansionSummary(summary));
            UpdateTagExpansionSummary(summary);
            return root;
        }

        void UpdateTagExpansionSummary(Label summary)
        {
            SerializedProperty entries = serializedObject.FindProperty("_entries");
            int count = entries != null && entries.isArray ? entries.arraySize : 0;
            summary.text = count == 1 ? "1 expansion available." : count + " expansions available.";
        }
    }

    [CustomEditor(typeof(EffectPlaybackAsset))]
    [CanEditMultipleObjects]
    internal sealed class EffectPlaybackAssetEditorBranded : UnityEditor.Editor
    {
        double _previewStartTime;
        float _previewElapsed;
        bool _previewPlaying;
        bool _previewComplete;

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-effect-playback-inspector");
            Button oneShot = new Button(() => ApplyPlaybackPreset(
                PlaybackMode.Once,
                duration: 0.5f,
                hold: 0f,
                returnDuration: 0.5f,
                cycles: 1,
                completion: EffectPlaybackCompletion.HoldFinal))
            {
                name = "ts-effect-playback-preset-once",
                text = "One-Shot",
                tooltip = "Run forward once and keep the final value."
            };
            Button loop = new Button(() => ApplyPlaybackPreset(
                PlaybackMode.Loop,
                duration: 1f,
                hold: 0f,
                returnDuration: 1f,
                cycles: 0,
                completion: EffectPlaybackCompletion.Reset))
            {
                name = "ts-effect-playback-preset-loop",
                text = "Continuous Loop",
                tooltip = "Repeat forward cycles continuously with no gap."
            };
            Button pop = new Button(() => ApplyPlaybackPreset(
                PlaybackMode.PingPong,
                duration: 0.15f,
                hold: 0.2f,
                returnDuration: 0.25f,
                cycles: 1,
                completion: EffectPlaybackCompletion.Reset))
            {
                name = "ts-effect-playback-preset-pop",
                text = "Pop",
                tooltip = "Rise quickly, hold briefly, then return smoothly."
            };
            VisualElement presetButtons = new VisualElement
            {
                name = "ts-effect-playback-preset-buttons"
            };
            presetButtons.AddToClassList("ts-button-row");
            presetButtons.Add(oneShot);
            presetButtons.Add(loop);
            presetButtons.Add(pop);
            root.Add(TextStudioInspectorElements.CreateSection(
                "Quick Starts",
                "ts-effect-playback-presets",
                TextStudioInspectorElements.CreateMutedText(
                    "Choose a polished starting envelope, then tune only the timing that matters."),
                presetButtons));

            SerializedProperty modeProperty = serializedObject.FindProperty("_mode");
            PropertyField mode = TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                "_mode",
                "Mode",
                "ts-effect-playback-mode");
            Label explanation = TextStudioInspectorElements.CreateMutedText(
                string.Empty,
                "ts-effect-playback-explanation");
            PropertyField startDelay = TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                "_delayBeforeStart",
                "Start Delay",
                "ts-effect-playback-start-delay");
            PropertyField duration = TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                "_duration",
                "Forward Duration",
                "ts-effect-playback-duration");
            PropertyField hold = TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                "_holdDuration",
                "Peak Hold",
                "ts-effect-playback-hold");
            PropertyField returnDuration = TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                "_returnDuration",
                "Return Duration",
                "ts-effect-playback-return-duration");
            PropertyField cycleCount = TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                "_cycleCount",
                "Cycle Count",
                "ts-effect-playback-cycle-count");
            Label cycleHint = TextStudioInspectorElements.CreateMutedText(
                "Cycle Count 0 repeats forever. Positive values stop after that many cycles.",
                "ts-effect-playback-cycle-hint");
            PropertyField cycleDelay = TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                "_delayBetweenCycles",
                "Gap Between Cycles",
                "ts-effect-playback-cycle-delay");
            PropertyField completion = TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                "_completion",
                "After Completion",
                "ts-effect-playback-completion");
            PropertyField externalPreview = TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                "_externalPreviewValue",
                "Preview Input",
                "ts-effect-playback-external-preview");
            externalPreview.tooltip = "The normalized external value shown in this inspector preview. Runtime callers still provide the actual value.";
            PropertyField weight = TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                "_weightCurve",
                "Weight Curve",
                "ts-effect-playback-weight");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Playback Envelope",
                "ts-effect-playback-settings",
                mode,
                explanation,
                startDelay,
                duration,
                hold,
                returnDuration,
                cycleCount,
                cycleHint,
                cycleDelay,
                completion,
                externalPreview,
                weight));

            ProgressBar preview = new ProgressBar
            {
                name = "ts-effect-playback-preview",
                lowValue = 0f,
                highValue = 100f,
                title = "Output 0.00"
            };
            preview.style.height = 34f;
            Label previewState = TextStudioInspectorElements.CreateMutedText(
                "Ready",
                "ts-effect-playback-preview-state");
            Button playPause = new Button
            {
                name = "ts-effect-playback-preview-play-pause",
                text = "Pause"
            };
            Button restart = new Button(RestartPreview)
            {
                name = "ts-effect-playback-preview-restart",
                text = "Restart"
            };
            VisualElement previewButtons = new VisualElement();
            previewButtons.AddToClassList("ts-button-row");
            previewButtons.Add(playPause);
            previewButtons.Add(restart);
            root.Add(TextStudioInspectorElements.CreateSection(
                "Live Preview",
                "ts-effect-playback-preview-section",
                preview,
                previewState,
                previewButtons));

            RestartPreview();
            playPause.clicked += () =>
            {
                if (_previewPlaying)
                {
                    _previewElapsed = GetPreviewElapsed();
                    _previewPlaying = false;
                }
                else
                {
                    if (_previewComplete)
                        _previewElapsed = 0f;
                    _previewStartTime = EditorApplication.timeSinceStartup - _previewElapsed;
                    _previewPlaying = true;
                    _previewComplete = false;
                }
            };

            System.Action refresh = () =>
            {
                serializedObject.UpdateIfRequiredOrScript();
                if (modeProperty.hasMultipleDifferentValues)
                {
                    explanation.text = "Multiple playback modes are selected. Mode-specific fields remain visible for multi-editing.";
                    SetPlaybackFieldDisplay(startDelay, true);
                    SetPlaybackFieldDisplay(duration, true);
                    SetPlaybackFieldDisplay(hold, true);
                    SetPlaybackFieldDisplay(returnDuration, true);
                    SetPlaybackFieldDisplay(cycleCount, true);
                    SetPlaybackFieldDisplay(cycleHint, true);
                    SetPlaybackFieldDisplay(cycleDelay, true);
                    SetPlaybackFieldDisplay(completion, true);
                    SetPlaybackFieldDisplay(externalPreview, true);
                    SetPlaybackFieldDisplay(weight, true);
                    return;
                }

                PlaybackMode selected = (PlaybackMode)modeProperty.enumValueIndex;
                bool timed = selected != PlaybackMode.External;
                bool pingPong = selected == PlaybackMode.PingPong;
                bool repeating = selected == PlaybackMode.Loop || pingPong;
                bool hasHold = selected == PlaybackMode.Once ||
                               selected == PlaybackMode.PingPong ||
                               selected == PlaybackMode.Weighted;
                bool hasCompletionChoice = selected == PlaybackMode.Once ||
                                           selected == PlaybackMode.Loop ||
                                           selected == PlaybackMode.Weighted;
                SetPlaybackFieldDisplay(startDelay, timed);
                SetPlaybackFieldDisplay(duration, timed);
                SetPlaybackFieldDisplay(hold, hasHold);
                SetPlaybackFieldDisplay(returnDuration, pingPong);
                SetPlaybackFieldDisplay(cycleCount, repeating);
                SetPlaybackFieldDisplay(cycleHint, repeating);
                SetPlaybackFieldDisplay(cycleDelay, repeating);
                SetPlaybackFieldDisplay(completion, hasCompletionChoice);
                SetPlaybackFieldDisplay(externalPreview, selected == PlaybackMode.External);
                SetPlaybackFieldDisplay(weight, selected == PlaybackMode.Weighted);
                duration.label = GetDurationLabel(selected);
                hold.label = pingPong ? "Peak Hold" : "Final Hold";
                explanation.text = GetPlaybackDescription(selected);
            };

            root.TrackPropertyValue(modeProperty, _ => refresh());
            refresh();
            TextStudioInspectorElements.SchedulePeriodicRefresh(root, () =>
            {
                EffectPlaybackAsset playback = target as EffectPlaybackAsset;
                if (playback == null)
                    return;

                float elapsed = GetPreviewElapsed();
                float previewInput = playback.Mode == PlaybackMode.External
                    ? playback.ExternalPreviewValue
                    : elapsed;
                float output = playback.Evaluate(previewInput, 0f, out bool complete);
                preview.value = Mathf.Clamp01(output) * 100f;
                preview.title = "Output " + output.ToString("0.00");
                previewState.text = complete
                    ? "Complete at " + elapsed.ToString("0.00") + "s"
                    : (_previewPlaying ? "Playing · " : "Paused · ") + elapsed.ToString("0.00") + "s";
                if (complete && _previewPlaying)
                {
                    _previewElapsed = elapsed;
                    _previewPlaying = false;
                    _previewComplete = true;
                }
                playPause.text = _previewPlaying ? "Pause" : (_previewComplete ? "Play Again" : "Play");
            }, 33);
            return root;
        }

        void ApplyPlaybackPreset(
            PlaybackMode mode,
            float duration,
            float hold,
            float returnDuration,
            int cycles,
            EffectPlaybackCompletion completion)
        {
            serializedObject.Update();
            serializedObject.FindProperty("_mode").enumValueIndex = (int)mode;
            serializedObject.FindProperty("_delayBeforeStart").floatValue = 0f;
            serializedObject.FindProperty("_duration").floatValue = duration;
            serializedObject.FindProperty("_holdDuration").floatValue = hold;
            serializedObject.FindProperty("_returnDuration").floatValue = returnDuration;
            serializedObject.FindProperty("_cycleCount").intValue = cycles;
            serializedObject.FindProperty("_delayBetweenCycles").floatValue = 0f;
            serializedObject.FindProperty("_completion").enumValueIndex = (int)completion;
            serializedObject.ApplyModifiedProperties();
            RestartPreview();
        }

        void RestartPreview()
        {
            _previewElapsed = 0f;
            _previewStartTime = EditorApplication.timeSinceStartup;
            _previewPlaying = true;
            _previewComplete = false;
        }

        float GetPreviewElapsed()
        {
            return _previewPlaying
                ? Mathf.Max(0f, (float)(EditorApplication.timeSinceStartup - _previewStartTime))
                : _previewElapsed;
        }

        static void SetPlaybackFieldDisplay(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static string GetDurationLabel(PlaybackMode mode)
        {
            switch (mode)
            {
                case PlaybackMode.Loop:
                    return "Cycle Duration";
                case PlaybackMode.Weighted:
                    return "Curve Duration";
                default:
                    return "Forward Duration";
            }
        }

        static string GetPlaybackDescription(PlaybackMode mode)
        {
            switch (mode)
            {
                case PlaybackMode.Once:
                    return "Runs from 0 to 1 once after the optional start delay, can hold at the final value, then either remains applied or resets.";
                case PlaybackMode.Loop:
                    return "Repeats forward 0-to-1 cycles with an optional gap. Use a finite Cycle Count for bursts, or 0 for continuous playback.";
                case PlaybackMode.PingPong:
                    return "Runs forward to 1, optionally holds at the peak, then returns smoothly to 0. Forward and return durations can differ.";
                case PlaybackMode.Weighted:
                    return "Samples the Weight Curve once over Curve Duration, with an optional final hold. This is ideal for authored pops, emphasis envelopes, and irregular easing.";
                case PlaybackMode.External:
                    return "Uses the normalized value supplied by an external clock such as Timeline. Local timing controls are intentionally hidden.";
                default:
                    return "Controls reusable effect progress over time.";
            }
        }
    }

    [CustomEditor(typeof(TextStudioActionAsset), true)]
    [CanEditMultipleObjects]
    internal sealed class TextStudioActionAssetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-action-asset-inspector");
            root.Add(TextStudioActionInspectorUtility.CreateActionDefinitionSection(target as ITextStudioActionDefinitionProvider));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Settings",
                "ts-action-asset-settings",
                TextStudioInspectorElements.CreateDefaultProperties(serializedObject)));
            return root;
        }
    }

    [CustomEditor(typeof(TextStudioActionComponent), true)]
    [CanEditMultipleObjects]
    internal sealed class TextStudioActionComponentEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-action-component-inspector");
            root.Add(TextStudioActionInspectorUtility.CreateActionDefinitionSection(target as ITextStudioActionDefinitionProvider));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Settings",
                "ts-action-component-settings",
                TextStudioInspectorElements.CreateDefaultProperties(serializedObject)));
            return root;
        }
    }

    static class TextStudioActionInspectorUtility
    {
        public static VisualElement CreateActionDefinitionSection(ITextStudioActionDefinitionProvider provider)
        {
            VisualElement content = new VisualElement();
            if (provider != null && provider.ActionDefinition != null)
            {
                TextStudioActionDefinition definition = provider.ActionDefinition;
                content.Add(TextStudioInspectorElements.CreateMutedText("ID: " + definition.Id));
                content.Add(TextStudioInspectorElements.CreateMutedText(definition.Description));
                IReadOnlyList<string> examples = definition.Examples;
                if (examples != null && examples.Count > 0)
                    content.Add(TextStudioInspectorElements.CreateMutedText("Example: " + examples[0]));
            }
            else
            {
                content.Add(TextStudioInspectorElements.CreateMutedText("Runs a Text Studio action tag."));
            }

            return TextStudioInspectorElements.CreateSection("Action", "ts-action-asset-definition", content);
        }
    }
}
