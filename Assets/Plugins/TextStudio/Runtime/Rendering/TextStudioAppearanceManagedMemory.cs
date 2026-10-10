using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TextStudio.Geometry;
using Unity.Collections.LowLevel.Unsafe;

namespace TextStudio
{
    /// <summary>Observed reachable payloads. Separate from admitted table/texture reservations and process heap size.</summary>
    internal struct TextStudioAppearanceManagedMemorySnapshot
    {
        internal long ProgramBytes, SelectionBytes, MeshBufferBytes, FontSourceBytes, CurveBytes, ContourBytes;
        internal int ProgramCount, FontCount, ParsedFaceCount;
        internal long TotalBytes => ProgramBytes + SelectionBytes + MeshBufferBytes + FontSourceBytes + CurveBytes + ContourBytes;
    }

    /// <summary>Short-lived, on-demand measurement. Never registers owners or runs in rendering ticks.</summary>
    internal sealed class TextStudioAppearanceManagedMemory
    {
        readonly HashSet<object> _seen = new HashSet<object>(IdentityComparer.Instance);
        internal TextStudioAppearanceManagedMemorySnapshot Snapshot;

        internal void Add(TextStudioText hub)
        {
            if (hub == null || !_seen.Add(hub)) return;
            Snapshot.SelectionBytes += hub.AppearanceSelectionPayloadBytes;
            Snapshot.MeshBufferBytes += hub.AppearanceMeshPayloadBytes;
            CompiledTextStudio2DStyle style = hub.CompiledAppearanceStyle;
            AddProgram(style);
            CompiledTextStudioAppearancePlan plan = hub.CompiledAppearancePlan;
            if (plan != null && _seen.Add(plan))
            {
                Snapshot.SelectionBytes += plan.CaptureManagedPayload(this);
                foreach (CompiledTextStudio2DStyle program in plan.Programs) AddProgram(program);
            }
            for (int index = 0; index < hub.AppearanceFontCount; index++) Add(hub.GetAppearanceFont(index));
        }

        void AddProgram(CompiledTextStudio2DStyle style)
        {
            if (style == null || style.IsPassThrough || !_seen.Add(style)) return;
            Snapshot.ProgramCount++;
            Snapshot.ProgramBytes += style.CaptureManagedPayload(this);
        }

        internal bool Visit(object value) => value != null && _seen.Add(value);

        internal void Add(TextStudioFontAppearanceData font)
        {
            if (font == null || !_seen.Add(font)) return;
            Snapshot.FontCount++;
            Snapshot.FontSourceBytes += font.SourceBytes;
            Snapshot.ContourBytes += font.ContourPayloadBytes;
            ICurveFontFace face = font.ExistingCurveSource;
            if (face == null || !_seen.Add(face)) return;
            FontCurveSourceCache.GetRetainedPayload(face, out long source, out long curves);
            Snapshot.ParsedFaceCount++;
            Snapshot.FontSourceBytes += source;
            Snapshot.CurveBytes += curves;
        }

        internal long Array<T>(T[] values) where T : struct => values != null && _seen.Add(values) ? ArrayBytes(values) : 0;
        internal long Text(string value) => value != null && _seen.Add(value) ? value.Length * 2L : 0;
        internal static long ArrayBytes<T>(T[] values) where T : struct => (values?.LongLength ?? 0) * UnsafeUtility.SizeOf<T>();
        internal static long ListBytes<T>(List<T> values) where T : struct => (values?.Capacity ?? 0L) * UnsafeUtility.SizeOf<T>();

        sealed class IdentityComparer : IEqualityComparer<object>
        {
#if UNITY_6000_7_OR_NEWER
            [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
            internal static readonly IdentityComparer Instance = new IdentityComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
