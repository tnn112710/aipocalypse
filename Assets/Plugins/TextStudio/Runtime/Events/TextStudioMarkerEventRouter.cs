using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Events;

namespace TextStudio
{
    public enum TextStudioMarkerPayloadMode
    {
        [InspectorName("All Arguments")] JoinedParameters,
        [InspectorName("Event Name")] RouteId,
        [InspectorName("First Argument")] FirstParameter
    }

    /// <summary>Serializable marker route, payload formatting policy, enabled state, and scene callback.</summary>
    [Serializable]
    public sealed class TextStudioMarkerBinding
    {
        [SerializeField, Tooltip("Event name to match, ignoring case. For <event=door,open>, enter door. Add separate entries for names that need different callbacks.")] string _routeId;
        [SerializeField, Tooltip("String sent to the callback: all comma-joined arguments after the event name, the name itself, or only the first argument. With <event=door,open>, arguments contain open.")] TextStudioMarkerPayloadMode _payloadMode = TextStudioMarkerPayloadMode.JoinedParameters;
        [SerializeField, Tooltip("Invoked only for this event name. Use Dynamic string to receive its payload; static string uses the value entered in the listener. Multiple listeners may share one event.")] UnityEvent<string> _onMarker = new UnityEvent<string>();

        public string RouteId
        {
            get => _routeId;
            set => _routeId = value;
        }

        public TextStudioMarkerPayloadMode PayloadMode
        {
            get => _payloadMode;
            set => _payloadMode = value;
        }

        public UnityEvent<string> OnMarker => _onMarker;

        /// <summary>Reports whether an authored marker resolves to this binding's route.</summary>
        public bool Matches(string markerId, string[] parameters)
        {
            return !string.IsNullOrEmpty(_routeId) &&
                   string.Equals(_routeId, TextStudioMarkerEventRouter.GetRouteId(markerId, parameters), StringComparison.OrdinalIgnoreCase);
        }

        internal void Invoke(string markerId, string[] parameters)
        {
            _onMarker.Invoke(TextStudioMarkerEventRouter.FormatPayload(markerId, parameters, _payloadMode));
        }
    }

    /// <summary>
    /// Inspector-friendly marker router for gameplay hooks such as dialogue commands,
    /// audio cues, camera beats, and quest events.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Text Studio/Events/Marker Event Router")]
    [HelpURL("https://www.wetzold.com/tools/text-studio")]
    /// <summary>Routes authored marker identifiers and payloads to serialized scene events.</summary>
    public sealed class TextStudioMarkerEventRouter : MonoBehaviour
    {
        [SerializeField] TextStudioMarkerPayloadMode _anyMarkerPayloadMode = TextStudioMarkerPayloadMode.JoinedParameters;
        [SerializeField] UnityEvent<string> _onAnyMarker = new UnityEvent<string>();
        [SerializeField] List<TextStudioMarkerBinding> _bindings = new List<TextStudioMarkerBinding>();

        TextStudioText _subscribedTarget;

        public TextStudioText Target => ResolveTarget();

        public UnityEvent<string> OnAnyMarker => _onAnyMarker;
        public IReadOnlyList<TextStudioMarkerBinding> Bindings => _bindings;

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

        /// <summary>Logs marker arguments with the Text Studio prefix, using a guidance message when arguments are empty.</summary>
        public void DebugLog(string text)
        {
            string message = string.IsNullOrEmpty(text)
                ? "(no event arguments; choose Send: Event Name to log the name)"
                : text;
            UnityEngine.Debug.Log("[Text Studio] Marker: " + message, this);
        }

        /// <summary>Adds a route binding and optionally subscribes a serialized string callback.</summary>
        public TextStudioMarkerBinding AddBinding(string routeId, UnityAction<string> callback)
        {
            TextStudioMarkerBinding binding = new TextStudioMarkerBinding
            {
                RouteId = routeId
            };
            if (callback != null)
                binding.OnMarker.AddListener(callback);

            _bindings.Add(binding);
            return binding;
        }

        /// <summary>Immediately routes one marker to every matching enabled binding.</summary>
        public void RouteNow(string markerId, string[] parameters)
        {
            HandleMarkerReached(markerId, parameters);
        }

        /// <summary>Formats marker parameters as the first value, joined values, or an empty payload.</summary>
        public static string FormatPayload(string markerId, string[] parameters, TextStudioMarkerPayloadMode mode)
        {
            int payloadStartIndex = GetPayloadStartIndex(markerId, parameters);
            switch (mode)
            {
                case TextStudioMarkerPayloadMode.RouteId:
                    return GetRouteId(markerId, parameters);
                case TextStudioMarkerPayloadMode.FirstParameter:
                    return parameters != null && parameters.Length > payloadStartIndex ? parameters[payloadStartIndex] ?? string.Empty : string.Empty;
                case TextStudioMarkerPayloadMode.JoinedParameters:
                default:
                    return parameters != null && parameters.Length > payloadStartIndex
                        ? string.Join(",", parameters, payloadStartIndex, parameters.Length - payloadStartIndex)
                        : string.Empty;
            }
        }

        /// <summary>Resolves a marker's canonical route identifier.</summary>
        public static string GetRouteId(string markerId, string[] parameters)
        {
            if (IsNamedEventMarker(markerId, parameters))
                return parameters[0] ?? string.Empty;

            return markerId ?? string.Empty;
        }

        static int GetPayloadStartIndex(string markerId, string[] parameters)
        {
            return IsNamedEventMarker(markerId, parameters) ? 1 : 0;
        }

        static bool IsNamedEventMarker(string markerId, string[] parameters)
        {
            return string.Equals(markerId, TextStudioBuiltInActionIds.Event, StringComparison.OrdinalIgnoreCase) &&
                   parameters != null &&
                   parameters.Length > 0 &&
                   !string.IsNullOrEmpty(parameters[0]);
        }

        TextStudioText ResolveTarget()
        {
            return GetComponentInParent<TextStudioText>(true);
        }

        void SubscribeTarget(TextStudioText target)
        {
            if (_subscribedTarget == target)
                return;

            UnsubscribeTarget();

            if (target == null)
                return;

            target.OnMarkerReached += HandleMarkerReached;
            _subscribedTarget = target;
        }

        void UnsubscribeTarget()
        {
            if (_subscribedTarget == null)
                return;

            _subscribedTarget.OnMarkerReached -= HandleMarkerReached;
            _subscribedTarget = null;
        }

        void HandleMarkerReached(string markerId, string[] parameters)
        {
            _onAnyMarker.Invoke(FormatPayload(markerId, parameters, _anyMarkerPayloadMode));

            for (int i = 0; i < _bindings.Count; i++)
            {
                TextStudioMarkerBinding binding = _bindings[i];
                if (binding != null && binding.Matches(markerId, parameters))
                    binding.Invoke(markerId, parameters);
            }
        }
    }
}
