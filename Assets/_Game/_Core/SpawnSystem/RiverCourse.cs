using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using RM = StylizedWater3.RiverModeler;

namespace Aipocalypse.Core.SpawnSystem
{
    // The surface of sloped rivers and streams (Stylized Water River Modeler ribbons) for spawning in them: each spline sampled
    // once every Step metres into a course of centres on the surface with their half width. A point within a sample's width
    // and step has the level of the nearest such sample (the ribbon is level across, tilted along).
    public sealed class RiverCourse : IWaterLevel
    {
        // Metres between two samples along a river.
        public const float Step = 2f;
        // Samples per chunk, each chunk with its own footprint so a query skips most of the course.
        const int ChunkSize = 32;

        readonly List<Vector3> _centers = new();
        readonly List<Vector2> _forwards = new();
        readonly List<float> _halfWidths = new();
        readonly List<Rect> _chunks = new();

        public int Samples => _centers.Count;

        // Samples every river's splines (World space; widths from its scale data).
        public static RiverCourse From(IReadOnlyList<RM.RiverModeler> rivers)
        {
            var course = new RiverCourse();
            foreach (RM.RiverModeler river in rivers)
            {
                SplineContainer container = river != null ? river.SplineContainer : null;
                if (container == null)
                    continue;
                for (int s = 0; s < container.Splines.Count; s++)
                {
                    float length = container.Splines[s].GetLength();
                    if (length < 0.01f)
                        continue;
                    int steps = Mathf.Max(1, Mathf.CeilToInt(length / Step));
                    for (int i = 0; i <= steps; i++)
                    {
                        float distance = Mathf.Min(length, i * Step);
                        if (!container.Evaluate(s, distance / length, out float3 position, out float3 tangent, out float3 _))
                            continue;
                        course.Add(position, tangent, river.SampleWidth(s, distance) * 0.5f);
                    }
                }
            }
            course.Close();
            return course;
        }

        // A sample: `center` on the surface, `forward` along the flow, `halfWidth` metres either side.
        public void Add(Vector3 center, Vector3 forward, float halfWidth)
        {
            var flat = new Vector2(forward.x, forward.z);
            if (flat.sqrMagnitude < 1e-6f || halfWidth <= 0f)
                return;
            _centers.Add(center);
            _forwards.Add(flat.normalized);
            _halfWidths.Add(halfWidth);
        }

        // Builds the chunk footprints once every sample is added.
        public void Close()
        {
            _chunks.Clear();
            for (int start = 0; start < _centers.Count; start += ChunkSize)
            {
                int end = Mathf.Min(_centers.Count, start + ChunkSize);
                float reach = Step;
                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);
                for (int i = start; i < end; i++)
                {
                    reach = Mathf.Max(reach, _halfWidths[i] + Step);
                    var flat = new Vector2(_centers[i].x, _centers[i].z);
                    min = Vector2.Min(min, flat);
                    max = Vector2.Max(max, flat);
                }
                _chunks.Add(Rect.MinMaxRect(min.x - reach, min.y - reach, max.x + reach, max.y + reach));
            }
        }

        public bool TryGetLevel(Vector3 point, out float level)
        {
            level = float.NegativeInfinity;
            float nearest = float.MaxValue;
            var flat = new Vector2(point.x, point.z);
            for (int chunk = 0; chunk < _chunks.Count; chunk++)
            {
                if (!_chunks[chunk].Contains(flat))
                    continue;
                int end = Mathf.Min(_centers.Count, (chunk + 1) * ChunkSize);
                for (int i = chunk * ChunkSize; i < end; i++)
                {
                    Vector3 center = _centers[i];
                    var offset = new Vector2(point.x - center.x, point.z - center.z);
                    Vector2 forward = _forwards[i];
                    // A whole step either way, so the outside of a bend has no gaps.
                    if (Mathf.Abs(Vector2.Dot(offset, forward)) > Step)
                        continue;
                    if (Mathf.Abs(offset.x * forward.y - offset.y * forward.x) > _halfWidths[i])
                        continue;
                    float distance = offset.sqrMagnitude;
                    if (distance >= nearest)
                        continue;
                    nearest = distance;
                    level = center.y;
                }
            }
            return level > float.NegativeInfinity;
        }
    }
}
