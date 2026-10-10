#if USE_TIMELINE
using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace TextStudio.Timeline
{
    public enum TextStudioControlCommand
    {
        ShowText,
        ShowTextInstant,
        ClearText,
        StartReveal,
        ContinueReveal,
        PauseReveal,
        ResumeReveal,
        SkipCurrentWait,
        SkipReveal,
        StopReveal,
        SetRevealSpeed,
        SetRevealProgress,
        DriveRevealProgress,
        TriggerRemainingEvents,
        StartDisappear,
        SkipDisappear,
        StopDisappear,
        RestartEffects
    }

    [Serializable]
    [DisplayName("Text Studio Control")]
    public sealed class TextStudioControlMarker :
        Marker,
        INotification,
        INotificationOptionProvider
    {
        static readonly PropertyName NotificationId = new PropertyName("TextStudioControl");

        [Tooltip("Text Studio command to invoke when Timeline passes this marker.")]
        [SerializeField] TextStudioControlCommand _command = TextStudioControlCommand.StartReveal;

        [Tooltip("Text or markup used by Show Text and Show Text Instantly commands.")]
        [TextArea(2, 5)]
        [SerializeField] string _text = string.Empty;

        [Tooltip("Numeric value used by reveal speed and reveal progress commands.")]
        [SerializeField] float _value = 1f;

        [Tooltip("Trigger when playback starts after this marker.")]
        [SerializeField] bool _retroactive;

        [Tooltip("Trigger no more than once during the lifetime of the playable graph.")]
        [SerializeField] bool _triggerOnce;

        [Tooltip("Allow this command to run while Timeline is evaluated outside Play Mode.")]
        [SerializeField] bool _triggerInEditMode;

        public TextStudioControlCommand Command
        {
            get => _command;
            set => _command = value;
        }

        public string Text
        {
            get => _text;
            set => _text = value ?? string.Empty;
        }

        public float Value
        {
            get => _value;
            set => _value = value;
        }

        public bool TriggerInEditMode
        {
            get => _triggerInEditMode;
            set => _triggerInEditMode = value;
        }

        public PropertyName id => NotificationId;

        public NotificationFlags flags =>
            (_retroactive ? NotificationFlags.Retroactive : default) |
            (_triggerOnce ? NotificationFlags.TriggerOnce : default) |
            (_triggerInEditMode ? NotificationFlags.TriggerInEditMode : default);
    }

}
#endif
