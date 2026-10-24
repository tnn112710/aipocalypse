using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Flags the live or retained anchors, geometry, targets, physics, and output capabilities a Reaction action requires.</summary>
    [Flags]
    public enum TextStudioReactionActionRequirements
    {
        None = 0,
        LiveAnchor = 1,
        RetainedGeometry = 2,
        NamedTarget = 4,
        PhysicsWorld = 8,
        RetainedAnchor = 16,
        WorldObjectOutput = 32,
        UIParticles = 64
    }

    /// <summary>Flags the completion, arrival, impact, break, and return events a Reaction action can emit for dependent responses.</summary>
    [Flags]
    public enum TextStudioReactionActionEvents
    {
        None = 0,
        Completed = 1,
        Arrived = 2,
        Impacted = 4,
        Broke = 8,
        Returned = 16
    }

    /// <summary>Declares whether a Reaction action supports only Play Mode, direct deterministic seeking, or bounded resimulation during authoring previews.</summary>
    public enum TextStudioReactionPreviewCapability
    {
        PlayModeOnly,
        DeterministicSeek,
        BoundedResimulation
    }

    /// <summary>Events and event time produced by one action sample, including whether its target was lost.</summary>
    public readonly struct TextStudioReactionActionSample
    {
        public readonly TextStudioReactionActionEvents Events;
        public readonly double EventTime;
        public readonly bool TargetLost;

        /// <summary>Creates an action sample with emitted events and their host event time, without reporting target loss.</summary>
        public TextStudioReactionActionSample(TextStudioReactionActionEvents events, double eventTime)
            : this(events, eventTime, false) { }

        TextStudioReactionActionSample(TextStudioReactionActionEvents events,
            double eventTime, bool targetLost)
        {
            Events = events;
            EventTime = eventTime;
            TargetLost = targetLost;
        }

        /// <summary>Creates an event-free action sample indicating that its target was lost at the supplied time.</summary>
        public static TextStudioReactionActionSample LostTarget(double eventTime) =>
            new TextStudioReactionActionSample(TextStudioReactionActionEvents.None,
                eventTime, true);
    }

    /// <summary>Scene-local services supplied to a prepared reaction action.</summary>
    public interface ITextStudioReactionActionHost
    {
        TextStudioReactionActionRequirements Capabilities { get; }
        TextStudioMotionPolicy MotionPolicy { get; }
        /// <summary>Attempts to resolve the current visual anchor for the semantic unit in a reaction context.</summary>
        bool TryGetReactionAnchor(in TextStudioReactionContext context, out TextStudioReactionAnchor anchor);
        /// <summary>Resolves a named target binding to the slot used for subsequent target lookup.</summary>
        int ResolveReactionTargetSlot(string key);
        /// <summary>Attempts to resolve a configured named target to an anchor in the host's output space using its slot.</summary>
        bool TryGetReactionTarget(int slot, out TextStudioReactionAnchor anchor);
        /// <summary>Attempts to resolve a configured named target to an anchor in the host's output space using its binding key.</summary>
        bool TryGetReactionTarget(string key, out TextStudioReactionAnchor anchor);
        /// <summary>Attempts to acquire retained output for the requested reaction context, intent, and resource class.</summary>
        bool TryCaptureReactionOutput(in TextStudioReactionCaptureRequest request,
            out TextStudioReactionLease lease);
    }

    /// <summary>Immutable result of compiling a custom reaction action.</summary>
    public interface ITextStudioReactionCommand
    {
        TextStudioReactionActionRequirements Requirements { get; }
        TextStudioReactionActionEvents SupportedEvents { get; }
        TextStudioReactionPreviewCapability PreviewCapability { get; }
        TextStudioReactionCaptureIntent CaptureIntent { get; }
        TextStudioReactionResourceClass CaptureResources { get; }
        /// <summary>Creates the mutable execution state for this immutable compiled action command.</summary>
        ITextStudioReactionActionState CreateState();
    }

    /// <summary>Reports a valid event unit whose visual layout has not been published yet.</summary>
    public interface ITextStudioReactionPendingAnchorHost
    {
        /// <summary>Reports whether a valid event unit is waiting for its visual layout to be published.</summary>
        bool IsReactionAnchorPending(in TextStudioReactionContext context);
    }

    /// <summary>Resolves the current semantic unit for host-independent glyph audio cues.</summary>
    public interface ITextStudioReactionUnitTextSource
    {
        /// <summary>Attempts to resolve the current semantic unit's primary character for a reaction context.</summary>
        bool TryGetReactionUnitCharacter(in TextStudioReactionContext context, out char character);
    }

    /// <summary>Optional bridge to a target-scoped physics owner, without placing simulation in Text Studio.</summary>
    public interface ITextStudioReactionBodyActionHost
    {
        /// <summary>Reports whether the host can release the semantic body identified by the reaction context.</summary>
        bool CanReleaseReactionBody(in TextStudioReactionContext context);
        /// <summary>Attempts to release the context's semantic body with world-space launch velocities and return its event session.</summary>
        bool TryReleaseReactionBody(in TextStudioReactionContext context,
            Vector3 velocity, Vector3 angularVelocity,
            out TextStudioReactionBodyEventSession session);
    }

    /// <summary>Non-consuming event view for one released semantic body.</summary>
    public interface ITextStudioReactionBodyEventSource
    {
        /// <summary>Reads the events for a released body's session without consuming them.</summary>
        bool TryGetReactionBodyEvents(int bodyIndex, ulong sessionId,
            out TextStudioReactionActionEvents events);
        /// <summary>Ends observation of the specified released-body session.</summary>
        void EndReactionBodySession(int bodyIndex, ulong sessionId);
    }

    /// <summary>Session token for observing events from one released semantic body without consuming them.</summary>
    public readonly struct TextStudioReactionBodyEventSession
    {
        readonly ITextStudioReactionBodyEventSource _source;
        readonly int _bodyIndex;
        readonly ulong _sessionId;

        /// <summary>Creates a session token for one body index and session identifier supplied by an event source.</summary>
        public TextStudioReactionBodyEventSession(ITextStudioReactionBodyEventSource source,
            int bodyIndex, ulong sessionId)
        {
            _source = source;
            _bodyIndex = bodyIndex;
            _sessionId = sessionId;
        }

        public bool IsValid => _source != null && _sessionId != 0ul;

        /// <summary>Reads this session's non-consuming body events, or returns false when the session is invalid.</summary>
        public bool TryGetEvents(out TextStudioReactionActionEvents events)
        {
            if (IsValid)
                return _source.TryGetReactionBodyEvents(_bodyIndex, _sessionId, out events);
            events = TextStudioReactionActionEvents.None;
            return false;
        }

        /// <summary>Ends this body-event session through its source when the session is valid.</summary>
        public void Release()
        {
            if (IsValid) _source.EndReactionBodySession(_bodyIndex, _sessionId);
        }
    }

    /// <summary>Optional preflight for expected target loss before action state starts.</summary>
    public interface ITextStudioReactionStartPolicy
    {
        /// <summary>Checks whether an action can start for the context and host before its execution state is acquired.</summary>
        bool CanStart(in TextStudioReactionContext context,
            ITextStudioReactionActionHost host);
    }

    /// <summary>Optional preflight after retained capture, before acquiring action state.</summary>
    public interface ITextStudioReactionLeaseStartPolicy
    {
        /// <summary>Checks whether an action can start with the retained capture before its execution state is acquired.</summary>
        bool CanStart(in TextStudioReactionContext context,
            ITextStudioReactionActionHost host, in TextStudioReactionLease lease);
    }

    /// <summary>Optional host input lookup for reaction-layer Boolean gates.</summary>
    public interface ITextStudioReactionBooleanInputProvider
    {
        /// <summary>Attempts to read a named Boolean input used to gate reaction layers.</summary>
        bool TryGetReactionBooleanInput(string key, out bool value);
    }

    /// <summary>Optional anchor selection carried into pre-commit outgoing capture.</summary>
    public interface ITextStudioReactionAnchorSelectionCommand
    {
        string AnchorKey { get; }
    }

    /// <summary>Optional cap shared by layers using the same action asset. Overflow replaces the oldest response.</summary>
    public interface ITextStudioReactionConcurrencyCommand
    {
        int MaxConcurrent { get; }
    }

    /// <summary>Optional cap on outgoing semantic units accepted for one text-change capture.</summary>
    public interface ITextStudioReactionChangeLimitCommand
    {
        int MaxUnitsPerChange { get; }
    }

    /// <summary>Optional current-frame anchor snapshot taken after presentation, before any action delay.</summary>
    public interface ITextStudioReactionTriggerAnchorCommand
    {
        bool CaptureTriggerAnchor { get; }
    }

    /// <summary>Reusable host-local state. CreateState runs during preparation or pool growth, never in a warmed sample.</summary>
    public interface ITextStudioReactionActionState
    {
        void Start(in TextStudioReactionContext context, ITextStudioReactionActionHost host,
            in TextStudioReactionLease lease);
        /// <summary>Samples the response at the supplied progress and host time, returning action events or target loss.</summary>
        TextStudioReactionActionSample Sample(in TextStudioReactionContext context, float progress, double hostTime);
        /// <summary>Handles successful completion after the response reaches its end.</summary>
        void Complete(in TextStudioReactionContext context);
        /// <summary>Cancels an action for the supplied reason and performs its cancellation cleanup.</summary>
        void Cancel(in TextStudioReactionContext context, TextStudioReactionCancelReason reason);
        /// <summary>Releases resources owned by this action state when the state is no longer used.</summary>
        void Dispose();
    }

    /// <summary>Optional Play Mode preparation for actions that own pooled Unity objects.</summary>
    public interface ITextStudioReactionPrewarmableCommand
    {
        int PrewarmCount { get; }
    }

    /// <summary>Optional preparation hook for allocating reusable action state before responses execute.</summary>
    public interface ITextStudioReactionPrewarmableState
    {
        /// <summary>Prepares reusable action resources for the supplied host before response execution.</summary>
        void Prewarm(ITextStudioReactionActionHost host);
    }

    /// <summary>Asset-only authoring definition; scene references belong to host bindings.</summary>
#if UNITY_6000_7_OR_NEWER
    // Content revisions invalidate compiled assets in Edit Mode and must survive Play transitions.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public abstract class TextStudioReactionActionAsset : ScriptableObject
    {
        static uint s_contentRevision = 1;

        public static uint ContentRevision => s_contentRevision;

        /// <summary>Compile and validate without touching scene objects, pools, or live output.</summary>
        public abstract bool TryCompile(out ITextStudioReactionCommand command, out string error);

        /// <summary>Call after programmatic changes that alter the compiled command.</summary>
        public void NotifyChanged()
        {
            unchecked { s_contentRevision++; }
            if (s_contentRevision == 0) s_contentRevision = 1;
        }

        protected virtual void OnValidate() => NotifyChanged();
    }
}
