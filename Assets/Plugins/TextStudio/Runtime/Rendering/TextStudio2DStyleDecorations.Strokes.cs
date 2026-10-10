using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    internal sealed partial class TextStudio2DStyleDecorations
    {
        readonly Dictionary<TextStudio2DSurfaceGeometry, StrokeSurface> _strokeSurfaces = new Dictionary<TextStudio2DSurfaceGeometry, StrokeSurface>();
        readonly List<TextStudio2DSurfaceGeometry> _unusedStrokes = new List<TextStudio2DSurfaceGeometry>();
        internal int DebugStrokeSurfaceCount => _strokeSurfaces.Count;
        internal string CompatibilityWarning { get; private set; } = string.Empty;

        internal static bool UsesContourStroke(in TextStudio2DCompiledOperation operation) =>
            operation.Kind == TextStudio2DLayerKind.Stroke && operation.SurfaceOwnerOperationIndex < 0 &&
            (operation.Stroke.Join != TextStudio2DStrokeJoin.Round || operation.Stroke.DashCount > 0);

        void BeginStrokes()
        {
            CompatibilityWarning = string.Empty;
            foreach (StrokeSurface surface in _strokeSurfaces.Values) surface.Used = false;
        }

        void ReleaseUnusedStrokes()
        {
            _unusedStrokes.Clear();
            foreach (KeyValuePair<TextStudio2DSurfaceGeometry, StrokeSurface> pair in _strokeSurfaces)
                if (!pair.Value.Used) _unusedStrokes.Add(pair.Key);
            foreach (TextStudio2DSurfaceGeometry geometry in _unusedStrokes)
            {
                StrokeSurface surface = _strokeSurfaces[geometry];
                TextStudio2DSurfaceTable.Shared.Release(ref surface.Handle);
                _strokeSurfaces.Remove(geometry);
            }
            _unusedStrokes.Clear();
        }

        void EmitSelectedStrokeGlyphs(in TextStudioFrame frame, in TextStudioVisualSnapshot snapshot,
            TMP_Text text, in TextStudio2DCompiledOperation operation, int operationIndex, int styleSlot,
            TextStudioPreparedFieldDemand sourceDemand, TextStudioTMPFrameApplier frameApplier)
        {
            TextStudio2DStrokeSpec stroke = operation.Stroke;
            if (stroke.Width <= 0) return;
            StyleBatch target = GetDecorationBatch(_batches, ref _activeBatches);
            if (target == null) return;
            IReadOnlyList<int> renderGlyphs = ScopedRenderGlyphs(in frame);
            int renderCount = renderGlyphs?.Count ?? snapshot.RenderGlyphCount;
            for (int row = 0; row < renderCount; row++)
            {
                int index = renderGlyphs != null ? renderGlyphs[row] : row;
                ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(index);
                if (!IsSelected(glyph.TextUnitIndex) || !glyph.IsVisible || glyph.SourceKind != TextStudioVisualSourceKind.FontGlyph ||
                    !snapshot.TryGetFontSource(glyph.FontIdentity, out TextStudioVisualFontSource source)) continue;
                if (!TextStudio2DTMPGlyphPlane.TryCreate(text, in glyph, out TextStudio2DTMPGlyphPlane plane, frameApplier, sourceDemand)) continue;
                float tolerance = .0005f;
                if (sourceDemand != null && sourceDemand.TryGetFont(glyph.FontIdentity, out TextStudioPreparedFieldDemand.Font font))
                    tolerance = font.ContourPrecision.Tolerance;
                TextStudio2DSurfaceGeometry geometry;
                try
                {
                    TextStudioPixelContourProjection? projection = null;
                    if (stroke.Units == TextStudio2DUnit.Pixels)
                    {
                        if (sourceDemand == null) { CompatibilityWarning = "Pixel contour strokes need an output camera."; continue; }
                        if (!sourceDemand.TryGetPixelContourProjection(in plane, out TextStudioPixelContourProjection projected, out string warning))
                        { CompatibilityWarning = warning; continue; }
                        projection = projected;
                    }
                    geometry = source.AppearanceData.GetStroke(glyph.GlyphIndex, in stroke, 1, tolerance, projection, plane.NativeFaceOffsetEm);
                    if (geometry != null && geometry.PixelProjection.HasValue)
                        geometry.PixelProjection.Value.UnprojectBounds(geometry.MetricBounds, stroke.Softness + 2);
                }
                catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException || exception is NotSupportedException)
                {
                    CompatibilityWarning = "The glyph outline could not be prepared: " + exception.Message;
                    continue;
                }
                if (geometry == null || geometry.Edges.Length == 0) continue;
                if (!_strokeSurfaces.TryGetValue(geometry, out StrokeSurface surface))
                {
                    if (_strokeSurfaces.Count >= 256)
                    {
                        CompatibilityWarning = "This text exceeds the 256 distinct stroke-shape budget. Reduce the outline detail or split the text.";
                        break;
                    }
                    surface = new StrokeSurface();
                    _strokeSurfaces.Add(geometry, surface);
                }
                surface.Used = true;
                if (!TextStudio2DSurfaceTable.Shared.TryUpdate(ref surface.Handle, geometry, Vector4.zero))
                {
                    CompatibilityWarning = TextStudio2DSurfaceTable.Shared.MemoryBudgetExceeded
                        ? TextStudioAppearanceMemory.LastRejection.ToString()
                        : "The shared contour geometry pool is full. Reduce the active outline detail.";
                    continue;
                }
                EmitStrokeQuad(target.Geometry, geometry, surface.Handle, in frame, in snapshot, in glyph, index,
                    in plane, in stroke, tolerance, operationIndex, styleSlot);
            }
        }

        static void EmitStrokeQuad(GeometryBuffer target, TextStudio2DSurfaceGeometry geometry, TextStudio2DSurfaceHandle handle,
            in TextStudioFrame frame, in TextStudioVisualSnapshot snapshot, in TextStudioVisualGlyph glyph,
            int glyphIndex, in TextStudio2DTMPGlyphPlane plane, in TextStudio2DStrokeSpec stroke, float tolerance, int operationIndex, int styleSlot)
        {
            float em = glyph.LocalEmSize > 0 ? glyph.LocalEmSize : frame.GetEffectUnitScale(glyph.TextUnitIndex);
            Vector4 bounds = geometry.Bounds;
            float width = Mathf.Max(.000001f, bounds.z - bounds.x), height = Mathf.Max(.000001f, bounds.w - bounds.y);
            float guard = stroke.Softness + tolerance * 10;
            float left = bounds.x - guard, right = bounds.z + guard, bottom = bounds.y - guard, top = bounds.w + guard;
            if (geometry.PixelProjection.HasValue)
            {
                Vector4 expanded = geometry.PixelProjection.Value.UnprojectBounds(geometry.MetricBounds, stroke.Softness + 2);
                left = expanded.x; bottom = expanded.y; right = expanded.z; top = expanded.w;
            }
            Vector2 origin = glyph.Origin;
            Vector2 a = new Vector2(left, bottom), b = new Vector2(right, bottom), c = new Vector2(left, top), d = new Vector2(right, top);
            TextStudio2DTMPGlyphPlane nativePlane = plane;
            Vector4 Uv(Vector2 point)
            {
                Vector2 local = nativePlane.NativeCoordinate(point);
                return new Vector4((point.x - bounds.x) / width, (point.y - bounds.y) / height, local.x, local.y);
            }
            int firstVertex = target.Vertices.Count;
            target.EmitQuad(plane.Position(a), plane.Position(b), plane.Position(c), plane.Position(d), plane.SampleColor(a),
                Uv(a), Uv(b), Uv(c), Uv(d),
                Program(origin.x + a.x * em, origin.y + a.y * em, in snapshot, styleSlot, operationIndex),
                Program(origin.x + b.x * em, origin.y + b.y * em, in snapshot, styleSlot, operationIndex),
                Program(origin.x + c.x * em, origin.y + c.y * em, in snapshot, styleSlot, operationIndex),
                Program(origin.x + d.x * em, origin.y + d.y * em, in snapshot, styleSlot, operationIndex),
                new Vector4(width, height, stroke.Softness, MappingGlyphIndex(in frame, glyphIndex) + 1), new Vector4(handle.Slot, handle.Generation, plane.WritingSample, 0));
            target.Colors[firstVertex + 1] = plane.SampleColor(b);
            target.Colors[firstVertex + 2] = plane.SampleColor(c);
            target.Colors[firstVertex + 3] = plane.SampleColor(d);
        }

        sealed class StrokeSurface
        {
            internal TextStudio2DSurfaceHandle Handle;
            internal bool Used;
        }
    }
}
