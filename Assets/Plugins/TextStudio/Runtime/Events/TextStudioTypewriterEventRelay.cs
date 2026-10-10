using System;
using UnityEngine;
using UnityEngine.Events;

namespace TextStudio
{
    /// <summary>Serialized callback carrying a visible-glyph index and displayed text.</summary>
    [Serializable]
    public sealed class TextStudioCharacterVisibleEvent : UnityEvent<int, string> { }

    /// <summary>Serialized callback carrying glyph index, character, wait kind, and duration in seconds.</summary>
    [Serializable]
    public sealed class TextStudioTypewriterWaitEvent : UnityEvent<int, string, string, float> { }

    /// <summary>Serialized callback carrying a message channel and payload.</summary>
    [Serializable]
    public sealed class TextStudioMessageEvent : UnityEvent<string, string> { }

    /// <summary>Bridges Text Studio playback events to serialized UnityEvents for scene authoring.</summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("Text Studio/Events/Typewriter Event Relay")]
    [HelpURL("https://www.wetzold.com/tools/text-studio")]
    public sealed class TextStudioTypewriterEventRelay : MonoBehaviour
    {
        [SerializeField] UnityEvent _onTypewriterStarted = new UnityEvent();
        [SerializeField] UnityEvent _onTextShown = new UnityEvent();
        [SerializeField] UnityEvent _onTextDisappeared = new UnityEvent();
        [SerializeField] TextStudioCharacterVisibleEvent _onCharacterVisible = new TextStudioCharacterVisibleEvent();
        [SerializeField] TextStudioTypewriterWaitEvent _onWaitStarted = new TextStudioTypewriterWaitEvent();
        [SerializeField] TextStudioTypewriterWaitEvent _onWaitFinished = new TextStudioTypewriterWaitEvent();
        [SerializeField] TextStudioMessageEvent _onMessage = new TextStudioMessageEvent();

        TextStudioText _subscribedTarget;

        public TextStudioText Target => ResolveTarget();

        public UnityEvent OnTypewriterStarted => _onTypewriterStarted;
        public UnityEvent OnTextShown => _onTextShown;
        public UnityEvent OnTextDisappeared => _onTextDisappeared;
        public TextStudioCharacterVisibleEvent OnCharacterVisible => _onCharacterVisible;
        public TextStudioTypewriterWaitEvent OnWaitStarted => _onWaitStarted;
        public TextStudioTypewriterWaitEvent OnWaitFinished => _onWaitFinished;
        public TextStudioMessageEvent OnMessage => _onMessage;

        void OnEnable() => RefreshTarget();
        void OnDisable() => UnsubscribeTarget();
        void OnDestroy() => UnsubscribeTarget();
        void OnTransformParentChanged() => RefreshTarget();
        void OnValidate() => RefreshTarget();

        // Ancestor reparenting and components added above us do not send this
        // component OnTransformParentChanged. Keep the nearest binding current.
        void Update() => RefreshTarget();

        void RefreshTarget()
        {
            if (isActiveAndEnabled) SubscribeTarget(ResolveTarget());
            else UnsubscribeTarget();
        }

        /// <summary>Immediately dispatches a message route and payload through the relay event.</summary>
        public void RouteMessageNow(string routeId, string payload)
        {
            _onMessage.Invoke(routeId ?? string.Empty, payload ?? string.Empty);
        }

        TextStudioText ResolveTarget()
        {
            return GetComponentInParent<TextStudioText>(true);
        }

        bool ShouldMaintainSubscription() => isActiveAndEnabled;

        void SubscribeTarget(TextStudioText target)
        {
            if (_subscribedTarget == target)
                return;

            UnsubscribeTarget();
            if (target == null)
                return;

            target.OnTypewriterStarted += HandleTypewriterStarted;
            target.OnTextUnitRevealed += HandleTextUnitRevealed;
            target.OnMarkerReached += HandleMarkerReached;
            target.OnTypewriterWaitStarted += HandleWaitStarted;
            target.OnTypewriterWaitFinished += HandleWaitFinished;
            target.OnShowComplete += HandleShowComplete;
            target.OnDisappearComplete += HandleDisappearComplete;
            _subscribedTarget = target;
        }

        void UnsubscribeTarget()
        {
            if (_subscribedTarget == null)
                return;

            _subscribedTarget.OnTypewriterStarted -= HandleTypewriterStarted;
            _subscribedTarget.OnTextUnitRevealed -= HandleTextUnitRevealed;
            _subscribedTarget.OnMarkerReached -= HandleMarkerReached;
            _subscribedTarget.OnTypewriterWaitStarted -= HandleWaitStarted;
            _subscribedTarget.OnTypewriterWaitFinished -= HandleWaitFinished;
            _subscribedTarget.OnShowComplete -= HandleShowComplete;
            _subscribedTarget.OnDisappearComplete -= HandleDisappearComplete;
            _subscribedTarget = null;
        }

        void HandleTypewriterStarted()
        {
            if (!ShouldMaintainSubscription())
                return;

            _onTypewriterStarted.Invoke();
        }

        void HandleTextUnitRevealed(int index, string textUnit)
        {
            if (!ShouldMaintainSubscription())
                return;

            _onCharacterVisible.Invoke(index, textUnit ?? string.Empty);
        }

        void HandleMarkerReached(string markerId, string[] parameters)
        {
            if (!ShouldMaintainSubscription())
                return;

            _onMessage.Invoke(
                TextStudioMarkerEventRouter.GetRouteId(markerId, parameters),
                TextStudioMarkerEventRouter.FormatPayload(markerId, parameters, TextStudioMarkerPayloadMode.JoinedParameters));
        }

        void HandleWaitStarted(TextStudioTypewriterWaitInfo info)
        {
            if (!ShouldMaintainSubscription())
                return;

            _onWaitStarted.Invoke(
                info.GlyphIndex,
                FormatCharacter(info.Character),
                info.Kind.ToString(),
                info.Duration);
        }

        void HandleWaitFinished(TextStudioTypewriterWaitInfo info)
        {
            if (!ShouldMaintainSubscription())
                return;

            _onWaitFinished.Invoke(
                info.GlyphIndex,
                FormatCharacter(info.Character),
                info.Kind.ToString(),
                info.Duration);
        }

        void HandleShowComplete()
        {
            if (!ShouldMaintainSubscription())
                return;

            _onTextShown.Invoke();
        }

        void HandleDisappearComplete()
        {
            if (!ShouldMaintainSubscription())
                return;

            _onTextDisappeared.Invoke();
        }

        static string FormatCharacter(char character)
        {
            return character == '\0' ? string.Empty : character.ToString();
        }
    }
}
