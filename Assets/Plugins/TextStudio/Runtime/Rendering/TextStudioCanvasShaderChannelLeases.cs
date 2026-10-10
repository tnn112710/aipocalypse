using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEngine;

namespace TextStudio
{
    internal struct TextStudioCanvasShaderChannelLease
    {
        internal Canvas Canvas;
        internal int CanvasInstanceId;
        internal uint Generation;
        internal AdditionalCanvasShaderChannels Channels;

        internal bool IsValid => Canvas != null &&
                                 CanvasInstanceId != 0 &&
                                 Channels != AdditionalCanvasShaderChannels.None;
    }

    /// <summary>Reference-counts individual additional vertex channels across independent Text Studio consumers.</summary>
#if UNITY_6000_7_OR_NEWER
    // Explicit lifecycle reset releases owned resources and restores leases before clearing state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioCanvasShaderChannelLeases
    {
        static readonly Dictionary<int, Entry> s_entries = new Dictionary<int, Entry>();
        static uint s_generation = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ResetRuntimeState()
        {
            ResetForTests();
        }

        internal static void Acquire(
            Canvas canvas,
            AdditionalCanvasShaderChannels channels,
            ref TextStudioCanvasShaderChannelLease lease)
        {
            channels &= ~AdditionalCanvasShaderChannels.None;
            if (canvas == null || channels == AdditionalCanvasShaderChannels.None)
            {
                Release(ref lease);
                return;
            }
            if (lease.IsValid &&
                ReferenceEquals(lease.Canvas, canvas) &&
                lease.Channels == channels &&
                IsCurrent(lease, canvas))
            {
                return;
            }

            Release(ref lease);
            int instanceId = canvas.GetStableId();
            if (!s_entries.TryGetValue(instanceId, out Entry entry) ||
                !ReferenceEquals(entry.Canvas, canvas))
            {
                if (entry != null)
                    Restore(entry);
                entry = new Entry(canvas);
                s_entries[instanceId] = entry;
            }

            int bits = (int)channels;
            int newlyManaged = bits & ~entry.ManagedBits;
            entry.OriginalBits |= (int)canvas.additionalShaderChannels & newlyManaged;
            entry.ManagedBits |= bits;
            ForEachBit(bits, entry, 1);
            canvas.additionalShaderChannels |= channels;
            lease = new TextStudioCanvasShaderChannelLease
            {
                Canvas = canvas,
                CanvasInstanceId = instanceId,
                Generation = s_generation,
                Channels = channels
            };
        }

        internal static void Release(ref TextStudioCanvasShaderChannelLease lease)
        {
            if (!lease.IsValid)
            {
                lease = default;
                return;
            }

            if (lease.Generation == s_generation &&
                s_entries.TryGetValue(lease.CanvasInstanceId, out Entry entry) &&
                ReferenceEquals(entry.Canvas, lease.Canvas))
            {
                ForEachBit((int)lease.Channels, entry, -1);
                Apply(entry);
                if (entry.ActiveBits == 0)
                    s_entries.Remove(lease.CanvasInstanceId);
            }
            lease = default;
        }

        internal static bool IsCurrent(
            in TextStudioCanvasShaderChannelLease lease,
            Canvas canvas)
        {
            if (!lease.IsValid || lease.Generation != s_generation || !ReferenceEquals(lease.Canvas, canvas))
                return false;
            if (!s_entries.TryGetValue(lease.CanvasInstanceId, out Entry entry) ||
                !ReferenceEquals(entry.Canvas, canvas))
            {
                return false;
            }
            canvas.additionalShaderChannels |= lease.Channels;
            return true;
        }

        internal static void ResetForTests()
        {
            foreach (KeyValuePair<int, Entry> pair in s_entries)
                Restore(pair.Value);
            s_entries.Clear();
            unchecked { s_generation++; }
            if (s_generation == 0) s_generation = 1;
        }

        static void ForEachBit(int bits, Entry entry, int delta)
        {
            for (int bit = 0; bit < entry.Counts.Length; bit++)
            {
                int mask = 1 << bit;
                if ((bits & mask) == 0)
                    continue;
                entry.Counts[bit] = Mathf.Max(0, entry.Counts[bit] + delta);
            }
        }

        static void Apply(Entry entry)
        {
            if (entry.Canvas == null)
                return;
            int activeBits = entry.ActiveBits;
            int currentBits = (int)entry.Canvas.additionalShaderChannels;
            int restoredBits =
                (currentBits & ~entry.ManagedBits) |
                (entry.OriginalBits & entry.ManagedBits) |
                activeBits;
            entry.Canvas.additionalShaderChannels =
                (AdditionalCanvasShaderChannels)restoredBits;
        }

        static void Restore(Entry entry)
        {
            if (entry.Canvas == null)
                return;
            int currentBits = (int)entry.Canvas.additionalShaderChannels;
            entry.Canvas.additionalShaderChannels =
                (AdditionalCanvasShaderChannels)(
                    (currentBits & ~entry.ManagedBits) |
                    (entry.OriginalBits & entry.ManagedBits));
        }

        sealed class Entry
        {
            internal readonly Canvas Canvas;
            internal readonly int[] Counts = new int[32];
            internal int OriginalBits;
            internal int ManagedBits;

            internal int ActiveBits
            {
                get
                {
                    int bits = 0;
                    for (int bit = 0; bit < Counts.Length; bit++)
                    {
                        if (Counts[bit] > 0)
                            bits |= 1 << bit;
                    }
                    return bits;
                }
            }

            internal Entry(Canvas canvas)
            {
                Canvas = canvas;
            }
        }
    }
}
