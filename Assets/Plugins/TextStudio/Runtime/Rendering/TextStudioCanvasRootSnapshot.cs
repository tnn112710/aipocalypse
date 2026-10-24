using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// The root canvas of each canvas that holds Canvas text, with the root's transform, render mode and scale factor, read
    /// once per canvas and player frame. Settled styled text revalidates its batching payload and overlay projection every
    /// frame; reading the root again for every text made the payload check the largest idle cost of static styled labels.
    /// Edit Mode reads on every call because editor ticks do not advance frames.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // The snapshot serves Edit Mode too; every new player frame and every Edit Mode read starts it empty.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioCanvasRootSnapshot
    {
        internal struct Entry
        {
            internal Canvas Canvas;
            internal Canvas Root;
            internal Matrix4x4 LocalToWorld;
            internal RenderMode RenderMode;
            internal float ScaleFactor;
        }

        // A few canvases cover typical interfaces; texts under further canvases read their roots again.
        const int Capacity = 8;
        static readonly Entry[] s_entries = new Entry[Capacity];
        static int s_count;
        static int s_next;
        static int s_frame = -1;

        /// <summary>The root of a live canvas and the root's state, as first read during this player frame.</summary>
        internal static ref readonly Entry Of(Canvas canvas) => ref Of(canvas, Application.isPlaying ? Time.frameCount : -1);

        /// <summary>
        /// The same read for a caller that already holds the player frame, such as the hub scheduler; Edit Mode passes -1
        /// and reads again on every call.
        /// </summary>
        internal static ref readonly Entry Of(Canvas canvas, int frame)
        {
            if (frame != s_frame || frame < 0)
            {
                // Released entries must not keep destroyed canvases alive.
                for (int index = 0; index < s_count; index++)
                    s_entries[index] = default;
                s_count = 0;
                s_next = 0;
                // Edit Mode records no frame, so Play Mode never reuses a read made while frames did not advance.
                s_frame = frame;
            }

            for (int index = 0; index < s_count; index++)
            {
                if (ReferenceEquals(s_entries[index].Canvas, canvas))
                    return ref s_entries[index];
            }

            ref Entry entry = ref s_entries[s_count < Capacity ? s_count++ : s_next++ % Capacity];
            Canvas root = canvas.rootCanvas;
            entry.Canvas = canvas;
            entry.Root = root;
            entry.LocalToWorld = root.transform.localToWorldMatrix;
            entry.RenderMode = root.renderMode;
            entry.ScaleFactor = root.scaleFactor;
            return ref entry;
        }
    }
}
