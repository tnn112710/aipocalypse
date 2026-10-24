using System;
using System.Reflection;
using TMPro;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;

namespace TextStudio
{
    /// <summary>
    /// Takes TMP's per-frame scale check off parked TextMeshProUGUI labels. TMP_UpdateManager calls InternalUpdate on every
    /// enabled label from Canvas.willRenderCanvases, and for settled text that call only reads the label's lossy scale and acts
    /// once it moved past TMP's own baseline. Once enough labels are parked, a label that stays parked leaves that queue; one
    /// read-only transform job compares the world rotation and rotation-scale of every watched label in this class's
    /// willRenderCanvases callback, and a label whose pose changed runs TMP's own InternalUpdate there, so TMP applies its
    /// threshold to its own baseline in the same canvas update. Unparking returns the label to TMP's queue wherever TMP itself
    /// would have kept it.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Owns native containers and a canvas callback, which ReleaseRuntimeState releases explicitly.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioTMPScaleWatch
    {
        /// <summary>
        /// Parked labels needed before any label leaves TMP's queue. Below it the watch's fixed cost per canvas update can exceed
        /// the InternalUpdate calls it replaces on desktop.
        /// </summary>
        internal const int MinimumPopulation = 128;

        /// <summary>
        /// Canvas updates a parked label waits before the job reads it, so a hub that parks for a few frames never pays for
        /// TMP's linear removal from its update queue.
        /// </summary>
        internal const int SettleChecks = 6;

        const int BatchSize = 32;

        // Up to this many watched labels the job runs on the main thread, where it reads dense hierarchy data instead of the
        // labels' managed objects; above it, workers share the batches and the waiting main thread runs some of them.
        const int ParallelThreshold = 128;

        // World rotation-scale and rotation of a label. TMP's lossy scale is a function of both and of nothing else, so a label
        // that only moves is not checked again. Comparisons are by value: a NaN component checks the label every frame, as
        // TMP's own lossy scale comparison does.
        struct WatchedPose
        {
            public float M00, M10, M20, M01, M11, M21, M02, M12, M22;
            public float X, Y, Z, W;
            public int Valid;

            public WatchedPose(in Matrix4x4 matrix, in Quaternion rotation)
            {
                M00 = matrix.m00; M10 = matrix.m10; M20 = matrix.m20;
                M01 = matrix.m01; M11 = matrix.m11; M21 = matrix.m21;
                M02 = matrix.m02; M12 = matrix.m12; M22 = matrix.m22;
                X = rotation.x; Y = rotation.y; Z = rotation.z; W = rotation.w;
                Valid = 1;
            }

            public bool Matches(in Matrix4x4 matrix, in Quaternion rotation) =>
                Valid != 0 &&
                M00 == matrix.m00 && M10 == matrix.m10 && M20 == matrix.m20 &&
                M01 == matrix.m01 && M11 == matrix.m11 && M21 == matrix.m21 &&
                M02 == matrix.m02 && M12 == matrix.m12 && M22 == matrix.m22 &&
                X == rotation.x && Y == rotation.y && Z == rotation.z && W == rotation.w;
        }

        // Records each label's pose and flags the labels whose pose differs from the previous check.
        struct PoseJob : IJobParallelForTransform
        {
            public NativeArray<WatchedPose> Poses;
            public NativeArray<byte> Changed;

            public void Execute(int index, TransformAccess transform)
            {
                if (!transform.isValid)
                {
                    Changed[index] = 0;
                    return;
                }

                Matrix4x4 matrix = transform.localToWorldMatrix;
                Quaternion rotation = transform.rotation;
                WatchedPose previous = Poses[index];
                if (previous.Matches(in matrix, in rotation))
                {
                    Changed[index] = 0;
                    return;
                }

                Poses[index] = new WatchedPose(in matrix, in rotation);
                Changed[index] = 1;
            }
        }

        static readonly ProfilerMarker CheckProfilerMarker = new ProfilerMarker("Text Studio TMP Scale Watch");

        // TMP's own update; null when this TMP version has none, which leaves every label in TMP's queue.
        static readonly Action<TextMeshProUGUI> s_internalUpdate = CreateInternalUpdate();

        // Parked labels counting settle checks in park order, which follows TMP's queue order; released entries stay null
        // until the next compaction.
        static TextStudioText[] s_pendingHubs = Array.Empty<TextStudioText>();
        static TextMeshProUGUI[] s_pendingTexts = Array.Empty<TextMeshProUGUI>();
        static int[] s_pendingSince = Array.Empty<int>();
        static int s_pendingCount;
        static int s_pendingReleased;

        // Watched labels, indexed like s_transforms, s_poses and s_changed. A taken label is out of TMP's queue.
        static TextStudioText[] s_hubs = Array.Empty<TextStudioText>();
        static TextMeshProUGUI[] s_texts = Array.Empty<TextMeshProUGUI>();
        static bool[] s_taken = Array.Empty<bool>();
        static int s_count;
        static int s_takenCount;
        static TransformAccessArray s_transforms;
        static NativeArray<WatchedPose> s_poses;
        static NativeArray<byte> s_changed;

        static int s_checks;
        static bool s_subscribed;
        static bool s_checking;
        static bool s_hasReleased;

        internal static int PendingCountForDebug => s_pendingCount - s_pendingReleased;
        internal static int WatchedCountForDebug => s_count;
        internal static int TakenCountForDebug => s_takenCount;

        internal static bool IsTakenForDebug(TextStudioText hub)
        {
            int slot = hub.TMPScaleWatchSlot;
            return slot >= 0 && slot < s_count && ReferenceEquals(s_hubs[slot], hub) && s_taken[slot];
        }

        /// <summary>Runs one check outside a canvas update, so tests can measure the steady check.</summary>
        internal static void CheckForDebug() => OnWillRenderCanvases();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlayMode() => ReleaseRuntimeState();

#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ReleaseRuntimeState()
        {
            if (s_subscribed)
            {
                Canvas.willRenderCanvases -= OnWillRenderCanvases;
                s_subscribed = false;
            }

            for (int index = 0; index < s_count; index++)
            {
                TextStudioText hub = s_hubs[index];
                if (hub is null)
                    continue;
                if (s_taken[index])
                    ReturnToTMP(s_texts[index]);
                if (hub.TMPScaleWatchSlot == index)
                    hub.TMPScaleWatchSlot = -1;
            }

            for (int index = 0; index < s_pendingCount; index++)
            {
                TextStudioText hub = s_pendingHubs[index];
                if (!(hub is null) && hub.TMPScaleWatchSlot == -2 - index)
                    hub.TMPScaleWatchSlot = -1;
            }

            Array.Clear(s_hubs, 0, s_hubs.Length);
            Array.Clear(s_texts, 0, s_texts.Length);
            Array.Clear(s_taken, 0, s_taken.Length);
            Array.Clear(s_pendingHubs, 0, s_pendingHubs.Length);
            Array.Clear(s_pendingTexts, 0, s_pendingTexts.Length);
            s_count = 0;
            s_takenCount = 0;
            s_pendingCount = 0;
            s_pendingReleased = 0;
            s_checking = false;
            s_hasReleased = false;
            DisposeNative();
        }

        /// <summary>
        /// Starts watching the TextMeshProUGUI of a hub that just parked. The hub must receive TMP's font writes through TMP's
        /// layout callback, since a new font asset can give InternalUpdate an environment map to update; unparking calls
        /// <see cref="Release"/>.
        /// </summary>
        internal static void Watch(TextStudioText hub, TextMeshProUGUI text)
        {
            if (s_internalUpdate == null || text is null || hub.TMPScaleWatchSlot != -1)
                return;

            if (s_pendingCount == s_pendingHubs.Length)
            {
                if (s_pendingReleased > 0)
                    CompactPending(false);
                if (s_pendingCount == s_pendingHubs.Length)
                {
                    int capacity = Math.Max(16, s_pendingHubs.Length * 2);
                    Array.Resize(ref s_pendingHubs, capacity);
                    Array.Resize(ref s_pendingTexts, capacity);
                    Array.Resize(ref s_pendingSince, capacity);
                }
            }

            int pending = s_pendingCount++;
            s_pendingHubs[pending] = hub;
            s_pendingTexts[pending] = text;
            s_pendingSince[pending] = s_checks;
            hub.TMPScaleWatchSlot = -2 - pending;
            if (!s_subscribed)
            {
                Canvas.willRenderCanvases += OnWillRenderCanvases;
                s_subscribed = true;
            }
        }

        /// <summary>Stops watching an unparking hub's label and returns a taken label to TMP's queue.</summary>
        internal static void Release(TextStudioText hub)
        {
            int slot = hub.TMPScaleWatchSlot;
            hub.TMPScaleWatchSlot = -1;
            if (slot >= 0)
            {
                if (slot >= s_count || !ReferenceEquals(s_hubs[slot], hub))
                    return;

                TextMeshProUGUI text = s_texts[slot];
                bool taken = s_taken[slot];
                if (taken)
                {
                    s_taken[slot] = false;
                    s_takenCount--;
                }

                if (s_checking)
                {
                    // TMP's text-changed event can unpark hubs during a check; the loop skips the entry and RemoveReleased
                    // drops it afterwards.
                    s_hubs[slot] = null;
                    s_hasReleased = true;
                }
                else
                {
                    RemoveWatched(slot);
                }

                if (taken)
                    ReturnToTMP(text);
            }
            else if (slot <= -2)
            {
                int pending = -2 - slot;
                if (pending >= s_pendingCount || !ReferenceEquals(s_pendingHubs[pending], hub))
                    return;
                s_pendingHubs[pending] = null;
                s_pendingTexts[pending] = null;
                s_pendingReleased++;
            }
        }

        static void OnWillRenderCanvases()
        {
            // A canvas update forced from a callback during a check would find the arrays mid-pass.
            if (s_checking)
                return;

            try
            {
                s_checks++;
                int pending = s_pendingCount - s_pendingReleased;
                bool promote = pending > 0 && pending + s_count >= MinimumPopulation;
                if (promote || s_pendingReleased > 0)
                    CompactPending(promote);
                if (s_count == 0)
                    return;

                using (CheckProfilerMarker.Auto())
                {
                    PoseJob job = new PoseJob { Poses = s_poses, Changed = s_changed };
                    if (s_count <= ParallelThreshold)
                        job.RunReadOnly(s_transforms);
                    else
                        job.ScheduleReadOnly(s_transforms, BatchSize).Complete();
                    ServiceWatched();
                }
            }
            catch (Exception exception)
            {
                // Later willRenderCanvases callbacks, such as TMP's own update, still run.
                Debug.LogException(exception);
            }
        }

        static void ServiceWatched()
        {
            s_checking = true;
            try
            {
                if (s_takenCount == s_count)
                    UpdateChangedLabels();
                else
                    ServiceAllLabels();
            }
            finally
            {
                s_checking = false;
                if (s_hasReleased)
                    RemoveReleased();
            }
        }

        // Steady state: every watched label is taken, so only labels whose pose changed need work. Flags past the last entry
        // stay zero (RemoveWatched clears them), so whole words of unchanged labels are skipped.
        static void UpdateChangedLabels()
        {
            NativeArray<ulong> words = s_changed.Reinterpret<ulong>(1);
            int count = s_count;
            int wordCount = (count + 7) >> 3;
            for (int word = 0; word < wordCount; word++)
            {
                if (words[word] == 0UL)
                    continue;
                int end = Math.Min(count, (word + 1) << 3);
                for (int index = word << 3; index < end; index++)
                {
                    if (s_changed[index] != 0 && s_taken[index])
                        UpdateLabel(s_texts[index]);
                }
            }
        }

        static void ServiceAllLabels()
        {
            for (int index = 0; index < s_count; index++)
            {
                if (s_hubs[index] is null)
                    continue;
                bool changed = s_changed[index] != 0;
                if (s_taken[index])
                {
                    if (changed)
                        UpdateLabel(s_texts[index]);
                }
                else if (!changed)
                {
                    Take(index);
                }
            }
        }

        // The label's pose matches the previous check, and TMP called InternalUpdate between the two checks with that pose, so
        // further calls change nothing until the pose changes. Labels are taken in watch order, which follows TMP's queue order
        // and keeps TMP's list removal at the front of its queue.
        static void Take(int index)
        {
            TextMeshProUGUI text = s_texts[index];
            if (!IsUpdatedOnlyForScale(text))
            {
                // TMP does not update the label, or updates more than its scale; it stays with TMP until its hub parks again.
                s_hubs[index].TMPScaleWatchSlot = -1;
                s_hubs[index] = null;
                s_hasReleased = true;
                return;
            }

            // Leaves TMP's InternalUpdate queue without changing the user's isTextObjectScaleStatic setting. TextMeshProUGUI
            // never uses the layout and graphic queues this also clears.
            TMP_UpdateManager.UnRegisterTextElementForRebuild(text);
            s_taken[index] = true;
            s_takenCount++;
        }

        // True while TMP keeps the label in its update queue, an enabled label that is not scale static under an enabled canvas,
        // and InternalUpdate does nothing but the scale check for it: no legacy animation regeneration and no environment map
        // matrix, which TMP writes for a shared material with a cube map (ValidateEnvMapProperty).
        static bool IsUpdatedOnlyForScale(TextMeshProUGUI text)
        {
            if (text == null || !text.isActiveAndEnabled || text.isTextObjectScaleStatic || text.isUsingLegacyAnimationComponent)
                return false;
            Canvas canvas = text.canvas;
            if (canvas == null || !canvas.enabled)
                return false;
            Material material = text.fontSharedMaterial;
            return material == null || !material.HasProperty(ShaderUtilities.ID_EnvMap) ||
                   material.GetTexture(ShaderUtilities.ID_EnvMap) == null;
        }

        // TMP calls InternalUpdate only for enabled labels that are not scale static. A disabled label rejoins TMP's queue in
        // OnEnable, which also unparks its hub.
        static void UpdateLabel(TextMeshProUGUI text)
        {
            if (text == null || !text.isActiveAndEnabled || text.isTextObjectScaleStatic)
                return;
            try
            {
                s_internalUpdate(text);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, text);
            }
        }

