#if USE_TIMELINE
using System;
using UnityEngine;
using UnityEngine.Playables;

namespace TextStudio.Timeline
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Text Studio/Integrations/Timeline/Receiver")]
    [HelpURL("https://www.wetzold.com/tools/text-studio/docs/")]
    public sealed class TextStudioTimelineReceiver : MonoBehaviour, INotificationReceiver
    {
        [Tooltip("Text Studio component controlled by markers on this PlayableDirector's marker track.")]
        [SerializeField] TextStudioText _target;

        public TextStudioText Target
        {
            get => ResolveTarget();
            set => _target = value;
        }

        public TextStudioText ConfiguredTarget => _target;

        void Reset()
        {
            _target = GetComponent<TextStudioText>();
            if (_target == null)
                _target = GetComponentInChildren<TextStudioText>(true);
        }

        public void OnNotify(Playable origin, INotification notification, object context)
        {
            if (notification is not TextStudioControlMarker marker)
                return;

            Execute(ResolveTarget(), marker.Command, marker.Text, marker.Value);
        }

        public static void Execute(
            TextStudioText target,
            TextStudioControlCommand command,
            string text,
            float value)
        {
            if (target == null)
                return;

            switch (command)
            {
                case TextStudioControlCommand.ShowText:
                    target.ShowText(text ?? string.Empty);
                    break;
                case TextStudioControlCommand.ShowTextInstant:
                    target.ShowTextInstant(text ?? string.Empty);
                    break;
                case TextStudioControlCommand.ClearText:
                    target.ClearText();
                    break;
                case TextStudioControlCommand.StartReveal:
                    target.StartReveal();
                    break;
                case TextStudioControlCommand.ContinueReveal:
                    target.ContinueReveal();
                    break;
                case TextStudioControlCommand.PauseReveal:
                    target.PauseReveal();
                    break;
                case TextStudioControlCommand.ResumeReveal:
                    target.ResumeReveal();
                    break;
                case TextStudioControlCommand.SkipCurrentWait:
                    target.SkipCurrentWait();
                    break;
                case TextStudioControlCommand.SkipReveal:
                    target.SkipReveal();
                    break;
                case TextStudioControlCommand.StopReveal:
                    target.StopReveal();
                    break;
                case TextStudioControlCommand.SetRevealSpeed:
                    target.SetRevealSpeed(Mathf.Max(0f, value));
                    break;
                case TextStudioControlCommand.SetRevealProgress:
                    target.SetRevealProgress(Mathf.Clamp01(value));
                    break;
                case TextStudioControlCommand.DriveRevealProgress:
                    target.DriveRevealProgress(Mathf.Clamp01(value));
                    break;
                case TextStudioControlCommand.TriggerRemainingEvents:
                    target.TriggerRemainingEvents();
                    break;
                case TextStudioControlCommand.StartDisappear:
                    target.StartDisappear();
                    break;
                case TextStudioControlCommand.SkipDisappear:
                    target.SkipDisappear();
                    break;
                case TextStudioControlCommand.StopDisappear:
                    target.StopDisappear();
                    break;
                case TextStudioControlCommand.RestartEffects:
                    target.RestartEffects();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }

        TextStudioText ResolveTarget()
        {
            if (_target != null)
                return _target;

            _target = GetComponent<TextStudioText>();
            if (_target == null)
                _target = GetComponentInChildren<TextStudioText>(true);
            return _target;
        }
    }
}
#endif
