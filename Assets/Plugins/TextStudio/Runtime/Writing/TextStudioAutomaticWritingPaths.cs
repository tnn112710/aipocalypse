using System;
using System.Collections.Generic;
using TextStudio.Geometry;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Font-owned, bounded estimates of pen paths derived from the filled glyph contours.</summary>
    internal sealed class TextStudioAutomaticWritingPaths
    {
        const int Resolution = 112;
        const int MaximumEntries = 64;
        readonly Dictionary<ulong, TextStudioPreparedWritingPath> _paths = new Dictionary<ulong, TextStudioPreparedWritingPath>();
        uint _revision;

        internal TextStudioPreparedWritingPath Get(TextStudioFontAppearanceData font, uint glyph, bool outline)
        {
            if (_revision != font.Revision) { _paths.Clear(); _revision = font.Revision; }
            ulong key = ((ulong)glyph << 1) | (outline ? 1UL : 0UL);
            if (_paths.TryGetValue(key, out TextStudioPreparedWritingPath path)) return path;
            MeasuredContourSet contours = font.GetContours(glyph, 0.001f);
            if (contours == null || contours.Count == 0) return null;
            Vector2[][] loops = new Vector2[contours.Count][];
            for (int i = 0; i < loops.Length; i++) loops[i] = contours.GetContour(i).ToArray();
            path = Build(loops, outline);
            if (_paths.Count >= MaximumEntries) _paths.Clear();
            _paths.Add(key, path);
            return path;
        }

        internal static TextStudioPreparedWritingPath Build(Vector2[][] loops, bool outline)
        {
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector2[] loop in loops)
                foreach (Vector2 point in loop) { minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point); }
            if (!float.IsFinite(minimum.x) || maximum.x <= minimum.x || maximum.y <= minimum.y) return null;
            Rect bounds = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            List<Vector2[]> strokes = new List<Vector2[]>();
            if (outline)
            {
                foreach (Vector2[] loop in loops)
                {
                    if (loop.Length < 3) continue;
                    Vector2[] closed = new Vector2[loop.Length + 1];
                    Array.Copy(loop, closed, loop.Length);
                    closed[loop.Length] = loop[0];
                    strokes.Add(closed);
                }
            }
            else
            {
                float step = Mathf.Max(bounds.width, bounds.height) / (Resolution - 5);
                int width = Mathf.CeilToInt(bounds.width / step) + 5;
                int height = Mathf.CeilToInt(bounds.height / step) + 5;
                Vector2 origin = minimum - Vector2.one * (2 * step);
                bool[] mask = new bool[width * height];
                for (int y = 1; y < height - 1; y++)
                    for (int x = 1; x < width - 1; x++)
                        mask[y * width + x] = Contains(loops, origin + new Vector2(x, y) * step);
                Thin(mask, width, height);
                Trace(mask, width, height, origin, step, strokes);
            }
            return Prepare(strokes, bounds, outline);
        }

        static bool Contains(Vector2[][] loops, Vector2 point)
        {
            bool inside = false;
            foreach (Vector2[] loop in loops)
                for (int i = 0, j = loop.Length - 1; i < loop.Length; j = i++)
                {
                    Vector2 a = loop[i], b = loop[j];
                    if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                        inside = !inside;
                }
            return inside;
        }

        // Two-pass topology-preserving thinning keeps counters and connected strokes intact.
        static void Thin(bool[] mask, int width, int height)
        {
            List<int> removed = new List<int>();
            int[] offsets = {-width, -width + 1, 1, width + 1, width, width - 1, -1, -width - 1};
            for (int iteration = 0; iteration < Resolution; iteration++)
            {
                bool changed = false;
                for (int pass = 0; pass < 2; pass++)
                {
                    removed.Clear();
                    for (int y = 1; y < height - 1; y++)
                        for (int x = 1; x < width - 1; x++)
                        {
                            int index = y * width + x;
                            if (!mask[index]) continue;
                            int neighbors = 0, transitions = 0;
                            for (int n = 0; n < 8; n++)
                            {
                                bool current = mask[index + offsets[n]];
                                if (current) neighbors++;
                                if (!current && mask[index + offsets[(n + 1) % 8]]) transitions++;
                            }
                            if (neighbors < 2 || neighbors > 6 || transitions != 1) continue;
                            bool north = mask[index - width], east = mask[index + 1], south = mask[index + width], west = mask[index - 1];
                            if (pass == 0 ? (north && east && south) || (east && south && west)
                                : (north && east && west) || (north && south && west)) continue;
                            removed.Add(index);
                        }
                    foreach (int index in removed) mask[index] = false;
                    changed |= removed.Count > 0;
                }
                if (!changed) break;
            }
        }

        static void Trace(bool[] mask, int width, int height, Vector2 origin, float scale, List<Vector2[]> strokes)
        {
            List<int>[] edges = new List<int>[mask.Length];
            for (int index = 0; index < mask.Length; index++)
            {
                if (!mask[index]) continue;
                edges[index] = new List<int>(3);
                int x = index % width, y = index / width;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if ((dx == 0 && dy == 0) || x + dx < 0 || x + dx >= width || y + dy < 0 || y + dy >= height) continue;
                        int next = index + dy * width + dx;
                        if (!mask[next]) continue;
                        if (dx != 0 && dy != 0 && (mask[index + dx] || mask[index + dy * width])) continue;
                        edges[index].Add(next);
                    }
            }
            for (int stroke = 0; stroke < 256; stroke++)
            {
                int start = -1;
                // Prefer a topmost endpoint. Remaining cycles start at their topmost pixel.
                for (int endpointPass = 0; endpointPass < 2 && start < 0; endpointPass++)
                    for (int y = height - 1; y >= 0 && start < 0; y--)
                        for (int x = 0; x < width; x++)
                        {
                            int index = y * width + x;
                            if (edges[index] != null && (endpointPass == 0 ? edges[index].Count == 1 : edges[index].Count > 0)) { start = index; break; }
                        }
                if (start < 0) break;
                List<Vector2> points = new List<Vector2>();
                int current = start;
                Vector2 direction = Vector2.down;
                points.Add(origin + new Vector2(current % width, current / width) * scale);
                while (edges[current].Count > 0)
                {
                    int next = edges[current][0];
                    float best = float.NegativeInfinity;
                    foreach (int candidate in edges[current])
                    {
                        Vector2 delta = new Vector2(candidate % width - current % width, candidate / width - current / width).normalized;
                        float score = Vector2.Dot(direction, delta);
                        if (score > best) { best = score; next = candidate; }
                    }
                    edges[current].Remove(next); edges[next].Remove(current);
                    Vector2 position = origin + new Vector2(next % width, next / width) * scale;
                    direction = (position - points[points.Count - 1]).normalized;
                    points.Add(position); current = next;
                    if (current == start) break;
                }
                if (points.Count < 3) continue;
                Vector2[] smooth = points.ToArray();
                for (int i = 1; i < smooth.Length - 1; i++) smooth[i] = (points[i - 1] + points[i] * 2 + points[i + 1]) * 0.25f;
                strokes.Add(smooth);
            }
        }

        static TextStudioPreparedWritingPath Prepare(List<Vector2[]> paths, Rect bounds, bool outline)
        {
            List<TextStudioPreparedWritingPoint> points = new List<TextStudioPreparedWritingPoint>();
            List<TextStudioPreparedWritingStroke> strokes = new List<TextStudioPreparedWritingStroke>();
            float total = 0;
            foreach (Vector2[] path in paths) for (int i = 1; i < path.Length; i++) total += Vector2.Distance(path[i - 1], path[i]);
            if (total <= 0) return null;
            float traversed = 0;
            TextStudioDeterministicHash64 hash = default;
            foreach (Vector2[] path in paths)
            {
                float length = 0;
                for (int i = 1; i < path.Length; i++) length += Vector2.Distance(path[i - 1], path[i]);
                int first = points.Count;
                float distance = 0;
                for (int i = 0; i < path.Length; i++)
                {
                    if (i > 0) distance += Vector2.Distance(path[i - 1], path[i]);
                    Vector2 tangent = path[Mathf.Min(i + 1, path.Length - 1)] - path[Mathf.Max(0, i - 1)];
                    points.Add(new TextStudioPreparedWritingPoint(path[i], tangent, 1f, 0f, distance, distance / Mathf.Max(length, 0.00001f)));
                    hash.Add(path[i]);
                }
                strokes.Add(new TextStudioPreparedWritingStroke(TextStudioStackItemId.Create("writing.auto." + strokes.Count),
                    outline ? TextStudioWritingStrokeRole.Outline : TextStudioWritingStrokeRole.Centerline,
                    first, path.Length, length, traversed / total, length / total, bounds, outline));
                traversed += length;
            }
            return new TextStudioPreparedWritingPath(points.ToArray(), strokes.ToArray(), bounds, total, hash.Value);
        }
    }
}
