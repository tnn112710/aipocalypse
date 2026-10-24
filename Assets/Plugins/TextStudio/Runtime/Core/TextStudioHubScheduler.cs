using System;
using System.Collections.Generic;
using ImpossibleRobert.Common.Timing;
using UnityEngine;

namespace TextStudio
{
    // Ticks every playing hub from one LateUpdate. A Unity callback per hub cost more than a settled hub's whole sleep
    // check, so populations of idle labels paid mostly for the dispatch. Hubs tick in the order they were enabled.
    // A parked hub (TextStudioText.TryPark) is not called at all; its slot advances the clocks its sleep check would and
    // compares the inputs that check polls for styled text (ParkedAppearance).
#if UNITY_6000_7_OR_NEWER
    // The driver, quitting subscription and parked clocks require explicit release before session state is cleared.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioHubScheduler
    {
        // The presentation and placement clocks of a parked hub with the timing it parked with. A hub on a TMP type that
        // does not report text and font writes keeps its bridge here to compare them each frame.
        struct ParkedClock
        {
            internal TextStudioTMPBridge Probe;
            internal float PresentationTime;
            internal float PlacementTime;
            internal float MaxDeltaTime;
            internal float TimeScale;
            internal bool Parked;
            internal bool Unscaled;
            internal bool AnimationPaused;
            internal bool WatchesAppearance;
        }

        /// <summary>
        /// The polled inputs of a parked styled hub's appearance checks (TextStudioText.IsPolledAppearanceCurrent): its style
        /// dependency revision and, for Canvas text with a batching payload, the text and root canvas matrices that
        /// TextStudioTMPFrameApplier.HaveCanvasPayloadInputs compares. Every other input of those checks unparks the hub when
        /// it changes, so while these match, the checks would pass without side effects.
        /// </summary>
        internal struct ParkedAppearance
        {
            internal bool Active;
            internal bool Inline;
            internal TextStudio2DStyleAsset Style;
            internal TextStudio2DStyleSettings InlineSettings;
            internal ulong Revision;
            internal Transform PoseTransform;
            internal Canvas PoseCanvas;
            internal Matrix4x4 PoseMatrix;
            internal Matrix4x4 PoseRootMatrix;

            // Reads what the hub's checks read, through the same per-frame style and root canvas results; frame is the
            // player frame, or -1 in Edit Mode, which recomputes both.
            internal bool IsCurrent(int frame)
            {
                if (Inline)
                {
                    // An emptied stack, such as one whose only base style was destroyed, selects the asset path.
                    if (InlineSettings.IsEmpty || TextStudio2DStyleCompiler.ComputeDependencyRevision(in InlineSettings, frame) != Revision)
                        return false;
                }
                else if (TextStudio2DStyleCompiler.ComputeDependencyRevision(Style, frame) != Revision) return false;
                // Graphic changes its canvas only in hierarchy messages, which unpark the hub on the same object, in OnEnable,
                // which TMP reports through its layout callback, or after that canvas is destroyed, which this check sees.
                return PoseTransform is null ||
                       PoseCanvas != null &&
                       PoseMatrix.Equals(PoseTransform.localToWorldMatrix) &&
                       PoseRootMatrix.Equals(TextStudioCanvasRootSnapshot.Of(PoseCanvas, frame).LocalToWorld);
            }
        }

        static readonly List<TextStudioText> s_Hubs = new List<TextStudioText>();
        // Indexed by slot like s_Hubs and at least as long; entries past the last slot stay cleared.
        static ParkedClock[] s_Clocks = Array.Empty<ParkedClock>();
        // Parallel to s_Clocks, so parked hubs without appearance never read these larger entries.
        static ParkedAppearance[] s_Appearances = Array.Empty<ParkedAppearance>();
        static int s_ParkedCount;
        static TextStudioHubSchedulerDriver s_Driver;
        static bool s_Ticking;
        static bool s_HasRemovals;
        static bool s_Quitting;

        internal static int RegisteredCountForDebug => s_Hubs.Count;
        internal static int ParkedCountForDebug => s_ParkedCount;

        /// <summary>Counts idle-sleeping hubs and names what keeps the first awake hub from sleeping.</summary>
        internal static int CountIdleSleepingForDebug(out int registered, out string firstBlocker)
        {
            registered = 0;
            firstBlocker = null;
            int sleeping = 0;
            for (int i = 0; i < s_Hubs.Count; i++)
            {
                TextStudioText hub = s_Hubs[i];
                if (hub is null)
                    continue;
                registered++;
                if (hub.IsIdleSleepingForDebug)
                    sleeping++;
                else if (firstBlocker == null)
                    firstBlocker = hub.IdleSleepBlockerForDebug;
            }

            return sleeping;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlayMode()
        {
            ReleaseRuntimeState();
            s_Quitting = false;
            Application.quitting += OnQuitting;
        }

#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ReleaseRuntimeState()
        {
            Application.quitting -= OnQuitting;
            s_Quitting = true;
            foreach (TextStudioText hub in s_Hubs)
                if (hub != null)
                {
                    hub.Unpark();
                    hub.SchedulerSlot = -1;
                }
            s_Hubs.Clear();
            Array.Clear(s_Clocks, 0, s_Clocks.Length);
            Array.Clear(s_Appearances, 0, s_Appearances.Length);
            s_ParkedCount = 0;
            s_Ticking = false;
            s_HasRemovals = false;
            TextStudioHubSchedulerDriver driver = s_Driver;
            s_Driver = null;
            if (driver != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(driver.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(driver.gameObject);
            }
        }

        // The Editor raises quitting when Play Mode exits, before the scene teardown destroys the driver.
        static void OnQuitting()
        {
            s_Quitting = true;
        }

        internal static void Register(TextStudioText hub)
        {
            // Edit Mode hubs tick through the editor, and the driver's DontDestroyOnLoad exists only in Play Mode.
            if (!Application.isPlaying)
                return;

            int slot = hub.SchedulerSlot;
            if (slot >= 0 && slot < s_Hubs.Count && ReferenceEquals(s_Hubs[slot], hub))
                return;

            hub.SchedulerSlot = s_Hubs.Count;
            s_Hubs.Add(hub);
            if (s_Clocks.Length < s_Hubs.Count)
            {
                int capacity = Math.Max(16, s_Clocks.Length * 2);
                Array.Resize(ref s_Clocks, capacity);
                Array.Resize(ref s_Appearances, capacity);
            }
            if (s_Driver == null)
                s_Driver = TextStudioHubSchedulerDriver.Create();
        }

        internal static void Unregister(TextStudioText hub)
        {
            // A parked hub takes its clocks back while its slot is still valid.
            hub.Unpark();
            int slot = hub.SchedulerSlot;
            hub.SchedulerSlot = -1;
            if (slot < 0 || slot >= s_Hubs.Count || !ReferenceEquals(s_Hubs[slot], hub))
                return;

            s_Hubs[slot] = null;
            s_HasRemovals = true;
            if (!s_Ticking)
                Compact();
        }

        internal static void Tick()
        {
            s_Ticking = true;
            float deltaTime = Time.deltaTime;
            float unscaledDeltaTime = Time.unscaledDeltaTime;
            int frame = Time.frameCount;
            ITextStudioHubTickObserver observer = TextStudioPerformanceDiagnostics.HubTickObserver;
            TextStudioPerformanceDiagnostics.MeasurementScope measurement =
                TextStudioPerformanceDiagnostics.Measure(TextStudioPerformanceStage.Scheduler);
            try
            {
                // Hubs enabled by a tick join the next frame, as a newly enabled component would.
                int count = s_Hubs.Count;
                for (int i = 0; i < count; i++)
                {
                    TextStudioText hub = s_Hubs[i];
                    if (hub is null)
                        continue;
                    try
                    {
                        // A parked hub costs its clock entry; nothing reads the hub until a change unparks it.
                        if (s_Clocks[i].Parked && AdvanceParkedClock(i, frame, deltaTime, unscaledDeltaTime))
                            continue;
                        if (observer == null)
                            hub.ScheduledLateUpdate(deltaTime, unscaledDeltaTime, frame);
                        else
                            TickObserved(hub, observer, deltaTime, unscaledDeltaTime, frame);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, hub);
                    }
                }
            }
            finally
            {
                measurement.Dispose();
                s_Ticking = false;
                if (s_HasRemovals)
                    Compact();
            }
        }

        static void TickObserved(TextStudioText hub, ITextStudioHubTickObserver observer, float deltaTime, float unscaledDeltaTime, int frame)
        {
            bool measureAllocations = TextStudioPerformanceDiagnostics.HubAllocationMeasurementsAvailable;
            long allocatedBefore = measureAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0L;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                hub.ScheduledLateUpdate(deltaTime, unscaledDeltaTime, frame);
            }
            finally
            {
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
                long allocated = measureAllocations ? GC.GetAllocatedBytesForCurrentThread() - allocatedBefore : 0L;
                observer.OnHubTicked(hub, elapsed, allocated);
            }
        }

        // Adds the increment ContinueIdleSleep adds while the preview passes runtime time through, so an unparked hub holds
        // the clocks it would have had. A probed hub whose TMP text or font changed, or a styled hub whose polled appearance
        // inputs changed, unparks and runs its sleep check in this slot instead, in the same order and frame as before.
        static bool AdvanceParkedClock(int slot, int frame, float deltaTime, float unscaledDeltaTime)
        {
            ref ParkedClock clock = ref s_Clocks[slot];
            if (clock.Probe != null && !clock.Probe.AreObservedTextAndFontCurrent() ||
                clock.WatchesAppearance && !s_Appearances[slot].IsCurrent(frame))
            {
                s_Hubs[slot].Unpark();
                return false;
            }

            float frameDeltaTime = clock.Unscaled ? unscaledDeltaTime : deltaTime;
            float hostDeltaTime = clock.AnimationPaused
                ? 0f
                : Mathf.Max(0f, HostTime.ClampDelta(frameDeltaTime, clock.MaxDeltaTime) * clock.TimeScale);
            clock.PresentationTime += hostDeltaTime;
            clock.PlacementTime += hostDeltaTime;
            return true;
        }

        internal static void Park(
            int slot,
            float presentationTime,
            float placementTime,
            bool unscaled,
            float maxDeltaTime,
            float timeScale,
            bool animationPaused,
            TextStudioTMPBridge probe,
            in ParkedAppearance appearance)
        {
            s_Clocks[slot] = new ParkedClock
            {
                Probe = probe,
                PresentationTime = presentationTime,
                PlacementTime = placementTime,
                MaxDeltaTime = maxDeltaTime,
                TimeScale = timeScale,
                Parked = true,
                Unscaled = unscaled,
                AnimationPaused = animationPaused,
                WatchesAppearance = appearance.Active
            };
            if (appearance.Active)
                s_Appearances[slot] = appearance;
            s_ParkedCount++;
        }

        internal static void Unpark(int slot, ref float presentationTime, ref float placementTime)
        {
            ref ParkedClock clock = ref s_Clocks[slot];
            if (!clock.Parked)
                return;
            presentationTime = clock.PresentationTime;
            placementTime = clock.PlacementTime;
            // Released entries must not keep a style, canvas or transform reachable.
            if (clock.WatchesAppearance)
                s_Appearances[slot] = default;
            clock = default;
            s_ParkedCount--;
        }

        internal static float ParkedPresentationTime(int slot) => s_Clocks[slot].PresentationTime;
        internal static float ParkedPlacementTime(int slot) => s_Clocks[slot].PlacementTime;

        // Effect content and behavior profiles are inputs of every sleep check. Their edits are rare, so they unpark every
        // parked hub.
        internal static void UnparkAll()
        {
            if (s_ParkedCount == 0)
                return;
            for (int i = 0; i < s_Hubs.Count; i++)
            {
                if (s_Clocks[i].Parked)
                    s_Hubs[i].Unpark();
            }
        }

        internal static void ReleaseDriver(TextStudioHubSchedulerDriver driver)
        {
            if (!ReferenceEquals(s_Driver, driver))
                return;

            s_Driver = null;
            // Scene code that destroys every object can take the hidden driver while hubs stay enabled; they keep ticking.
            if (!s_Quitting && HasRegisteredHub())
                s_Driver = TextStudioHubSchedulerDriver.Create();
        }

        static bool HasRegisteredHub()
        {
            for (int i = 0; i < s_Hubs.Count; i++)
            {
                if (!(s_Hubs[i] is null))
                    return true;
            }
            return false;
        }

        static void Compact()
        {
            int write = 0;
            for (int read = 0; read < s_Hubs.Count; read++)
            {
                TextStudioText hub = s_Hubs[read];
                if (hub is null)
                    continue;
                hub.SchedulerSlot = write;
                s_Clocks[write] = s_Clocks[read];
                s_Appearances[write] = s_Appearances[read];
                s_Hubs[write++] = hub;
            }
            Array.Clear(s_Clocks, write, s_Hubs.Count - write);
            Array.Clear(s_Appearances, write, s_Hubs.Count - write);
            s_Hubs.RemoveRange(write, s_Hubs.Count - write);
            s_HasRemovals = false;
        }
    }
}