        // TMP's OnEnable, OnCanvasHierarchyChanged and isTextObjectScaleStatic setter each set the registration TMP keeps, also
        // while the label was taken, so a label returns to TMP's queue only where the last of them would have kept it there.
        // Assigning the current false value registers it without changing the user's setting.
        static void ReturnToTMP(TextMeshProUGUI text)
        {
            if (text == null || !text.isActiveAndEnabled || text.isTextObjectScaleStatic)
                return;
            Canvas canvas = text.canvas;
            if (canvas != null && canvas.enabled)
                text.isTextObjectScaleStatic = false;
        }

        // Moves settled pending labels to the watch while the population is large enough and drops released entries, keeping
        // park order.
        static void CompactPending(bool promote)
        {
            int write = 0;
            for (int read = 0; read < s_pendingCount; read++)
            {
                TextStudioText hub = s_pendingHubs[read];
                if (hub is null)
                    continue;
                TextMeshProUGUI text = s_pendingTexts[read];
                int since = s_pendingSince[read];
                if (promote && s_checks - since >= SettleChecks)
                {
                    if (text != null)
                        AddWatched(hub, text);
                    else
                        hub.TMPScaleWatchSlot = -1;
                    continue;
                }

                s_pendingHubs[write] = hub;
                s_pendingTexts[write] = text;
                s_pendingSince[write] = since;
                hub.TMPScaleWatchSlot = -2 - write;
                write++;
            }

            Array.Clear(s_pendingHubs, write, s_pendingCount - write);
            Array.Clear(s_pendingTexts, write, s_pendingCount - write);
            s_pendingCount = write;
            s_pendingReleased = 0;
        }

