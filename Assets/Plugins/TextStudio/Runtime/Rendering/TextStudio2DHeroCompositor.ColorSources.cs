using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    internal sealed partial class TextStudio2DHeroCompositor
    {
        struct ColorBinding
        {
            internal TextStudioColorSource Source;
            internal uint ContentRevision;
            internal TextStudioColorFieldCache.Entry Field;
            internal Texture2D PublishedTexture;
        }

        internal string ColorSourceCompatibilityWarning
        {
            get => AppearanceColorDiagnostic(false);
            private set => _occurrenceState.ColorCompatibilityWarning = value;
        }
        internal string ColorSourceQualityHint
        {
            get => AppearanceColorDiagnostic(true);
            private set => _occurrenceState.ColorQualityHint = value;
        }

        internal bool NeedsColorSourceRefresh
        {
            get
            {
                if (_appearancePlan == null) return ColorSourceNeedsRefresh(_occurrenceState);
                foreach (OccurrenceState state in _activeAppearanceStates) if (ColorSourceNeedsRefresh(state)) return true;
                return false;
            }
        }

        static bool ColorSourceNeedsRefresh(OccurrenceState state)
        {
            for (int index = 0; index < state.ColorBindingCount; index++)
            {
                ColorBinding binding = state.ColorBindings[index];
                if (binding.Source == null) continue;
                if (binding.Source.Texture == null || binding.Source.Texture.updateCount != binding.ContentRevision) return true;
                TextStudioColorFieldCache.Entry field = binding.Field;
                if (field == null) continue;
                if (field.Texture != binding.PublishedTexture || !field.Running && field.NeedsGeneration && field.Owner.RunningJobs < 2) return true;
            }
            return false;
        }

        string AppearanceColorDiagnostic(bool quality)
        {
            if (_appearancePlan == null) return quality ? _occurrenceState.ColorQualityHint : _occurrenceState.ColorCompatibilityWarning;
            foreach (OccurrenceState state in _activeAppearanceStates)
            {
                string diagnostic = quality ? state.ColorQualityHint : state.ColorCompatibilityWarning;
                if (!string.IsNullOrEmpty(diagnostic)) return diagnostic;
            }
            return string.Empty;
        }

        bool UpdateColorSources(in TextStudioFrame frame, CompiledTextStudio2DStyle style)
        {
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            IReadOnlyList<int> occurrenceGlyphs = OccurrenceRenderGlyphs(in frame);
            int sourceCount = occurrenceGlyphs?.Count ?? snapshot.RenderGlyphCount;
            _occurrenceState.ColorBindingIndices.Clear();
            _occurrenceState.ColorSourceIndices.Clear();
            if (occurrenceGlyphs == null)
            {
                for (int index = 0; index < snapshot.ColorSourceCount; index++)
                {
                    _occurrenceState.ColorBindingIndices.Add(index, index);
                    _occurrenceState.ColorSourceIndices.Add(index);
                }
            }
            else
            {
                for (int source = 0; source < sourceCount; source++)
                {
                    ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(occurrenceGlyphs[source]);
                    if (!glyph.IsVisible || glyph.SourceKind == TextStudioVisualSourceKind.FontGlyph ||
                        (uint)glyph.ColorSourceIndex >= (uint)snapshot.ColorSourceCount ||
                        !frame.AppearanceBinding.Contains(frame.AppearanceOccurrenceIndex, glyph.TextUnitIndex) ||
                        _occurrenceState.ColorBindingIndices.ContainsKey(glyph.ColorSourceIndex)) continue;
                    _occurrenceState.ColorBindingIndices.Add(glyph.ColorSourceIndex, _occurrenceState.ColorSourceIndices.Count);
                    _occurrenceState.ColorSourceIndices.Add(glyph.ColorSourceIndex);
                }
            }
            int count = _occurrenceState.ColorSourceIndices.Count;
            if (count == 0)
            {
                bool hadSources = _occurrenceState.ColorBindingCount > 0;
                ReleaseColorSources();
                return hadSources;
            }
            if (_occurrenceState.NextColorBindings.Length < count) Array.Resize(ref _occurrenceState.NextColorBindings, TextStudioBufferCapacity.ForCount(count));
            bool changed = count != _occurrenceState.ColorBindingCount;
            bool preparing = false, densityLimited = false;
            ColorSourceCompatibilityWarning = string.Empty;
            TextStudioColorFieldCache cache = TextStudioColorFieldCache.Shared;
            for (int index = 0; index < count; index++)
            {
                int sourceIndex = _occurrenceState.ColorSourceIndices[index];
                TextStudioColorSource source = snapshot.ColorSources[sourceIndex];
                if (source == null || source.Texture == null)
                {
                    changed |= index < _occurrenceState.ColorBindingCount && _occurrenceState.ColorBindings[index].Source != null;
                    _occurrenceState.NextColorBindings[index] = default;
                    continue;
                }
                float density = 16, padding = 0;
                bool needsField = false;
                for (int glyphCursor = 0; glyphCursor < sourceCount; glyphCursor++)
                {
                    int glyphIndex = occurrenceGlyphs == null ? glyphCursor : occurrenceGlyphs[glyphCursor];
                    ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(glyphIndex);
                    if (!glyph.IsVisible || glyph.ColorSourceIndex != sourceIndex || glyph.SourceKind == TextStudioVisualSourceKind.FontGlyph ||
                        occurrenceGlyphs != null && !frame.AppearanceBinding.Contains(frame.AppearanceOccurrenceIndex, glyph.TextUnitIndex)) continue;
                    density = Mathf.Max(Mathf.Max(density, ResolveColorDensity(source)), Mathf.Max(_projectedPixelsPerUnit * glyph.LocalEmSize, style.SourceFieldRequest.MinimumPixelsPerEm));
                    for (int operationIndex = 0; operationIndex < style.OperationCount; operationIndex++)
                    {
                        ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(operationIndex);
                        if (operation.SurfaceOwnerOperationIndex >= 0 || !OperationApplies(style, in frame, operationIndex, glyph.TextUnitIndex, glyph.SourceKind)) continue;
                        if (operation.Kind == TextStudio2DLayerKind.Stroke && (operation.Stroke.Join != TextStudio2DStrokeJoin.Round || operation.Stroke.DashCount > 0))
                        { ColorSourceCompatibilityWarning = "Exact joins and contour dashes need vector contours; this color image supplies a scalar silhouette."; continue; }
                        bool field = operation.Kind != TextStudio2DLayerKind.Group && operation.Kind != TextStudio2DLayerKind.Decoration &&
                            operation.Kind != TextStudio2DLayerKind.ColorAdjustment &&
                            (operation.Kind != TextStudio2DLayerKind.Fill || operation.Fill.ColorFace == TextStudio2DColorFaceMode.SilhouetteFill);
                        needsField |= field;
                        if (!field) continue;
                        Vector4 bounds = operation.BoundsExpansion.Vector;
                        float extent = Mathf.Max(Mathf.Max(bounds.x, bounds.y), Mathf.Max(bounds.z, bounds.w));
                        if (TextStudio2DFieldPadding.UsesPixelUnits(in operation)) extent /= Mathf.Max(.0001f,
                            _projectedPixelsPerUnit > 0 ? _projectedPixelsPerUnit * glyph.LocalEmSize : ResolveColorDensity(source));
                        padding = Mathf.Max(padding, extent);
                    }
                }
                string error = string.Empty;
                TextStudioColorFieldCache.Entry lease = needsField ? cache.Acquire(source, density, padding, out error, _hub.FindAppearanceImage(source)) : null;
                if (needsField && lease == null) ColorSourceCompatibilityWarning = error;
                bool ready = lease != null && cache.TryGet(lease);
                if (lease != null && lease.Error.Length > 0) ColorSourceCompatibilityWarning = lease.Error;
                preparing |= lease != null && lease.Error.Length == 0 && (!ready || lease.Running || lease.NeedsGeneration);
                densityLimited |= ready && lease.Density < density * .9f;
                ColorBinding binding = new ColorBinding { Source = source, ContentRevision = source.Texture.updateCount, Field = lease, PublishedTexture = ready ? lease.Texture : null };
                ColorBinding previous = index < _occurrenceState.ColorBindingCount ? _occurrenceState.ColorBindings[index] : default;
                changed |= previous.Source?.Identity != source.Identity || previous.ContentRevision != binding.ContentRevision || previous.PublishedTexture != binding.PublishedTexture;
                _occurrenceState.NextColorBindings[index] = binding;
            }
            // Acquire the whole new source set before releasing the old set so reordering never destroys a still-used image field.
            for (int index = 0; index < _occurrenceState.ColorBindingCount; index++)
            {
                TextStudioColorFieldCache.Entry lease = _occurrenceState.ColorBindings[index].Field;
                lease?.Owner.Release(ref lease);
                _occurrenceState.ColorBindings[index] = default;
            }
            ColorBinding[] old = _occurrenceState.ColorBindings;
            _occurrenceState.ColorBindings = _occurrenceState.NextColorBindings;
            _occurrenceState.NextColorBindings = old;
            _occurrenceState.ColorBindingCount = count;
            ColorSourceQualityHint = preparing ? "Preparing inline image silhouettes. Original image faces remain available."
                : densityLimited ? "Inline image silhouette detail is limited by the 1024-pixel source tile budget." : string.Empty;
            return changed;
        }

        internal static float ResolveColorDensity(TextStudioColorSource source)
        {
            if (source.Positions.Length < 4) return 64;
            Vector2 textureSize = new Vector2(source.Texture.width, source.Texture.height);
            Vector2 horizontal = Vector2.Scale(source.Uv[3] - source.Uv[0], textureSize);
            Vector2 vertical = Vector2.Scale(source.Uv[1] - source.Uv[0], textureSize);
            return Mathf.Max(horizontal.magnitude / Mathf.Max(.0001f, (source.Positions[3] - source.Positions[0]).magnitude),
                vertical.magnitude / Mathf.Max(.0001f, (source.Positions[1] - source.Positions[0]).magnitude));
        }

        bool EmitColorGlyph(in TextStudioFrame frame, in TextStudioVisualSnapshot snapshot, in TextStudioVisualGlyph glyph, int glyphIndex,
            in TMP_MeshInfo mesh, int materialIndex, int start, Material material, TextStudioTMPFrameApplier frameApplier,
            ref Vector2 minimum, ref Vector2 maximum)
        {
            TextStudioColorSource source = snapshot.GetColorSource(in glyph);
            if (source == null || source.IsEmpty || source.Positions.Length != 4 || material == null) return false;
            LayerBatch native = GetBatch(material, source.Texture, ResolveColorDensity(source), source.PremultipliedAlpha ? 2 : 1);
            if (native == null) return false;
            int mappingGlyphIndex = frame.AppearanceOccurrenceIndex >= 0 && frame.AppearanceBinding != null
                ? frame.AppearanceBinding.GetMappingGlyphIndex(frame.AppearanceOccurrenceIndex, glyphIndex) : glyphIndex;
            Vector4 appearanceBounds = frame.AppearanceOccurrenceIndex >= 0 && frame.AppearanceBinding != null
                ? frame.AppearanceBinding.GetDomainBounds(frame.AppearanceOccurrenceIndex) : snapshot.SourceBounds;
            Vector4 nativeSelectors = BuildSelectorChunks(frame.AppearanceStyle, in frame, glyph.TextUnitIndex, glyph.SourceKind, true);
            EmitColorQuad(native, source, null, in glyph, mappingGlyphIndex, appearanceBounds, in mesh, materialIndex, start, frameApplier, nativeSelectors, ref minimum, ref maximum);
            if (_occurrenceState.ColorBindingIndices.TryGetValue(glyph.ColorSourceIndex, out int bindingIndex))
            {
                ColorBinding binding = _occurrenceState.ColorBindings[bindingIndex];
                if (binding.PublishedTexture != null)
                {
                    TextStudioColorFieldCache.Entry field = binding.Field;
                    LayerBatch silhouette = GetBatch(material, binding.PublishedTexture, field.Density, 3, field.DistanceRange);
                    if (silhouette != null)
                    {
                        Vector4 fieldSelectors = BuildSelectorChunks(frame.AppearanceStyle, in frame, glyph.TextUnitIndex, glyph.SourceKind);
                        EmitColorQuad(silhouette, source, field, in glyph, mappingGlyphIndex, appearanceBounds, in mesh, materialIndex, start,
                            frameApplier, fieldSelectors, ref minimum, ref maximum);
                    }
                }
            }
            return true;
        }

        static void EmitColorQuad(LayerBatch batch, TextStudioColorSource source, TextStudioColorFieldCache.Entry field,
            in TextStudioVisualGlyph glyph, int glyphIndex, Vector4 appearanceBounds, in TMP_MeshInfo mesh,
            int materialIndex, int start, TextStudioTMPFrameApplier frameApplier, Vector4 selectors, ref Vector2 minimum, ref Vector2 maximum)
        {
            Vector2 origin = source.Positions[0];
            Vector2 axisX = source.Positions[3] - origin, axisY = source.Positions[1] - origin;
            float determinant = axisX.x * axisY.y - axisX.y * axisY.x;
            if (Mathf.Abs(determinant) < .000001f) return;
            Rect bounds = field != null ? field.Bounds : source.Bounds;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 em = field == null ? source.Positions[corner] : new Vector2(corner < 2 ? bounds.xMin : bounds.xMax, corner == 0 || corner == 3 ? bounds.yMin : bounds.yMax);
                Vector2 relative = em - origin;
                float x = (relative.x * axisY.y - relative.y * axisY.x) / determinant;
                float y = (axisX.x * relative.y - axisX.y * relative.x) / determinant;
                Vector3 position = TextStudio2DPrimitiveSupport.Bilinear(mesh.vertices[start], mesh.vertices[start + 1], mesh.vertices[start + 2], mesh.vertices[start + 3], x, y);
                Vector2 uv = field == null ? source.Uv[corner] : new Vector2((em.x - bounds.xMin) / bounds.width, (em.y - bounds.yMin) / bounds.height);
                Color left = Color.LerpUnclamped(mesh.colors32[start], mesh.colors32[start + 1], y);
                Color right = Color.LerpUnclamped(mesh.colors32[start + 3], mesh.colors32[start + 2], y);
                Color32 color = Color.LerpUnclamped(left, right, x);
                frameApplier.TryGetMaterialPayload(materialIndex, start + corner, out Vector4 payload0, out Vector4 payload1, out Vector4 local);
                local.x = x; local.y = y; local.z = PackGlyphAndPixelPayload(local.z, glyphIndex);
                float writing = frameApplier.GetSourceCoordinateNormal(materialIndex, start + corner).z;
                Vector2 sourcePosition = new Vector2(Mathf.LerpUnclamped(glyph.BoundsMin.x, glyph.BoundsMax.x, x), Mathf.LerpUnclamped(glyph.BoundsMin.y, glyph.BoundsMax.y, y));
                Vector2 textCoordinate = NormalizeSourcePosition(sourcePosition, appearanceBounds);
                batch.EmitVertex(position, new Vector3(textCoordinate.x, textCoordinate.y, writing), color, new Vector4(uv.x, uv.y, 0, 1),
                    Vector2.zero, payload0, payload1, selectors, local);
                minimum = Vector2.Min(minimum, position); maximum = Vector2.Max(maximum, position);
            }
            batch.EmitTriangles();
        }

        void ReleaseColorSources()
        {
            for (int index = 0; index < _occurrenceState.ColorBindingCount; index++)
            {
                TextStudioColorFieldCache.Entry lease = _occurrenceState.ColorBindings[index].Field;
                lease?.Owner.Release(ref lease);
                _occurrenceState.ColorBindings[index] = default;
            }
            _occurrenceState.ColorBindingCount = 0;
            ColorSourceCompatibilityWarning = string.Empty;
            ColorSourceQualityHint = string.Empty;
        }
    }
}
