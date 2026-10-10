using System;
using System.Collections.Generic;

namespace TextStudio
{
    /// <summary>Resolved scope ownership, independent of style assets and render programs.</summary>
    internal readonly struct TextStudioAppearanceCompositionSpan
    {
        internal readonly TextStudioAppearanceRange Domain;
        internal readonly TextStudioAppearanceApplyMode Mode;
        internal readonly bool Active;

        internal TextStudioAppearanceCompositionSpan(TextStudioAppearanceRange domain, TextStudioAppearanceApplyMode mode, bool active = true)
        {
            Domain = domain;
            Mode = mode;
            Active = active;
        }
    }

    /// <summary>Compiles shared semantic spans into persistent contribution chains and visible fragments.</summary>
    internal static class TextStudioAppearanceComposition
    {
        internal static void Build(IReadOnlyList<TextStudioAppearanceCompositionSpan> spans, int units,
            out TextStudioAppearanceContribution[] contributions, out int[] unitContributions,
            out TextStudioAppearanceRange[][] visibleRanges)
        {
            if (spans == null || spans.Count == 0) throw new ArgumentException("A root appearance scope is required.", nameof(spans));
            if (units < 0) throw new ArgumentOutOfRangeException(nameof(units));
            List<Boundary> boundaries = new List<Boundary>(Math.Max(0, spans.Count - 1) * 2);
            List<TextStudioAppearanceRange>[] visible = new List<TextStudioAppearanceRange>[spans.Count];
            for (int index = 0; index < spans.Count; index++)
            {
                visible[index] = new List<TextStudioAppearanceRange>();
                TextStudioAppearanceCompositionSpan span = spans[index];
                int start = Math.Clamp(span.Domain.Start, 0, units);
                int end = (int)Math.Clamp((long)span.Domain.Start + span.Domain.Length, start, units);
                if (index == 0 || !span.Active || end == start) continue;
                boundaries.Add(new Boundary(start, index, true));
                boundaries.Add(new Boundary(end, index, false));
            }
            boundaries.Sort(CompareBoundaries);
            List<TextStudioAppearanceContribution> links = new List<TextStudioAppearanceContribution>
            {
                new TextStudioAppearanceContribution(0, -1)
            };
            Dictionary<long, int> interned = new Dictionary<long, int>();
            List<int> active = new List<int>();
            unitContributions = new int[units];
            int position = 0;
            int cursor = 0;
            while (position < units)
            {
                while (cursor < boundaries.Count && boundaries[cursor].Position == position)
                {
                    Boundary boundary = boundaries[cursor++];
                    if (boundary.Open)
                    {
                        int insertion = active.BinarySearch(boundary.Occurrence);
                        active.Insert(insertion < 0 ? ~insertion : insertion, boundary.Occurrence);
                    }
                    else active.Remove(boundary.Occurrence);
                }
                int head = 0;
                foreach (int occurrence in active)
                {
                    int backdrop = spans[occurrence].Mode == TextStudioAppearanceApplyMode.Replace ? -1 : head;
                    long key = ((long)occurrence << 32) | (uint)(backdrop + 1);
                    if (!interned.TryGetValue(key, out head))
                    {
                        head = links.Count;
                        interned.Add(key, head);
                        links.Add(new TextStudioAppearanceContribution(occurrence, backdrop));
                    }
                }
                int end = cursor < boundaries.Count ? boundaries[cursor].Position : units;
                for (int unit = position; unit < end; unit++) unitContributions[unit] = head;
                for (int link = head; link >= 0; link = links[link].BackdropIndex)
                    AppendRange(visible[links[link].OccurrenceIndex], position, end);
                position = end;
            }
            contributions = links.ToArray();
            visibleRanges = new TextStudioAppearanceRange[spans.Count][];
            for (int index = 0; index < spans.Count; index++)
                visibleRanges[index] = visible[index].Count > 0 ? visible[index].ToArray() : Array.Empty<TextStudioAppearanceRange>();
        }

        static int CompareBoundaries(Boundary left, Boundary right) => left.Position != right.Position
            ? left.Position.CompareTo(right.Position)
            : left.Open != right.Open ? left.Open ? 1 : -1 : left.Occurrence.CompareTo(right.Occurrence);

        static void AppendRange(List<TextStudioAppearanceRange> ranges, int start, int end)
        {
            if (end <= start) return;
            if (ranges.Count > 0 && ranges[ranges.Count - 1].End == start)
            {
                TextStudioAppearanceRange last = ranges[ranges.Count - 1];
                ranges[ranges.Count - 1] = new TextStudioAppearanceRange(last.Start, end - last.Start);
            }
            else ranges.Add(new TextStudioAppearanceRange(start, end - start));
        }

        readonly struct Boundary
        {
            internal readonly int Position;
            internal readonly int Occurrence;
            internal readonly bool Open;
            internal Boundary(int position, int occurrence, bool open) { Position = position; Occurrence = occurrence; Open = open; }
        }
    }
}
