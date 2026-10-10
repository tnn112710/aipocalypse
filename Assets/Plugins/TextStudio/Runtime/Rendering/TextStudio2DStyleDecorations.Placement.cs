using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    internal sealed partial class TextStudio2DStyleDecorations
    {
        readonly List<SurfacePlacementRow> _surfacePlacementRows = new List<SurfacePlacementRow>();
        readonly List<float> _surfaceColumns = new List<float>();
        readonly List<float> _surfaceRowCoordinates = new List<float>();
        int _surfacePlacementRowCount;

        bool BuildSurfacePlacement(TMP_Text text, in TextStudioFrame frame, in TextStudioVisualSnapshot snapshot)
        {
            _surfacePlacementRowCount = 0;
            if (!frame.Placements.IsCreated) return false;
            TextStudioText hub = text.GetComponent<TextStudioText>();
            foreach (SurfacePlacementRow row in _surfacePlacementRows) row.Glyphs.Clear();
            bool placed = false;
            foreach (int member in _surfaceMembers)
            {
                TextStudioVisualGlyph glyph = snapshot.GetGlyph(member);
                if (member >= frame.PlacementCount) continue;
                SurfacePlacementRow row = null;
                for (int i = 0; i < _surfacePlacementRowCount; i++)
                    if (_surfacePlacementRows[i].Line == glyph.LineIndex) { row = _surfacePlacementRows[i]; break; }
                if (row == null)
                {
                    if (_surfacePlacementRowCount == _surfacePlacementRows.Count) _surfacePlacementRows.Add(new SurfacePlacementRow());
                    row = _surfacePlacementRows[_surfacePlacementRowCount++];
                    row.Line = glyph.LineIndex;
                    row.HasGuide = false;
                    row.HasCurveGuide = false;
                    if (hub != null && hub.PlacementMode == PlacementMode.Arc)
                        row.HasGuide = hub.ArcPlacementSettings.TryGetGuide(row.Line, out row.Guide);
                    else if (hub != null && hub.PlacementMode == PlacementMode.Circle)
                        row.HasGuide = hub.CirclePlacementSettings.TryGetGuide(row.Line, out row.Guide);
                    row.Baseline = glyph.Baseline;
                    row.Bottom = float.PositiveInfinity;
                    row.Top = float.NegativeInfinity;
                    foreach (TextStudio2DSurfaceBand band in _surfaceBands)
                        if (band.Line == row.Line)
                        {
                            row.Bottom = Mathf.Min(row.Bottom, band.Bounds.yMin);
                            row.Top = Mathf.Max(row.Top, band.Bounds.yMax);
                        }
                }
                int curveOwner = hub != null && hub.PlacementMode == PlacementMode.Curve
                    ? hub.GetCurveGuideOwner(glyph.TextUnitIndex) : -1;
                TextStudioCurvePlacementGuide curveGuide = default;
                bool hasCurveGuide = curveOwner >= 0 &&
                    hub.TryGetCurvePlacementGuide(curveOwner, row.Line, out curveGuide);
                row.HasCurveGuide |= hasCurveGuide;
                Vector3 center = glyph.BoundsMax.y > glyph.BoundsMin.y
                    ? new Vector3((glyph.BoundsMin.x + glyph.BoundsMax.x) * .5f, (glyph.BoundsMin.y + glyph.BoundsMax.y) * .5f)
                    : new Vector3(glyph.Origin.x + glyph.Advance * .5f, glyph.Baseline);
                EvaluatedGlyphState state = EvaluatedGlyphState.Identity;
                GlyphPlacement placement = frame.Placements[member];
                TextStudioGlyphQuadTransform transform = TextStudioGlyphQuadTransformer.Create(center, in state, in placement, true, frame.GetEffectUnitScale(member), 1f);
                row.Glyphs.Add(new SurfacePlacement(center, placement, transform,
                    curveOwner, hasCurveGuide, curveGuide));
                placed |= (transform.ApplyPosition(center) - center).sqrMagnitude > .00000001f ||
                    (transform.ApplyPosition(center + Vector3.right) - center - Vector3.right).sqrMagnitude > .00000001f ||
                    (transform.ApplyPosition(center + Vector3.up) - center - Vector3.up).sqrMagnitude > .00000001f;
            }
            if (!placed) return false;
            for (int i = 0; i < _surfacePlacementRowCount; i++)
            {
                SurfacePlacementRow row = _surfacePlacementRows[i];

                row.Glyphs.Sort(SurfacePlacement.Compare);
            }
            _surfacePlacementRows.Sort(0, _surfacePlacementRowCount, SurfacePlacementRow.Order);
            return true;
        }

        void EmitPlacedSurface(GeometryBuffer target, Vector4 extent, Vector4 bounds, Color32 color,
            Vector4 geometry, Vector4 surface, in TextStudioVisualSnapshot snapshot, int styleSlot, int operationIndex)
        {
            _surfaceColumns.Clear();
            _surfaceRowCoordinates.Clear();
            _surfaceColumns.Add(extent.x); _surfaceColumns.Add(extent.z);
            _surfaceRowCoordinates.Add(extent.y); _surfaceRowCoordinates.Add(extent.w);
            for (int row = 0; row < _surfacePlacementRowCount; row++)
            {
                SurfacePlacementRow placed = _surfacePlacementRows[row];
                if (placed.Bottom > extent.y && placed.Bottom < extent.w) _surfaceRowCoordinates.Add(placed.Bottom);
                if (placed.Top > extent.y && placed.Top < extent.w) _surfaceRowCoordinates.Add(placed.Top);
                foreach (SurfacePlacement glyph in placed.Glyphs)
                    if (glyph.X > extent.x && glyph.X < extent.z) _surfaceColumns.Add(glyph.X);
                for (int glyph = 1; glyph < placed.Glyphs.Count; glyph++)
                {
                    SurfacePlacement left = placed.Glyphs[glyph - 1];
                    SurfacePlacement right = placed.Glyphs[glyph];
                    if (left.CurveOwner == right.CurveOwner) continue;
                    float boundary = (left.X + right.X) * .5f;
                    if (boundary > extent.x && boundary < extent.z) _surfaceColumns.Add(boundary);
                }
            }
            _surfaceColumns.Sort(); _surfaceRowCoordinates.Sort();
            // Interpolate neighboring placement frames across a tessellated source surface.
            // Source UVs keep its union boundary, holes, treatments, and masks continuous.
            int quads = 0;
            for (int row = 1; row < _surfaceRowCoordinates.Count; row++)
            {
                float bottom = _surfaceRowCoordinates[row - 1], top = _surfaceRowCoordinates[row];
                if (top - bottom < .00001f) continue;
                int verticalSteps = _surfacePlacementRowCount > 1 ? 4 : 1;
                for (int col = 1; col < _surfaceColumns.Count; col++)
                {
                    float left = _surfaceColumns[col - 1], right = _surfaceColumns[col];
                    if (right - left < .00001f) continue;
                    int horizontalSteps = 1;
                    for (int index = 0; index < _surfacePlacementRowCount; index++)
                    {
                        SurfacePlacementRow placed = _surfacePlacementRows[index];
                        if (placed.HasGuide)
                            horizontalSteps = Mathf.Max(horizontalSteps, Mathf.CeilToInt(Mathf.Abs(placed.Guide.Sweep * (placed.Progress(right) - placed.Progress(left))) / 3f));
                        if (placed.HasCurveGuide)
                            horizontalSteps = Mathf.Max(horizontalSteps, 8);
                    }
                    for (int y = 0; y < verticalSteps; y++)
                    for (int x = 0; x < horizontalSteps; x++)
                    {
                        if (++quads > 16384) throw new InvalidOperationException("This placed connected surface exceeds the 16384-quad budget. Split the selected range.");
                        float l = Mathf.Lerp(left, right, (float)x / horizontalSteps), r = Mathf.Lerp(left, right, (float)(x + 1) / horizontalSteps);
                        float b = Mathf.Lerp(bottom, top, (float)y / verticalSteps), t = Mathf.Lerp(bottom, top, (float)(y + 1) / verticalSteps);
                        float sampleX = (l + r) * .5f;
                        target.EmitQuad(PlaceSurfacePoint(new Vector3(l, b), sampleX), PlaceSurfacePoint(new Vector3(r, b), sampleX),
                            PlaceSurfacePoint(new Vector3(l, t), sampleX), PlaceSurfacePoint(new Vector3(r, t), sampleX), color,
                            SurfaceUv(l, b, bounds), SurfaceUv(r, b, bounds), SurfaceUv(l, t, bounds), SurfaceUv(r, t, bounds),
                            Program(l, b, in snapshot, styleSlot, operationIndex), Program(r, b, in snapshot, styleSlot, operationIndex),
                            Program(l, t, in snapshot, styleSlot, operationIndex), Program(r, t, in snapshot, styleSlot, operationIndex), geometry, surface);
                    }
                }
            }
        }

        static Vector4 SurfaceUv(float x, float y, Vector4 bounds)
        {
            float u = (x - bounds.x) / Mathf.Max(.0001f, bounds.z - bounds.x);
            float v = (y - bounds.y) / Mathf.Max(.0001f, bounds.w - bounds.y);
            return new Vector4(u, v, u, v);
        }

        Vector3 PlaceSurfacePoint(Vector3 point, float sampleX)
        {
            int upper = 0;
            while (upper < _surfacePlacementRowCount && _surfacePlacementRows[upper].Top < point.y) upper++;
            if (upper == 0) return _surfacePlacementRows[0].Place(point, sampleX);
            if (upper == _surfacePlacementRowCount) return _surfacePlacementRows[upper - 1].Place(point, sampleX);
            SurfacePlacementRow lowerRow = _surfacePlacementRows[upper - 1], upperRow = _surfacePlacementRows[upper];
            // Keep each complete line band in its own placement frame. Only the
            // connector through the empty inter-line gap blends neighboring frames.
            float weight = Mathf.InverseLerp(lowerRow.Top, upperRow.Bottom, point.y);
            return Vector3.LerpUnclamped(lowerRow.Place(point, sampleX), upperRow.Place(point, sampleX), weight);
        }

        readonly struct SurfacePlacement
        {
            internal readonly float X;
            internal readonly Vector3 Center;
            internal readonly GlyphPlacement Placement;
            internal readonly TextStudioGlyphQuadTransform Transform;
            internal readonly int CurveOwner;
            internal readonly bool HasCurveGuide;
            internal readonly TextStudioCurvePlacementGuide CurveGuide;
            internal SurfacePlacement(Vector3 center, GlyphPlacement placement, TextStudioGlyphQuadTransform transform,
                int curveOwner, bool hasCurveGuide, TextStudioCurvePlacementGuide curveGuide)
            {
                X = center.x; Center = center; Placement = placement; Transform = transform;
                CurveOwner = curveOwner; HasCurveGuide = hasCurveGuide; CurveGuide = curveGuide;
            }
            internal static int Compare(SurfacePlacement a, SurfacePlacement b) => a.X.CompareTo(b.X);
        }

#if UNITY_6000_7_OR_NEWER
        // The shared comparer or empty metadata has no Play Mode state.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        sealed class SurfacePlacementRow
        {
            internal static readonly IComparer<SurfacePlacementRow> Order = Comparer<SurfacePlacementRow>.Create((a, b) => a.Baseline.CompareTo(b.Baseline));
            internal int Line;
            internal float Baseline;
            internal float Bottom;
            internal float Top;
            internal bool HasGuide;
            internal CircularPlacementGuide Guide;
            internal bool HasCurveGuide;
            internal readonly List<SurfacePlacement> Glyphs = new List<SurfacePlacement>();
            int UpperGlyph(float x)
            {
                int low = 0, high = Glyphs.Count;
                while (low < high)
                {
                    int middle = (low + high) / 2;
                    if (Glyphs[middle].X < x) low = middle + 1; else high = middle;
                }
                return low;
            }

            internal float Progress(float x)
            {
                int low = UpperGlyph(x);
                if (low == 0) return Glyphs[0].Placement.pathProgress;
                if (low == Glyphs.Count) return Glyphs[low - 1].Placement.pathProgress;
                return Mathf.Lerp(Glyphs[low - 1].Placement.pathProgress, Glyphs[low].Placement.pathProgress,
                    Mathf.InverseLerp(Glyphs[low - 1].X, Glyphs[low].X, x));
            }

            Quaternion PathRotation(Vector3 up)
            {
                Vector3 tangent = new Vector3(up.y, -up.x, 0) * Mathf.Sign(Guide.Sweep);
                return PlacementOrientation.AlignLocalRight(tangent, up);
            }

            internal Vector3 Place(Vector3 point, float sampleX)
            {
                int low = UpperGlyph(sampleX);
                if (low == 0) return Glyphs[0].Transform.ApplyPosition(point);
                if (low == Glyphs.Count) return Glyphs[low - 1].Transform.ApplyPosition(point);
                SurfacePlacement left = Glyphs[low - 1], right = Glyphs[low];
                if (left.CurveOwner != right.CurveOwner)
                    return (sampleX < (left.X + right.X) * .5f ? left : right).Transform.ApplyPosition(point);
                float weight = Mathf.InverseLerp(left.X, right.X, point.x);
                if (HasGuide)
                {
                    Vector3 guide0 = Guide.Evaluate(left.Placement.pathProgress, out Vector3 up0);
                    Vector3 guide1 = Guide.Evaluate(right.Placement.pathProgress, out Vector3 up1);
                    Vector3 position = Guide.Evaluate(Mathf.Lerp(left.Placement.pathProgress, right.Placement.pathProgress, weight), out Vector3 up);
                    float offset0 = Vector3.Dot(left.Placement.position - guide0, up0);
                    float offset1 = Vector3.Dot(right.Placement.position - guide1, up1);
                    position += up * Mathf.Lerp(offset0, offset1, weight);
                    Quaternion residual0 = Quaternion.Inverse(PathRotation(up0)) * left.Placement.rotation;
                    Quaternion residual1 = Quaternion.Inverse(PathRotation(up1)) * right.Placement.rotation;
                    Quaternion rotation = PathRotation(up) * Quaternion.Slerp(residual0, residual1, weight);
                    Vector3 center = Vector3.LerpUnclamped(left.Center, right.Center, weight);
                    Vector3 scale = Vector3.LerpUnclamped(left.Placement.scale, right.Placement.scale, weight);
                    return position + rotation * Vector3.Scale(point - center, scale);
                }
                if (left.HasCurveGuide && right.HasCurveGuide)
                {
                    TextStudioCurvePlacementGuide curveGuide = left.CurveGuide;
                    Vector3 guide0 = curveGuide.Evaluate(left.X, out Vector3 up0);
                    Vector3 guide1 = curveGuide.Evaluate(right.X, out Vector3 up1);
                    Vector3 position = curveGuide.Evaluate(point.x, out Vector3 up);
                    float offset0 = Vector3.Dot(left.Placement.position - guide0, up0);
                    float offset1 = Vector3.Dot(right.Placement.position - guide1, up1);
                    position += up * Mathf.Lerp(offset0, offset1, weight);
                    Quaternion path0 = PlacementOrientation.AlignLocalRight(
                        new Vector3(up0.y, -up0.x, 0f), up0);
                    Quaternion path1 = PlacementOrientation.AlignLocalRight(
                        new Vector3(up1.y, -up1.x, 0f), up1);
                    Quaternion path = PlacementOrientation.AlignLocalRight(
                        new Vector3(up.y, -up.x, 0f), up);
                    Quaternion rotation = path * Quaternion.Slerp(
                        Quaternion.Inverse(path0) * left.Placement.rotation,
                        Quaternion.Inverse(path1) * right.Placement.rotation, weight);
                    Vector3 center = Vector3.LerpUnclamped(left.Center, right.Center, weight);
                    Vector3 scale = Vector3.LerpUnclamped(left.Placement.scale, right.Placement.scale, weight);
                    return position + rotation * Vector3.Scale(point - center, scale);
                }
                return Vector3.LerpUnclamped(left.Transform.ApplyPosition(point), right.Transform.ApplyPosition(point), weight);
            }
        }
    }
}