        static void AddWatched(TextStudioText hub, TextMeshProUGUI text)
        {
            EnsureWatchedCapacity(s_count + 1);
            int index = s_count++;
            s_hubs[index] = hub;
            s_texts[index] = text;
            s_taken[index] = false;
            s_poses[index] = default;
            s_changed[index] = 0;
            s_transforms.Add(text.transform);
            hub.TMPScaleWatchSlot = index;
        }

        static void RemoveReleased()
        {
            s_hasReleased = false;
            for (int index = s_count - 1; index >= 0; index--)
            {
                if (s_hubs[index] is null)
                    RemoveWatched(index);
            }
        }

        static void RemoveWatched(int index)
        {
            int last = --s_count;
            s_transforms.RemoveAtSwapBack(index);
            if (index != last)
            {
                TextStudioText moved = s_hubs[last];
                s_hubs[index] = moved;
                s_texts[index] = s_texts[last];
                s_taken[index] = s_taken[last];
                s_poses[index] = s_poses[last];
                s_changed[index] = s_changed[last];
                if (!(moved is null))
                    moved.TMPScaleWatchSlot = index;
            }

            s_hubs[last] = null;
            s_texts[last] = null;
            s_taken[last] = false;
            s_changed[last] = 0;
            if (s_count == 0)
                DisposeNative();
        }

