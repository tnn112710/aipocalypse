using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    // Ticks every playing hub from one LateUpdate. A Unity callback per hub cost more than a settled hub's whole sleep
    // check, so populations of idle labels paid mostly for the dispatch. Hubs tick in the order they were enabled.
#if UNITY_6000_7_OR_NEWER
    // The driver and quitting subscription require explicit release before session state is cleared.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioHubScheduler
    {
        static readonly List<TextStudioText> s_Hubs = new List<TextStudioText>();
        static TextStudioHubSchedulerDriver s_Driver;
        static bool s_Ticking;
        static bool s_HasRemovals;
        static bool s_Quitting;

        internal static int RegisteredCountForDebug => s_Hubs.Count;

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
                    hub.SchedulerSlot = -1;
            s_Hubs.Clear();
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
            if (s_Driver == null)
                s_Driver = TextStudioHubSchedulerDriver.Create();
        }

        internal static void Unregister(TextStudioText hub)
        {
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
            ITextStudioHubTickObserver observer = TextStudioPerformanceDiagnostics.HubTickObserver;
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
                        if (observer == null)
                            hub.ScheduledLateUpdate(deltaTime, unscaledDeltaTime);
                        else
                            TickObserved(hub, observer, deltaTime, unscaledDeltaTime);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, hub);
                    }
                }
            }
            finally
            {
                s_Ticking = false;
                if (s_HasRemovals)
                    Compact();
            }
        }

        static void TickObserved(TextStudioText hub, ITextStudioHubTickObserver observer, float deltaTime, float unscaledDeltaTime)
        {
            bool measureAllocations = TextStudioPerformanceDiagnostics.HubAllocationMeasurementsAvailable;
            long allocatedBefore = measureAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0L;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                hub.ScheduledLateUpdate(deltaTime, unscaledDeltaTime);
            }
            finally
            {
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
                long allocated = measureAllocations ? GC.GetAllocatedBytesForCurrentThread() - allocatedBefore : 0L;
                observer.OnHubTicked(hub, elapsed, allocated);
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
                s_Hubs[write++] = hub;
            }
            s_Hubs.RemoveRange(write, s_Hubs.Count - write);
            s_HasRemovals = false;
        }
    }
}
