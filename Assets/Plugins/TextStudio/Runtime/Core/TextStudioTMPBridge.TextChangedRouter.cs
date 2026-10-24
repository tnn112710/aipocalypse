using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine.Events;

namespace TextStudio
{
    internal sealed partial class TextStudioTMPBridge
    {
        TMP_Text _textChangedKey;
        TextStudioTMPBridge _nextTextChangedBridge;
        UnityAction _layoutDirtyCallback;

        // Registers this bridge for regenerations and layout invalidations of its current TMP text while subscribed.
        void SyncTextChangedRegistration()
        {
            TMP_Text key = _subscribedToChanged ? _tmpText : null;
            if (ReferenceEquals(key, _textChangedKey))
                return;
            if ((object)_textChangedKey != null)
            {
                TextChangedRouter.Unregister(_textChangedKey, this);
                _textChangedKey.UnregisterDirtyLayoutCallback(_layoutDirtyCallback);
            }
            _textChangedKey = key;
            if ((object)key != null)
            {
                TextChangedRouter.Register(key, this);
                key.RegisterDirtyLayoutCallback(_layoutDirtyCallback ??= HandleTMPLayoutCallback);
            }
        }

        // A write of generated text marks TextMeshProUGUI's layout dirty too. It is no external change for the steady lane, which
        // polls TMP's text and replays the write's confirmation (IsSteadyTextCurrent, ConfirmSteadyText), so it only hands a
        // parked hub back to its sleep check.
        void HandleTMPLayoutCallback()
        {
            if (_isWritingGeneratedText)
                _hub.UnparkIfParked();
            else
                HandleTMPLayoutDirty();
        }

        // TextMeshProUGUI marks its layout dirty on every text, SetText, character array and font write while it is active,
        // also while canvases rebuild graphics, which skip its vertex callback. TextMeshPro and subclasses that may override
        // that path are compared by the scheduler instead while their hub is parked.
        internal bool ReportsTMPWrites =>
            (object)_textChangedKey != null && _textChangedKey.GetType() == typeof(TextMeshProUGUI);

        // The TextMeshProUGUI whose writes reach the hub through that callback, or null.
        internal TextMeshProUGUI WriteReportingText => ReportsTMPWrites ? (TextMeshProUGUI)_textChangedKey : null;

        void HandleTMPLayoutDirty() => _hub.Unpark();

        /// <summary>
        /// TMP raises one global text-changed event for every regeneration of any text. A bridge per hub subscribing to it
        /// made each regeneration call every enabled bridge, quadratic in scenes with many changing texts; one forwarder
        /// now hands the event to the bridges of the regenerated text.
        /// </summary>
#if UNITY_6000_7_OR_NEWER
        // Bridge leases also serve Edit Mode; the TMP event forwarder is released on code unload.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        internal static partial class TextChangedRouter
        {
            static readonly Dictionary<TMP_Text, TextStudioTMPBridge> s_bridges =
                new Dictionary<TMP_Text, TextStudioTMPBridge>(TextReferenceComparer.Instance);
            static bool s_subscribed;

            internal static int RegisteredTextCount => s_bridges.Count;

            internal static void Register(TMP_Text text, TextStudioTMPBridge bridge)
            {
                s_bridges.TryGetValue(text, out TextStudioTMPBridge head);
                bridge._nextTextChangedBridge = head;
                s_bridges[text] = bridge;
                if (s_subscribed)
                    return;
                TMPro_EventManager.TEXT_CHANGED_EVENT.Add(Forward);
                s_subscribed = true;
            }

            internal static void Unregister(TMP_Text text, TextStudioTMPBridge bridge)
            {
                if (!s_bridges.TryGetValue(text, out TextStudioTMPBridge head))
                    return;
                if (ReferenceEquals(head, bridge))
                {
                    if (bridge._nextTextChangedBridge != null)
                        s_bridges[text] = bridge._nextTextChangedBridge;
                    else
                        s_bridges.Remove(text);
                }
                else
                {
                    for (TextStudioTMPBridge current = head; current != null; current = current._nextTextChangedBridge)
                    {
                        if (!ReferenceEquals(current._nextTextChangedBridge, bridge))
                            continue;
                        current._nextTextChangedBridge = bridge._nextTextChangedBridge;
                        break;
                    }
                }
                bridge._nextTextChangedBridge = null;
            }

#if UNITY_6000_7_OR_NEWER
            [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
            static void ReleaseSubscriptions()
            {
                TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(Forward);
                s_subscribed = false;
                s_bridges.Clear();
            }
#endif

            static void Forward(UnityEngine.Object changed)
            {
                if (!(changed is TMP_Text text) || !s_bridges.TryGetValue(text, out TextStudioTMPBridge bridge))
                    return;
                // A handler can unregister bridges, so each link is read before its bridge runs.
                while (bridge != null)
                {
                    TextStudioTMPBridge next = bridge._nextTextChangedBridge;
                    bridge.HandleTMPTextChanged(changed);
                    bridge = next;
                }
            }

#if UNITY_6000_7_OR_NEWER
            // The shared comparer or empty metadata has no Play Mode state.
            [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
            sealed class TextReferenceComparer : IEqualityComparer<TMP_Text>
            {
                internal static readonly TextReferenceComparer Instance = new TextReferenceComparer();

                public bool Equals(TMP_Text x, TMP_Text y) => ReferenceEquals(x, y);

                public int GetHashCode(TMP_Text value) => RuntimeHelpers.GetHashCode(value);
            }
        }
    }
}
