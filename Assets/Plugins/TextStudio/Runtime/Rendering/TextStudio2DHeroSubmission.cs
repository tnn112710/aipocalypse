using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TextStudio
{
    /// <summary>
    /// Records Hero compositors' offscreen work into one command stream and submits it once per batch instead of one
    /// <see cref="Graphics.ExecuteCommandBuffer"/> per owner. Play Mode defers every owner to the frame's single
    /// submission before canvases or cameras render; Edit Mode batches the editor tick through <see cref="Batch"/>, while
    /// an owner rendered on its own (inspector previews, tooling) completes synchronously. Commands keep append order, so
    /// a pooled target that a later owner reuses still observes an earlier owner's work complete. Recorded work reads only
    /// its own recorded state, property blocks captured at record time, immutable keyed materials and the shared global
    /// tables. Destroying anything a recording may reference, explicit synchronous refreshes and camera-time re-renders
    /// therefore flush first.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Explicit lifecycle reset releases owned resources and restores leases before clearing state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudio2DHeroSubmission
    {
        static CommandBuffer s_stream;
        static int s_recordingFrame = -1;
        static int s_pendingOwners;
        static int s_batchDepth;
        static bool s_hooked;

        /// <summary>Owner renders recorded into the pending stream.</summary>
        internal static int PendingOwners => s_pendingOwners;
        /// <summary>Stream submissions since the last runtime reset.</summary>
        internal static int SubmissionCount { get; private set; }
        /// <summary>Owner renders carried by those submissions.</summary>
        internal static int SubmittedOwnerCount { get; private set; }

        /// <summary>The frame stream owners record into. Access it only between <see cref="Begin"/> and <see cref="Recorded"/>.</summary>
        internal static CommandBuffer Stream => s_stream ??= new CommandBuffer { name = "Text Studio Hero Composition" };

        /// <summary>Starts an owner recording, first submitting work left from an earlier frame that no camera consumed.</summary>
        internal static void Begin()
        {
            int frame = Time.frameCount;
            if (s_recordingFrame != frame)
            {
                Flush();
                s_recordingFrame = frame;
            }
            EnsureHooks();
        }

        /// <summary>Completes one owner recording; it runs with the batch's single submission.</summary>
        internal static void Recorded()
        {
            s_pendingOwners++;
            if (!Application.isPlaying && s_batchDepth == 0)
                Flush();
        }

        /// <summary>Groups Edit Mode owner renders into one submission, made when the outermost scope ends.</summary>
        internal static BatchScope Batch()
        {
            s_batchDepth++;
            return new BatchScope(true);
        }

        internal readonly struct BatchScope : IDisposable
        {
            readonly bool _active;
            internal BatchScope(bool active) => _active = active;

            public void Dispose()
            {
                if (!_active || s_batchDepth == 0) return;
                if (--s_batchDepth == 0 && !Application.isPlaying) Flush();
            }
        }

        /// <summary>Submits pending work now. Cheap when nothing is pending.</summary>
        internal static void Flush()
        {
            if (s_stream == null || s_stream.sizeInBytes == 0)
                return;
            Graphics.ExecuteCommandBuffer(s_stream);
            s_stream.Clear();
            SubmissionCount++;
            SubmittedOwnerCount += s_pendingOwners;
            s_pendingOwners = 0;
        }

        static void EnsureHooks()
        {
            if (s_hooked)
                return;
            s_hooked = true;
            Canvas.willRenderCanvases += Flush;
            Camera.onPreCull += FlushBeforeCamera;
            RenderPipelineManager.beginContextRendering += FlushBeforeContext;
        }

        static void FlushBeforeCamera(Camera camera) => Flush();

        static void FlushBeforeContext(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras) => Flush();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        internal static void ResetRuntimeState()
        {
            if (s_hooked)
            {
                Canvas.willRenderCanvases -= Flush;
                Camera.onPreCull -= FlushBeforeCamera;
                RenderPipelineManager.beginContextRendering -= FlushBeforeContext;
                s_hooked = false;
            }
            s_stream?.Release();
            s_stream = null;
            s_recordingFrame = -1;
            s_pendingOwners = 0;
            s_batchDepth = 0;
            SubmissionCount = 0;
            SubmittedOwnerCount = 0;
        }
    }
}
