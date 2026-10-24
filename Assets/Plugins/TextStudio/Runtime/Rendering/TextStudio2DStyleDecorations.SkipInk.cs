using System;
using System.Collections.Generic;
using TextStudio.Geometry;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace TextStudio
{
    internal sealed partial class TextStudio2DStyleDecorations
    {
        // A static mask orders first the edges within this many em of its line's tiles. The lean line program measures only
        // those for pixels whose antialiasing and treatment reach stay inside that band, and every edge for the others.
        const float InkBandMarginEm = .25f;
        // A static mask over the edge budget keeps the outlines within this many em of its line's tiles. Its coverage stays
        // exact while the antialiasing width and treatment reach fit in the margin, above about ten output pixels per em.
        const float InkClipMarginEm = .5f;
        internal const string InkBudgetMetricWarning = "The glyph outlines of a skip-ink line exceed the 2048-edge budget, so the line skips the metric bounds of its glyphs instead. Split the line to keep outline gaps.";
        internal const string InkBudgetExceededWarning = "Skip ink for a line exceeds the 2048-edge budget even with metric glyph bounds, so the line stays continuous. Split the line.";
        readonly List<Vector2[]> _inkShapes = new List<Vector2[]>();
        readonly List<TextStudio2DNativeInkSource> _nativeInkSources = new List<TextStudio2DNativeInkSource>();
        // Set when a line of the operation being emitted samples live Writing ink, which only the general program evaluates.
        bool _liveInkEmitted;

        TextStudio2DSurfaceHandle PrepareSkipInk(in TextStudioFrame frame, in TextStudioVisualSnapshot snapshot,
            TMP_Text text, in TextStudio2DDecorationSpec decoration, in DecorationRun run, Vector4 expansion, Vector4 adjustmentMask,
            TextStudioPreparedFieldDemand sourceDemand, TextStudioTMPFrameApplier frameApplier)
        {
            TextStudio2DDecorationLineSpec line = decoration.Line;
            if (line.SkipInk == TextStudio2DSkipInk.Off) return default;
            bool pixelLine = line.Units == TextStudio2DUnit.Pixels;
            bool hasLiveInk = false;
            TextStudioDeterministicHash64 hash = default;
            hash.Add(pixelLine); hash.Add(run.Left); hash.Add(run.Right);
            hash.Add((byte)line.SkipInk); hash.Add(line.InkClearance); hash.Add(run.EmScale);
            // The band order and the budget clip follow the line's tiles.
            hash.Add(run.Bottom); hash.Add(run.Height);
            hash.Add(expansion.x); hash.Add(expansion.y); hash.Add(expansion.z); hash.Add(expansion.w);
            for (int index = 0; index < snapshot.RenderGlyphCount; index++)
            {
                ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(index);
                if (!IsNativeInkVisible(in frame, in glyph) || glyph.LineIndex != run.LineIndex) continue;
                bool posed = TextStudio2DTMPGlyphPlane.TryCreate(text, in glyph, out TextStudio2DTMPGlyphPlane plane, frameApplier, sourceDemand);
                if (posed && !plane.HasVisibleSource) continue;
                hasLiveInk |= posed && plane.WritingSample > .5f;
                hash.Add(posed ? plane.WritingSample : 0);
                hash.Add(posed ? plane.NativeFaceOffsetEm : 0);
                hash.Add(glyph.FontIdentity); hash.Add(glyph.GlyphIndex); hash.Add((byte)glyph.SourceKind);
                hash.Add(glyph.Origin.x); hash.Add(glyph.Origin.y); hash.Add(glyph.LocalEmSize);
                hash.Add(glyph.BoundsMin.x); hash.Add(glyph.BoundsMin.y); hash.Add(glyph.BoundsMax.x); hash.Add(glyph.BoundsMax.y);
                if (posed)
                {
                    hash.Add(plane.BottomLeft.x); hash.Add(plane.BottomLeft.y); hash.Add(plane.TopLeft.x); hash.Add(plane.TopLeft.y);
                    hash.Add(plane.TopRight.x); hash.Add(plane.TopRight.y); hash.Add(plane.BottomRight.x); hash.Add(plane.BottomRight.y);
                }
                if (snapshot.TryGetFontSource(glyph.FontIdentity, out TextStudioVisualFontSource source))
                { hash.Add(source.Revision); hash.Add(source.AppearanceData.SourceHash); hash.Add(ResolveInkTolerance(sourceDemand, glyph.FontIdentity)); }
                else if (line.SkipInk == TextStudio2DSkipInk.Precise)
                    CompatibilityWarning = "Precise skip ink needs prepared font outlines. Use Prepare Appearance Data for each font. The line stays continuous where that source is unavailable.";
            }
            _liveInkEmitted |= hasLiveInk;
            if (_activeSurfaces >= 128)
            {
                CompatibilityWarning = "The text exceeds the 128 line and connected-surface budget. Split the text to prepare additional skip-ink lines.";
                return default;
            }
            while (_surfaces.Count <= _activeSurfaces) _surfaces.Add(new ConnectedSurface());
            ConnectedSurface surface = _surfaces[_activeSurfaces];
            try
            {
                if (surface.Geometry == null || surface.InputFingerprint != hash.Value)
                {
                    TextStudio2DSurfaceGeometry geometry = BuildInkGeometry(in frame, in snapshot, text, in line, in run, expansion, pixelLine,
                        hasLiveInk, sourceDemand, frameApplier, out string warning);
                    if (geometry == null)
                    {
                        CompatibilityWarning = warning;
                        return default;
                    }
                    surface.Geometry = geometry;
                    surface.InputFingerprint = hash.Value;
                    surface.Warning = warning;
                }
                // A cached fallback keeps reporting itself on every build that reuses it.
                if (!string.IsNullOrEmpty(surface.Warning)) CompatibilityWarning = surface.Warning;
                if (surface.Geometry.Edges.Length == 0) return default;
                if (!TextStudio2DSurfaceTable.Shared.TryUpdate(ref surface.Handle, surface.Geometry, adjustmentMask))
                {
                    CompatibilityWarning = TextStudio2DSurfaceTable.Shared.MemoryBudgetExceeded
                        ? TextStudioAppearanceMemory.LastRejection.ToString()
                        : "The shared contour pool is full. Skip ink remains inactive for this line.";
                    return default;
                }
                _activeSurfaces++;
                return surface.Handle;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException || exception is NotSupportedException)
            {
                _inkShapes.Clear();
                _nativeInkSources.Clear();
                CompatibilityWarning = "Skip ink could not prepare this line: " + exception.Message;
                return default;
            }
        }

        // Prefers the union of the line's whole native ink. When it exceeds the edge budget, an em-sized static mask keeps the
        // outlines near its tiles, and any mask still over budget falls back to metric glyph bounds with an explicit warning.
        // Returns null, with the warning, only when even those bounds exceed the budget.
        TextStudio2DSurfaceGeometry BuildInkGeometry(in TextStudioFrame frame, in TextStudioVisualSnapshot snapshot, TMP_Text text,
            in TextStudio2DDecorationLineSpec line, in DecorationRun run, Vector4 expansion, bool pixelLine, bool hasLiveInk,
            TextStudioPreparedFieldDemand sourceDemand, TextStudioTMPFrameApplier frameApplier, out string warning)
        {
            warning = string.Empty;
            try
            {
                float bottom = run.Bottom - expansion.y, top = run.Top + expansion.w;
                float bandMargin = InkBandMarginEm * run.EmScale, clip = InkClipMarginEm * run.EmScale;
                Vector2 band = new Vector2(bottom - bandMargin, top + bandMargin);
                // Pixel-sized lines size their band per camera, so only em-sized static masks clip to their tiles.
                Rect? samples = pixelLine ? (Rect?)null :
                    Rect.MinMaxRect(run.Left - expansion.x - clip, bottom - clip, run.Right + expansion.z + clip, top + clip);
                CollectNativeInk(in frame, in snapshot, text, in line, in run, hasLiveInk, false, sourceDemand, frameApplier);
                TextStudio2DSurfaceGeometry geometry = hasLiveInk ? ComposeLiveInk() : ComposeStaticInk(_inkShapes, band, samples);
                if (geometry != null) return geometry;
                CollectNativeInk(in frame, in snapshot, text, in line, in run, hasLiveInk, true, sourceDemand, frameApplier);
                geometry = hasLiveInk ? ComposeLiveInk() : ComposeStaticInk(_inkShapes, band, null);
                warning = geometry != null ? InkBudgetMetricWarning : InkBudgetExceededWarning;
                return geometry;
            }
            finally
            {
                _inkShapes.Clear();
                _nativeInkSources.Clear();
            }
        }

        /// <summary>
        /// The union of static ink with the edges that meet <paramref name="band"/> first. When that union exceeds the edge
        /// budget, its part within <paramref name="samples"/>, which is unchanged within the rectangle's margin around the
        /// line. Null when even that part exceeds the budget, or no rectangle applies.
        /// </summary>
        internal static TextStudio2DSurfaceGeometry ComposeStaticInk(IReadOnlyList<Vector2[]> shapes, Vector2 band, Rect? samples)
        {
            Vector2[][] union = PolygonGeometry.Union(shapes);
            if (TextStudio2DSurfaceGeometry.CountEdges(union) > TextStudio2DSurfaceGeometry.MaximumEdges)
            {
                if (!samples.HasValue) return null;
                union = PolygonGeometry.Intersect(union, new[] { InkRectangle(samples.Value) });
                if (TextStudio2DSurfaceGeometry.CountEdges(union) > TextStudio2DSurfaceGeometry.MaximumEdges) return null;
            }
            return new TextStudio2DSurfaceGeometry(union, inkBand: band);
        }

        // Live Writing ink keeps each glyph's canonical edges in place for its native source records.
        TextStudio2DSurfaceGeometry ComposeLiveInk() =>
            TextStudio2DSurfaceGeometry.CountEdges(_inkShapes) <= TextStudio2DSurfaceGeometry.MaximumEdges
                ? new TextStudio2DSurfaceGeometry(_inkShapes, nativeInkSources: _nativeInkSources)
                : null;

        // Collects the visible native ink of the run's line: prepared outlines where available, and metric glyph bounds for
        // Auto lines, for glyphs without outlines, or for every glyph when metricOnly is set. Posed native ink is sampled after
        // the line's own sizing and glyph transform in both unit modes.
        void CollectNativeInk(in TextStudioFrame frame, in TextStudioVisualSnapshot snapshot, TMP_Text text,
            in TextStudio2DDecorationLineSpec line, in DecorationRun run, bool hasLiveInk, bool metricOnly,
            TextStudioPreparedFieldDemand sourceDemand, TextStudioTMPFrameApplier frameApplier)
        {
            _inkShapes.Clear();
            _nativeInkSources.Clear();
            int edgeOffset = 0;
            for (int index = 0; index < snapshot.RenderGlyphCount; index++)
            {
                ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(index);
                float em = glyph.LocalEmSize > 0 ? glyph.LocalEmSize : frame.GetEffectUnitScale(glyph.TextUnitIndex);
                if (!IsNativeInkVisible(in frame, in glyph) || glyph.LineIndex != run.LineIndex) continue;
                bool posed = TextStudio2DTMPGlyphPlane.TryCreate(text, in glyph, out TextStudio2DTMPGlyphPlane plane, frameApplier, sourceDemand);
                if (posed && !plane.HasVisibleSource) continue;
                int firstEdge = edgeOffset;
                bool prepared = !metricOnly && glyph.SourceKind == TextStudioVisualSourceKind.FontGlyph &&
                    snapshot.TryGetFontSource(glyph.FontIdentity, out TextStudioVisualFontSource source);
                if (prepared)
                {
                    snapshot.TryGetFontSource(glyph.FontIdentity, out source);
                    ICurveFontFace face = source.AppearanceData.GetCurveSource();
                    if (face != null && glyph.GlyphIndex < face.GlyphCount)
                    {
                        float nativeOffset = posed ? plane.NativeFaceOffsetEm : 0;
                        float tolerance = ResolveInkTolerance(sourceDemand, glyph.FontIdentity);
                        bool clipWriting = hasLiveInk && posed;
                        // Positive round dilations compose exactly. Combine them before tessellation to avoid doubling tiny edges.
                        // Erosion and Writing clipping must still precede the line's outward clearance.
                        bool combineDilation = !clipWriting && nativeOffset >= 0;
                        MeasuredContourSet measured = source.AppearanceData.GetContours(glyph.GlyphIndex, tolerance, combineDilation ? 0 : nativeOffset);
                        Vector2[][] contours = new Vector2[measured.Count][];
                        for (int contour = 0; contour < contours.Length; contour++) contours[contour] = measured.GetContour(contour).ToArray();
                        float clearance = line.InkClearance + (combineDilation ? nativeOffset : 0);
                        if (!clipWriting && clearance > 0) contours = PolygonGeometry.Offset(contours, clearance, tolerance);
                        foreach (Vector2[] contour in contours)
                        {
                            if (!hasLiveInk || !posed)
                                for (int point = 0; point < contour.Length; point++)
                                    contour[point] = posed ? (Vector2)plane.Position(contour[point]) : glyph.Origin + contour[point] * em;
                            _inkShapes.Add(contour);
                            edgeOffset += contour.Length;
                        }
                        if (hasLiveInk) AddNativeInkSource(firstEdge, edgeOffset, posed, in plane, line.InkClearance);
                        continue;
                    }
                }
                if (line.SkipInk == TextStudio2DSkipInk.Auto || metricOnly)
                {
                    Vector2[] metricShape = ResolveInkMetricShape(text, in glyph, (hasLiveInk && posed ? 0 : line.InkClearance) + (posed ? plane.NativeFaceOffsetEm : 0), em, hasLiveInk && posed);
                    _inkShapes.Add(metricShape);
                    edgeOffset += metricShape.Length;
                    if (hasLiveInk) AddNativeInkSource(firstEdge, edgeOffset, posed, in plane, line.InkClearance);
                }
                else CompatibilityWarning = "Precise skip ink is unavailable for one of this line's glyph sources. Prepare its font appearance data.";
            }
        }

        static bool IsNativeInkVisible(in TextStudioFrame frame, in TextStudioVisualGlyph glyph)
        {
            if (!glyph.IsVisible) return false;
            int unit = glyph.TextUnitIndex;
            return !frame.Animated.IsCreated || unit < 0 || unit >= frame.AnimatedCount || frame.Animated[unit].alpha > 0;
        }

        void AddNativeInkSource(int firstEdge, int endEdge, bool posed, in TextStudio2DTMPGlyphPlane plane, float clearanceEm)
        {
            if (endEdge <= firstEdge) return;
            _nativeInkSources.Add(posed ? new TextStudio2DNativeInkSource(firstEdge, endEdge - firstEdge, in plane, clearanceEm)
                : new TextStudio2DNativeInkSource(firstEdge, endEdge - firstEdge));
        }

        static float ResolveInkTolerance(TextStudioPreparedFieldDemand demand, ulong fontIdentity) =>
            demand != null && demand.TryGetFont(fontIdentity, out TextStudioPreparedFieldDemand.Font font) ? font.ContourPrecision.Tolerance : .0005f;

        static Vector2[] ResolveInkMetricShape(TMP_Text text, in TextStudioVisualGlyph glyph, float clearanceEm, float em, bool canonical = false)
        {
            if (text != null && text.textInfo != null && glyph.VisualIndex >= 0 && glyph.VisualIndex < text.textInfo.characterCount)
            {
                ref readonly TMP_CharacterInfo character = ref text.textInfo.characterInfo[glyph.VisualIndex];
                if (character.textElement?.glyph != null && character.fontAsset != null && character.fontAsset.faceInfo.pointSize > 0 &&
                    TextStudio2DTMPGlyphPlane.TryCreate(text, in glyph, out TextStudio2DTMPGlyphPlane plane))
                {
                    GlyphMetrics metrics = character.textElement.glyph.metrics;
                    float density = character.fontAsset.faceInfo.pointSize;
                    Vector2[] points = InkRectangle(new Rect(metrics.horizontalBearingX / density - clearanceEm,
                        (metrics.horizontalBearingY - metrics.height) / density - clearanceEm,
                        metrics.width / density + 2 * clearanceEm, metrics.height / density + 2 * clearanceEm));
                    if (!canonical)
                        for (int point = 0; point < points.Length; point++) points[point] = plane.Position(points[point]);
                    return points;
                }
                TMP_MeshInfo[] meshes = text.textInfo.meshInfo;
                int material = character.materialReferenceIndex, vertex = character.vertexIndex;
                if (meshes != null && material >= 0 && material < meshes.Length && vertex >= 0 &&
                    meshes[material].vertices != null && vertex + 3 < meshes[material].vertices.Length)
                {
                    Vector3[] vertices = meshes[material].vertices;
                    Vector2[][] quad = { new[] { (Vector2)vertices[vertex], (Vector2)vertices[vertex + 3],
                        (Vector2)vertices[vertex + 2], (Vector2)vertices[vertex + 1] } };
                    if (clearanceEm > 0) quad = PolygonGeometry.Offset(quad, clearanceEm * em);
                    if (quad.Length > 0) return quad[0];
                }
            }
            float clearance = clearanceEm * em;
            return InkRectangle(Rect.MinMaxRect(glyph.BoundsMin.x - clearance, glyph.BoundsMin.y - clearance,
                glyph.BoundsMax.x + clearance, glyph.BoundsMax.y + clearance));
        }

        static Vector2[] InkRectangle(Rect bounds) => bounds.width <= 0 || bounds.height <= 0 ? Array.Empty<Vector2>() : new[] { new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMax, bounds.yMin),
            new Vector2(bounds.xMax, bounds.yMax), new Vector2(bounds.xMin, bounds.yMax) };
    }
}
