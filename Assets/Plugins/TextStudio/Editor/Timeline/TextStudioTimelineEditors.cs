#if USE_TIMELINE
using System;
using TextStudio.Editor;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace TextStudio.Timeline.Editor
{
    [CustomTimelineEditor(typeof(TextStudioControlMarker))]
    internal sealed class TextStudioControlMarkerEditor : MarkerEditor
    {
        public override MarkerDrawOptions GetMarkerOptions(IMarker marker)
        {
            TextStudioControlMarker control = marker as TextStudioControlMarker;
            MarkerDrawOptions options = base.GetMarkerOptions(marker);
            if (control == null)
            {
                options.errorText = "Invalid Text Studio control marker.";
                return options;
            }

            options.tooltip = control.Command switch
            {
                TextStudioControlCommand.ShowText => "Text Studio: Show \"" + Summarize(control.Text) + "\"",
                TextStudioControlCommand.ShowTextInstant => "Text Studio: Show instantly \"" + Summarize(control.Text) + "\"",
                TextStudioControlCommand.SetRevealSpeed => "Text Studio: Reveal speed " + control.Value.ToString("0.##"),
                TextStudioControlCommand.SetRevealProgress => "Text Studio: Reveal progress " + Mathf.Clamp01(control.Value).ToString("0.##"),
                TextStudioControlCommand.DriveRevealProgress => "Text Studio: Drive reveal " + Mathf.Clamp01(control.Value).ToString("0.##"),
                _ => "Text Studio: " + ObjectNames.NicifyVariableName(control.Command.ToString())
            };
            return options;
        }

        static string Summarize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "(empty)";

            string singleLine = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return singleLine.Length <= 32
                ? singleLine
                : singleLine.Substring(0, 29) + "...";
        }
    }

    [CustomTimelineEditor(typeof(TextRevealClip))]
    internal sealed class TextStudioRevealClipEditor : ClipEditor
    {
        public override ClipDrawOptions GetClipOptions(TimelineClip clip)
        {
            ClipDrawOptions options = base.GetClipOptions(clip);
            TextRevealClip reveal = clip.asset as TextRevealClip;
            if (reveal == null)
            {
                options.errorText = "Missing Text Studio reveal clip asset.";
                return options;
            }

            options.tooltip = string.IsNullOrWhiteSpace(reveal.text)
                ? "Reveal the bound Text Studio text."
                : "Reveal: " + Summarize(reveal.text);
            return options;
        }

        public override void OnClipChanged(TimelineClip clip)
        {
            TextRevealClip reveal = clip.asset as TextRevealClip;
            if (reveal == null)
                return;

            clip.displayName = string.IsNullOrWhiteSpace(reveal.text)
                ? "Text Reveal"
                : "Reveal: " + Summarize(reveal.text);
        }

        static string Summarize(string value)
        {
            string singleLine = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return singleLine.Length <= 34
                ? singleLine
                : singleLine.Substring(0, 31) + "...";
        }
    }

    [CustomTimelineEditor(typeof(TextEffectClip))]
    internal sealed class TextStudioEffectClipEditor : ClipEditor
    {
        public override ClipDrawOptions GetClipOptions(TimelineClip clip)
        {
            ClipDrawOptions options = base.GetClipOptions(clip);
            TextEffectClip effect = clip.asset as TextEffectClip;
            if (effect == null || effect.effect == null)
            {
                options.errorText = "Assign a Text Studio effect asset.";
                return options;
            }

            options.tooltip =
                effect.effect.name + "  •  " +
                effect.range.ToString();
            return options;
        }

        public override void OnClipChanged(TimelineClip clip)
        {
            TextEffectClip effect = clip.asset as TextEffectClip;
            clip.displayName = effect != null && effect.effect != null
                ? effect.effect.name
                : "Text Effect";
        }

    }

    public sealed class TextStudioTimelineIntegrationProvider :
        ITextStudioIntegrationEditorProvider
    {
        public string IntegrationId => "timeline";

        public bool CanConfigure(TextStudioIntegrationValidationContext context)
        {
            return context?.SelectedGameObject != null &&
                   context.SelectedGameObject.GetComponent<PlayableDirector>() != null;
        }

        public void Configure(TextStudioIntegrationValidationContext context)
        {
            GameObject directorObject = context?.SelectedGameObject;
            if (directorObject == null)
                return;

            TextStudioTimelineReceiver receiver =
                directorObject.GetComponent<TextStudioTimelineReceiver>();
            if (receiver == null)
                receiver = Undo.AddComponent<TextStudioTimelineReceiver>(directorObject);

            TextStudioText target = directorObject.GetComponent<TextStudioText>();
            if (target == null)
                target = directorObject.GetComponentInChildren<TextStudioText>(true);

            if (target != null)
            {
                Undo.RecordObject(receiver, "Configure Text Studio Timeline Receiver");
                receiver.Target = target;
                EditorUtility.SetDirty(receiver);
            }
        }

        public void Validate(
            TextStudioIntegrationValidationContext context,
            System.Collections.Generic.ICollection<TextStudioIntegrationIssue> issues)
        {
            GameObject selected = context?.SelectedGameObject;
            PlayableDirector director = selected != null
                ? selected.GetComponent<PlayableDirector>()
                : null;
            if (director == null)
                return;

            TextStudioTimelineReceiver receiver =
                selected.GetComponent<TextStudioTimelineReceiver>();
            if (receiver == null || receiver.ConfiguredTarget == null)
            {
                issues.Add(new TextStudioIntegrationIssue(
                    TextStudioIntegrationIssueSeverity.Warning,
                    "Timeline marker receiver is not configured",
                    "The selected PlayableDirector cannot route Text Studio control markers.",
                    "Use Configure Selected."));
            }
        }
    }
}
#endif
