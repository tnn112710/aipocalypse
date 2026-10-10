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
        readonly List<Vector2[]> _inkShapes = new List<Vector2[]>();
        readonly List<TextStudio2DNativeInkSource> _nativeInkSources = new List<TextStudio2DNativeInkSource>();

        TextStudio2DSurfaceHandle PrepareSkipInk(in TextStudioFrame frame, in TextStudioVisualSnapshot snapshot,
            TMP_Text text, in TextStudio2DDecorationSpec decoration, in DecorationRun run, Vector4 adjustmentMask, TextStudioPreparedFieldDemand sourceDemand, TextStudioTMPFrameApplier frameApplier)
        {
            TextStudio2DDecorationLineSpec line = decoration.Line;
            if (line.SkipInk == TextStudio2DSkipInk.Off) return default;
            bool pixelLine = line.Units == TextStudio2DUnit.Pixels;
            bool hasLiveInk = false;
            TextStudioDeterministicHash64 hash = default;
            hash.Add(pixelLine); hash.Add(run.Left); hash.Add(run.Right);
            hash.Add((byte)line.SkipInk); hash.Add(line.InkClearance); hash.Add(run.EmScale);
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
                        bool prepared = glyph.SourceKind == TextStudioVisualSourceKind.FontGlyph &&
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
                        if (line.SkipInk == TextStudio2DSkipInk.Auto)
                        {
                            Vector2[] metricShape = ResolveInkMetricShape(text, in glyph, (hasLiveInk && posed ? 0 : line.InkClearance) + (posed ? plane.NativeFaceOffsetEm : 0), em, hasLiveInk && posed);
                            _inkShapes.Add(metricShape);
                            edgeOffset += metricShape.Length;
                            if (hasLiveInk) AddNativeInkSource(firstEdge, edgeOffset, posed, in plane, line.InkClearance);
                        }
                        else CompatibilityWarning = "Precise skip ink is unavailable for one of this line's glyph sources. Prepare its font appearance data.";
                    }
                    // Sample posed native ink after the line's own sizing and glyph transform in both unit modes.
                    surface.Geometry = hasLiveInk
                        ? new TextStudio2DSurfaceGeometry(_inkShapes, nativeInkSources: _nativeInkSources)
                        : new TextStudio2DSurfaceGeometry(PolygonGeometry.Union(_inkShapes));
                    surface.InputFingerprint = hash.Value;
                    _inkShapes.Clear();
                    _nativeInkSources.Clear();
                }
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
