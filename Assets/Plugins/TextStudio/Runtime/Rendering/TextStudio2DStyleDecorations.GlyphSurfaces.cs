using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    internal sealed partial class TextStudio2DStyleDecorations
    {
        readonly List<GlyphSurfaceInput> _glyphSurfaceInputs = new List<GlyphSurfaceInput>();
        readonly List<Vector2[]> _glyphSurfaceContours = new List<Vector2[]>();

        void EmitGlyphSurfaces(GeometryBuffer target, in TextStudioFrame frame, in TextStudioVisualSnapshot snapshot,
            TMP_Text text, in TextStudio2DCompiledOperation operation, int operationIndex, int styleSlot,
            TextStudioPreparedFieldDemand demand, TextStudioTMPFrameApplier applier)
        {
            try
            {
                TextStudio2DResolvedSelection selection = _binding.GetSelection(in frame, operation.SelectorIndex);
                TextStudio2DDecorationSpec decoration = operation.Decoration;
                TextStudioSurfaceGroup group = text.GetComponent<TextStudioSurfaceGroup>();
                if (group != null && !group.isActiveAndEnabled) group = null;
                for (int match = 0; match < selection.MatchCount; match++)
                {
                    _glyphSurfaceInputs.Clear(); _surfaceMembers.Clear();
                    TextStudioDeterministicHash64 hash = default;
                    hash.Add(decoration.GlyphPadding); hash.Add(decoration.GlyphBridgeRadius); hash.Add(decoration.GlyphFillHoles);
                    bool follow = decoration.BackplateVisibility == TextStudio2DBackplateVisibility.FollowTypewriter;
                    hash.Add(follow);
                    float opacity = follow ? group?.MaximumSourceAlpha() ?? 0 : 1;
                    float em = 0;
                    IReadOnlyList<int> renderGlyphs = ScopedRenderGlyphs(in frame);
                    int renderCount = renderGlyphs?.Count ?? snapshot.RenderGlyphCount;
                    for (int row = 0; row < renderCount; row++)
                    {
                        int index = renderGlyphs != null ? renderGlyphs[row] : row;
                        ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(index);
                        if (!IsSelected(glyph.TextUnitIndex) || selection.GetOwner(glyph.TextUnitIndex) != match || !glyph.IsVisible) continue;
                        if (glyph.SourceKind != TextStudioVisualSourceKind.FontGlyph ||
                            !snapshot.TryGetFontSource(glyph.FontIdentity, out TextStudioVisualFontSource source))
                            throw new NotSupportedException("Glyph Silhouette requires prepared font contours for every selected glyph. Inline images are not supported.");
                        if (!TextStudio2DTMPGlyphPlane.TryCreate(text, in glyph, out TextStudio2DTMPGlyphPlane plane, applier, demand))
                            throw new InvalidOperationException("The selected glyph has not produced a usable silhouette plane.");
                        _surfaceMembers.Add(glyph.TextUnitIndex);
                        em = Mathf.Max(em, glyph.LocalEmSize > 0 ? glyph.LocalEmSize : frame.GetEffectUnitScale(glyph.TextUnitIndex));
                        if (follow && !plane.HasVisibleSource) continue;
                        opacity = Mathf.Max(opacity, plane.MaximumAlpha);
                        float tolerance = ResolveInkTolerance(demand, glyph.FontIdentity);
                        _glyphSurfaceInputs.Add(new GlyphSurfaceInput(source.AppearanceData, glyph.GlyphIndex, plane, tolerance));
                        hash.Add(source.Revision); hash.Add(source.AppearanceData.SourceHash); hash.Add(glyph.GlyphIndex); hash.Add(tolerance);
                        TextStudioGlyphSilhouette.HashPlane(ref hash, in plane);
                    }
                    if (_surfaceMembers.Count == 0 || opacity <= 0) continue;
                    hash.Add(em); hash.Add(group != null);
                    if (group != null) group.AddFingerprint(ref hash);
                    if (_activeSurfaces >= 128) throw new InvalidOperationException("This text exceeds the 128 connected-surface budget.");
                    while (_surfaces.Count <= _activeSurfaces) _surfaces.Add(new ConnectedSurface());
                    ConnectedSurface surface = _surfaces[_activeSurfaces++];
                    if (surface.Geometry == null || surface.InputFingerprint != hash.Value)
                    {
                        _glyphSurfaceContours.Clear();
                        foreach (GlyphSurfaceInput input in _glyphSurfaceInputs)
                            TextStudioGlyphSilhouette.Append(_glyphSurfaceContours, input.Font, input.Glyph, input.Plane, Matrix4x4.identity, input.Tolerance);
                        if (group != null) group.AppendContours(_glyphSurfaceContours, .0005f, follow);
                        surface.Geometry = TextStudioGlyphSilhouette.Build(_glyphSurfaceContours, decoration.GlyphPadding * em,
                            decoration.GlyphBridgeRadius * em, decoration.GlyphFillHoles, Mathf.Max(.00001f, em * .001f));
                        surface.InputFingerprint = hash.Value;
                    }
                    Vector4 mask = _binding.SurfaceAdjustmentMask(in frame, operationIndex, _surfaceMembers);
                    if (!TextStudio2DSurfaceTable.Shared.TryUpdate(ref surface.Handle, surface.Geometry, mask))
                        throw new InvalidOperationException("The glyph-surface geometry pool is full. Reduce outline detail or active surfaces.");
                    // The union is already in the owner's final text plane. Applying placement again would bend it twice.
                    EmitSurfaceQuad(target, surface, text, in frame, in snapshot, in operation, operationIndex, styleSlot, em, alreadyPlaced: true, surfaceOpacity: opacity);
                }
                TextStudio2DSurfaceTable.Shared.FlushAndBind();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is NotSupportedException)
            {
                CompatibilityWarning = "The glyph-derived backing could not be prepared: " + exception.Message;
            }
        }

        readonly struct GlyphSurfaceInput
        {
            internal readonly TextStudioFontAppearanceData Font;
            internal readonly uint Glyph;
            internal readonly TextStudio2DTMPGlyphPlane Plane;
            internal readonly float Tolerance;
            internal GlyphSurfaceInput(TextStudioFontAppearanceData font, uint glyph, TextStudio2DTMPGlyphPlane plane, float tolerance)
            { Font = font; Glyph = glyph; Plane = plane; Tolerance = tolerance; }
        }
    }
}
