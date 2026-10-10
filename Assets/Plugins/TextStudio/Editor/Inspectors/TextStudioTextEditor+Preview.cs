using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal partial class TextStudioTextEditor
    {
        VisualElement BuildUitkPreviewPanel(TextStudioText hub)
        {
            VisualElement panel = CreateSection("Preview");
            panel.name = "ts-preview-panel";
            panel.AddToClassList("ts-preview-section");
            panel.style.display = DisplayStyle.Flex;
            Action refreshPreviewState = null;

            VisualElement liveBody = new VisualElement
            {
                name = "ts-preview-live-body"
            };
            liveBody.AddToClassList("ts-preview-live-body");

            panel.Add(BuildUitkAuthoringModeControl(hub, liveBody));
            panel.Add(liveBody);
            TextStudioAuthoringPreviewControls previewControls =
                new TextStudioAuthoringPreviewControls(new TextStudioAuthoringPreviewContext
                {
                    Preview = hub.Preview,
                    DisplayProgress = () =>
                        hub.Preview.ShowEndState ? hub.RevealProgress : hub.Preview.Progress,
                    IsRevealing = () =>
                        hub.IsRevealing &&
                        hub.TotalGlyphCount > 0 &&
                        hub.RevealedGlyphCount < hub.TotalGlyphCount,
                    IsDisappearing = () => hub.IsDisappearing,
                    IsWaitingForAction = () => hub.IsWaitingForAction,
                    IsRevealPaused = () => hub.IsRevealPaused,
                    SkipReveal = hub.SkipReveal,
                    ResumeReveal = hub.ResumeReveal,
                    SkipCurrentWait = hub.SkipCurrentWait,
                    StateChanged = () => refreshPreviewState?.Invoke()
                });
            previewControls.AddTo(liveBody);

            Toggle runActions = new Toggle("Run Actions")
            {
                name = "ts-preview-run-actions-toggle",
                tooltip = "Allow action tags to execute during edit-mode preview playback."
            };
            runActions.AddToClassList("ts-preview-run-actions-toggle");
            runActions.SetValueWithoutNotify(_runActionsInPreview.boolValue);
            runActions.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                _runActionsInPreview.boolValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
                hub.SetRunActionsInPreview(evt.newValue);
                EditorUtility.SetDirty(hub);
                refreshPreviewState?.Invoke();
            });
            liveBody.Add(runActions);

            Label runtimeTagHint = new Label();
            runtimeTagHint.name = "ts-preview-runtime-tags-hint";
            runtimeTagHint.AddToClassList("ts-muted-text");
            liveBody.Add(runtimeTagHint);

            refreshPreviewState = () =>
            {
                serializedObject.UpdateIfRequiredOrScript();
                previewControls.Refresh();
                runActions.SetValueWithoutNotify(_runActionsInPreview.boolValue);
                List<string> previewRunActionTags = CollectPreviewRunActionTags(hub);
                bool showRunActions = previewRunActionTags.Count > 0;
                runActions.style.display = showRunActions ? DisplayStyle.Flex : DisplayStyle.None;
                runActions.SetEnabled(showRunActions);
                string hint = BuildSkippedEditModeRuntimeTagHint(hub, previewRunActionTags);
                runtimeTagHint.text = hint;
                runtimeTagHint.style.display =
                    string.IsNullOrEmpty(hint) ? DisplayStyle.None : DisplayStyle.Flex;
            };
            refreshPreviewState();

            TextStudioInspectorElements.SchedulePeriodicRefresh(panel, () =>
            {
                if (CanRunScheduledInspectorUpdate(panel, hub))
                    refreshPreviewState();
            }, 120);

            return panel;
        }

        internal static Button CreatePreviewSegmentButton(string text, string tooltip, Action action)
        {
            Button button = new Button(action)
            {
                text = text,
                tooltip = tooltip
            };
            button.AddToClassList("ts-preview-segment-button");
            return button;
        }

        internal static string GetPreviewSegmentLabel(TextStudioPreviewSegment segment)
        {
            if (segment == TextStudioPreviewSegment.Reveal)
                return "Reveal";
            if (segment == TextStudioPreviewSegment.Active)
                return "Active";
            return "Hide";
        }

        static void TogglePreviewSegment(TextStudioText hub, TextStudioPreviewSegment segment)
        {
            if (hub == null)
                return;

            bool enabled = hub.Preview.IsSegmentEnabled(segment);
            hub.Preview.SetSegmentEnabled(segment, !enabled);
        }

        static void SyncPreviewSegmentButton(TextStudioText hub, Button button, TextStudioPreviewSegment segment)
        {
            if (hub == null || button == null)
                return;

            bool active = hub.Preview.IsSegmentEnabled(segment);
            SyncPreviewSegmentButton(button, segment, active);
        }

        internal static void SyncPreviewSegmentButton(
            Button button,
            TextStudioPreviewSegment segment,
            bool active)
        {
            if (button == null)
                return;

            button.text = GetPreviewSegmentLabel(segment) + ": " + (active ? "On" : "Off");
            SetClass(button, "ts-preview-segment-selected", active);
            SetClass(button, "ts-preview-segment-off", !active);
        }

        static void SyncPreviewSpeedButtons(TextStudioText hub, List<Button> speedButtons)
        {
            if (hub == null || speedButtons == null)
                return;

            for (int i = 0; i < speedButtons.Count; i++)
            {
                Button speedButton = speedButtons[i];
                SetClass(
                    speedButton,
                    "ts-authoring-mode-active",
                    speedButton != null &&
                    speedButton.userData is float previewSpeed &&
                    Mathf.Approximately(hub.Preview.PlaybackSpeed, previewSpeed));
            }
        }

        static void SyncPreviewPlaybackButton(TextStudioText hub, Button playPause)
        {
            if (hub == null || playPause == null)
                return;

            playPause.text = hub.Preview.IsPlaying ? "Pause" : "Play";
            playPause.tooltip = hub.Preview.IsPlaying
                ? "Pause the complete preview at its current state."
                : "Play or resume the selected preview phases.";
        }

        static void InvokeMarkerTest(TextStudioText hub)
        {
            TextStudioMarkerEventRouter router = hub != null ? hub.GetComponent<TextStudioMarkerEventRouter>() : null;
            if (router == null)
            {
                Debug.LogWarning("[Text Studio] Add TextStudioMarkerEventRouter to test marker routes.", hub);
                return;
            }

            router.RouteNow(s_markerRoute, ParseMarkerParameters(s_markerParameters));
            Debug.Log("[Text Studio] Marker test invoked: " + s_markerRoute, router);
        }

        static string[] ParseMarkerParameters(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();

            string[] raw = text.Split(',');
            List<string> parameters = new List<string>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                string value = raw[i].Trim();
                if (!string.IsNullOrEmpty(value))
                    parameters.Add(value);
            }

            return parameters.ToArray();
        }

        static void RefreshPreviewRuntimeControls(
            TextStudioText hub,
            Toggle runActions,
            Button endState,
            Button skipReveal,
            Button hide,
            Button continueReveal,
            Button skipWait,
            Label runtimeTagHint)
        {
            List<string> previewRunActionTags = null;
            if (runActions != null || runtimeTagHint != null)
                previewRunActionTags = CollectPreviewRunActionTags(hub);

            if (runActions != null)
            {
                bool showRunActions = previewRunActionTags != null && previewRunActionTags.Count > 0;
                runActions.style.display = showRunActions ? DisplayStyle.Flex : DisplayStyle.None;
                runActions.style.visibility = Visibility.Visible;
                runActions.SetEnabled(showRunActions);
            }

            if (endState != null)
            {
                bool showEndState = ShouldShowPreviewEndState(hub);
                endState.style.display = showEndState ? DisplayStyle.Flex : DisplayStyle.None;
                endState.SetEnabled(showEndState);
            }

            if (skipReveal != null)
            {
                bool showSkipReveal = ShouldShowPreviewSkipReveal(hub);
                skipReveal.style.display = showSkipReveal ? DisplayStyle.Flex : DisplayStyle.None;
                skipReveal.SetEnabled(showSkipReveal);
            }

            if (hide != null)
            {
                bool showHide = ShouldShowPreviewHide(hub);
                hide.style.display = showHide ? DisplayStyle.Flex : DisplayStyle.None;
                hide.SetEnabled(showHide);
            }

            if (continueReveal != null)
            {
                bool showContinue = ShouldShowPreviewContinue(hub);
                continueReveal.style.display = showContinue ? DisplayStyle.Flex : DisplayStyle.None;
                continueReveal.style.visibility = Visibility.Visible;
                continueReveal.SetEnabled(showContinue);
            }

            if (skipWait != null)
            {
                bool showSkipWait = ShouldShowPreviewSkipWait(hub);
                skipWait.style.display = showSkipWait ? DisplayStyle.Flex : DisplayStyle.None;
                skipWait.style.visibility = Visibility.Visible;
                skipWait.SetEnabled(showSkipWait);
            }

            if (runtimeTagHint == null)
                return;

            string hint = BuildSkippedEditModeRuntimeTagHint(hub, previewRunActionTags);
            runtimeTagHint.text = hint;
            runtimeTagHint.style.display = string.IsNullOrEmpty(hint) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        internal static bool ShouldShowPreviewEndState(TextStudioText hub)
        {
            return hub != null && !hub.Preview.ShowEndState;
        }

        internal static bool ShouldShowPreviewSkipReveal(TextStudioText hub)
        {
            return hub != null &&
                   hub.IsRevealing &&
                   hub.TotalGlyphCount > 0 &&
                   hub.RevealedGlyphCount < hub.TotalGlyphCount;
        }

        internal static bool ShouldShowPreviewHide(TextStudioText hub)
        {
            return hub != null &&
                   !hub.Preview.IsSegmentEnabled(TextStudioPreviewSegment.Hide) &&
                   !hub.IsDisappearing;
        }

        internal static bool ShouldShowPreviewSkipWait(TextStudioText hub)
        {
            return hub != null && hub.IsWaitingForAction;
        }

        internal static bool ShouldShowPreviewContinue(TextStudioText hub)
        {
            return hub != null && hub.IsRevealPaused && !hub.IsWaitingForAction;
        }

        internal static string BuildSkippedEditModeRuntimeTagHint(TextStudioText hub)
        {
            return BuildSkippedEditModeRuntimeTagHint(hub, CollectPreviewRunActionTags(hub));
        }

        internal static bool ShouldShowPreviewRunActions(TextStudioText hub)
        {
            List<string> previewRunActionTags = CollectPreviewRunActionTags(hub);
            return previewRunActionTags.Count > 0;
        }

        static string BuildSkippedEditModeRuntimeTagHint(TextStudioText hub, List<string> previewRunActionTags)
        {
            if (hub == null || previewRunActionTags == null || previewRunActionTags.Count == 0)
                return string.Empty;

            if (!hub.RunActionsInPreview)
                return "Preview actions are off: " + string.Join(", ", previewRunActionTags) + ".";

            return string.Empty;
        }

        static List<string> CollectPreviewRunActionTags(TextStudioText hub)
        {
            List<string> tags = new List<string>();
            if (hub == null || Application.isPlaying)
                return tags;

            string source = hub.Markup;
            if (string.IsNullOrWhiteSpace(source))
                return tags;

            TextStudioTagAnalysisContext context = TextStudioTagInspectorContext.Build(hub, source);
            TextStudioDiagnosticReport report = TextStudioAppearanceAuthoringMetadata.GetAnalysis(context, source);

            for (int i = 0; i < report.Tags.Length; i++)
            {
                TextStudioTagDiagnostic tag = report.Tags[i];
                if (tag.IsClosing ||
                    (tag.Kind != TextStudioTagKind.Action && tag.Kind != TextStudioTagKind.Marker))
                    continue;

                if (IsPreviewSafeAction(hub, tag.Name))
                    continue;

                string displayName = "<" + tag.Name + ">";
                if (!ContainsOrdinalIgnoreCase(tags, displayName))
                    tags.Add(displayName);
            }

            return tags;
        }

        static bool IsPreviewSafeAction(TextStudioText hub, string actionId)
        {
            if (hub != null)
            {
                ITextStudioAction[] localActions = hub.GetComponents<ITextStudioAction>();
                for (int i = 0; i < localActions.Length; i++)
                {
                    ITextStudioAction action = localActions[i];
                    if (!IsUsableLocalAction(action) ||
                        !TextStudioActionDefinitionUtility.MatchesName(action, actionId))
                    {
                        continue;
                    }

                    return action is ITextStudioEditModePreviewAction;
                }
            }

            if (BuiltInActions.TryGetCanonicalActionId(actionId, out string canonicalActionId))
                actionId = canonicalActionId;

            return TextStudioActions.TryGet(actionId, out ITextStudioAction registeredAction) &&
                   registeredAction is ITextStudioEditModePreviewAction;
        }

        static bool IsUsableLocalAction(ITextStudioAction action)
        {
            if (action == null)
                return false;

            if (action is Object unityObject && unityObject == null)
                return false;

            return action is not Behaviour behaviour || behaviour.isActiveAndEnabled;
        }
    }
}