        // Capacities stay multiples of eight, so the changed flags read as whole words.
        static void EnsureWatchedCapacity(int required)
        {
            int capacity = s_poses.IsCreated ? s_poses.Length : 0;
            if (s_transforms.isCreated && required <= capacity)
                return;

            capacity = Math.Max(64, capacity);
            while (capacity < required)
                capacity *= 2;

            if (s_transforms.isCreated)
                s_transforms.capacity = capacity;
            else
                s_transforms = new TransformAccessArray(capacity);

            NativeArray<WatchedPose> poses = new NativeArray<WatchedPose>(capacity, Allocator.Persistent);
            NativeArray<byte> changed = new NativeArray<byte>(capacity, Allocator.Persistent);
            if (s_poses.IsCreated)
            {
                NativeArray<WatchedPose>.Copy(s_poses, poses, s_count);
                NativeArray<byte>.Copy(s_changed, changed, s_count);
                s_poses.Dispose();
                s_changed.Dispose();
            }

            s_poses = poses;
            s_changed = changed;
            if (s_hubs.Length < capacity)
            {
                Array.Resize(ref s_hubs, capacity);
                Array.Resize(ref s_texts, capacity);
                Array.Resize(ref s_taken, capacity);
            }
        }

        static void DisposeNative()
        {
            if (s_transforms.isCreated)
                s_transforms.Dispose();
            if (s_poses.IsCreated)
                s_poses.Dispose();
            if (s_changed.IsCreated)
                s_changed.Dispose();
        }

        static Action<TextMeshProUGUI> CreateInternalUpdate()
        {
            MethodInfo method = typeof(TextMeshProUGUI).GetMethod(
                "InternalUpdate", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (method == null || method.ReturnType != typeof(void))
                return null;
            return Delegate.CreateDelegate(typeof(Action<TextMeshProUGUI>), method, false) as Action<TextMeshProUGUI>;
        }
    }
}
