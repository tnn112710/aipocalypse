using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    internal sealed partial class TextStudio2DStyleDecorations
    {
        readonly TextStudio2DBoundStyle _ownedBinding = new TextStudio2DBoundStyle();
        readonly List<TextStudio2DSurfaceBand> _surfaceBands = new List<TextStudio2DSurfaceBand>();
        readonly List<int> _surfaceMembers = new List<int>();
        readonly List<ConnectedSurface> _surfaces = new List<ConnectedSurface>();
        TextStudio2DBoundStyle _binding;
        int _activeSurfaces;

        void ReleaseUnusedSurfaces()
        {
            if (_surfaces.Count == 0) return;
            TextStudio2DSurfaceTable table = TextStudio2DSurfaceTable.Shared;
            for (int index = _surfaces.Count - 1; index >= _activeSurfaces; index--)
            {
                ConnectedSurface surface = _surfaces[index];
                table.Release(ref surface.Handle);
                _surfaces.RemoveAt(index);
            }
            table.FlushAndBind();
        }

        void EmitConnectedSurfaces(GeometryBuffer target, in TextStudioFrame frame, in TextStudioVisualSnapshot snapshot,
            TMP_Text text, in TextStudio2DCompiledOperation operation, int operationIndex, int styleSlot)
        {
            TextStudio2DResolvedSelection selection = _binding.GetSelection(in frame, operation.SelectorIndex);
            TextStudio2DDecorationSpec decoration = operation.Decoration;
            for (int match = 0; match < selection.MatchCount; match++)
            for (int lineFragment = decoration.ConnectAcrossLineBreaks ? -1 : 0; lineFragment < (decoration.ConnectAcrossLineBreaks ? 0 : selection.FragmentCount); lineFragment++)
            {
                int selectedGroup = -1;
                if (lineFragment >= 0)
                {
                    TextStudio2DVisualFragment line = selection.GetFragment(lineFragment);
                    if (line.MatchIndex != match) continue;
                    selectedGroup = SurfaceGroup(in frame, in snapshot, selection, in line);
                    bool alreadyEmitted = false;
                    for (int previous = 0; previous < lineFragment; previous++)
                    {
                        TextStudio2DVisualFragment candidate = selection.GetFragment(previous);
                        if (candidate.MatchIndex == match && SurfaceGroup(in frame, in snapshot, selection, in candidate) == selectedGroup) { alreadyEmitted = true; break; }
                    }
                    if (alreadyEmitted) continue;
                }
                _surfaceBands.Clear();
                _surfaceMembers.Clear();
                float emScale = 0;
                for (int index = 0; index < selection.FragmentCount; index++)
                {
                    TextStudio2DVisualFragment fragment = selection.GetFragment(index);
                    if (fragment.MatchIndex != match) continue;
                    if (selectedGroup >= 0 && SurfaceGroup(in frame, in snapshot, selection, in fragment) != selectedGroup) continue;
                    Vector4 bounds = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity);
                    int logicalStart = int.MaxValue, logicalEnd = 0;
                    float fragmentEm = 0;
                    for (int member = fragment.FirstMember; member < fragment.FirstMember + fragment.MemberCount; member++)
                    {
                        int glyphIndex = selection.GetMember(member);
                        TextStudioVisualGlyph glyph = snapshot.GetGlyph(glyphIndex);
                        if (!IsSelected(glyphIndex)) continue;
                        if (frame.Plan.IsLineBreakUnit(glyph.TextUnitIndex)) continue;
                        GetGlyphHorizontalSpan(in glyph, decoration.Kind, out float left, out float right);
                        ResolveRunVerticalBounds(in frame, in snapshot, text, in decoration, glyphIndex, glyphIndex + 1,
                            out float bottom, out float top, out float glyphEm);
                        fragmentEm = Mathf.Max(fragmentEm, glyphEm);
                        bounds.x = Mathf.Min(bounds.x, left); bounds.y = Mathf.Min(bounds.y, bottom);
                        bounds.z = Mathf.Max(bounds.z, right); bounds.w = Mathf.Max(bounds.w, top);
                        logicalStart = Mathf.Min(logicalStart, glyph.TextUnitIndex);
                        logicalEnd = Mathf.Max(logicalEnd, glyph.TextUnitIndex + 1);
                        _surfaceMembers.Add(glyphIndex);
                    }
                    if (!float.IsFinite(bounds.x)) continue;
                    float bandLeft = bounds.x, bandRight = bounds.z;
                    ResolvePlateHorizontalBounds(in decoration, fragmentEm, ref bandLeft, ref bandRight);
                    bounds.x = bandLeft;
                    bounds.z = bandRight;
                    bounds.x -= Mathf.Max(0, decoration.Padding.x) * fragmentEm;
                    bounds.z += Mathf.Max(0, decoration.Padding.z) * fragmentEm;
                    _surfaceBands.Add(new TextStudio2DSurfaceBand(Rect.MinMaxRect(bounds.x, bounds.y, bounds.z, bounds.w),
                        fragment.LineIndex, logicalStart, logicalEnd));
                    emScale = Mathf.Max(emScale, fragmentEm);
                }
                if (_surfaceBands.Count == 0 || _surfaceMembers.Count == 0) continue;
                if (_activeSurfaces >= 128)
                    throw new InvalidOperationException("This text exceeds the 128 connected-surface budget. Combine compatible ranges or split the text.");
                while (_surfaces.Count <= _activeSurfaces) _surfaces.Add(new ConnectedSurface());
                ConnectedSurface surface = _surfaces[_activeSurfaces++];
                TextStudioDeterministicHash64 hash = default;
                hash.Add(decoration.CornerRadius);
                hash.Add(decoration.ConnectAcrossLineBreaks);
                hash.Add(frame.Plan.SourceVersion);
                hash.Add(emScale);
                foreach (TextStudio2DSurfaceBand band in _surfaceBands)
                {
                    hash.Add(band.Bounds.xMin); hash.Add(band.Bounds.yMin); hash.Add(band.Bounds.xMax); hash.Add(band.Bounds.yMax);
                    hash.Add(band.Line); hash.Add(band.LogicalStart); hash.Add(band.LogicalEnd);
                }
                if (surface.Geometry == null || surface.InputFingerprint != hash.Value)
                {
                    TextStudioPlanView plan = frame.Plan;
                    surface.Geometry = TextStudio2DSurfaceGeometry.Connected(_surfaceBands, in plan,
                        decoration.ConnectAcrossLineBreaks, decoration.CornerRadius * emScale, Mathf.Max(.00001f, emScale * .002f));
                    surface.InputFingerprint = hash.Value;
                }
                Vector4 mask = _binding.SurfaceAdjustmentMask(in frame, operationIndex, _surfaceMembers);
                if (!TextStudio2DSurfaceTable.Shared.TryUpdate(ref surface.Handle, surface.Geometry, mask))
                    throw new InvalidOperationException(TextStudio2DSurfaceTable.Shared.MemoryBudgetExceeded
                        ? TextStudioAppearanceMemory.LastRejection.ToString()
                        : "The connected-surface geometry pool is full. Reduce the number of active surfaces or their detail.");
                EmitSurfaceQuad(target, surface, text, in frame, in snapshot, in operation, operationIndex, styleSlot, emScale);
            }
            TextStudio2DSurfaceTable.Shared.FlushAndBind();
        }

        static int SurfaceGroup(in TextStudioFrame frame, in TextStudioVisualSnapshot snapshot,
            TextStudio2DResolvedSelection selection, in TextStudio2DVisualFragment fragment)
        {
            // Wrapped lines remain one surface; explicit breaks own independent
            // placement and treatment margins unless connection is requested.
            if (fragment.MemberCount == 0) return 0;
            int unit = snapshot.GetGlyph(selection.GetMember(fragment.FirstMember)).TextUnitIndex;
            for (int previous = unit - 1; previous >= 0; previous--)
                if (frame.Plan.IsLineBreakUnit(previous)) return previous + 1;
            return 0;
        }

        void EmitSurfaceQuad(GeometryBuffer target, ConnectedSurface surface, TMP_Text text, in TextStudioFrame frame, in TextStudioVisualSnapshot snapshot,
            in TextStudio2DCompiledOperation operation, int operationIndex, int styleSlot, float emScale, bool alreadyPlaced = false, float? surfaceOpacity = null)
        {
            Vector4 bounds = surface.Geometry.Bounds;
            TextStudio2DDecorationSpec treatments = operation.Decoration;
            ResolveDecorationSurfaceExpansion(in treatments, emScale, out Vector4 expansion);
            float guard = Mathf.Max(.0001f, emScale * .02f);
            float left = bounds.x - expansion.x - guard, right = bounds.z + expansion.z + guard;
            float bottom = bounds.y - expansion.y - guard, top = bounds.w + expansion.w + guard;
            float width = Mathf.Max(.0001f, bounds.z - bounds.x), height = Mathf.Max(.0001f, bounds.w - bounds.y);
            int representative = _surfaceMembers[0];
            Color32 color = frame.GetSourceColors(representative).Sample(.5f, .5f);
            float opacity = 0;
            foreach (int member in _surfaceMembers)
            {
                float alpha = frame.Animated.IsCreated && member < frame.AnimatedCount ? frame.Animated[member].alpha : 1;
                opacity = Mathf.Max(opacity, alpha * frame.GetSourceColors(member).Sample(.5f, .5f).a / 255f);
            }
            color.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(surfaceOpacity ?? opacity) * 255);
            Vector4 uvBottomLeft = new Vector4((left - bounds.x) / width, (bottom - bounds.y) / height, 0, 0);
            Vector4 uvBottomRight = new Vector4((right - bounds.x) / width, uvBottomLeft.y, 1, 0);
            Vector4 uvTopLeft = new Vector4(uvBottomLeft.x, (top - bounds.y) / height, 0, 1);
            Vector4 uvTopRight = new Vector4(uvBottomRight.x, uvTopLeft.y, 1, 1);
            Vector4 programBottomLeft = new Vector4(NormalizeTextX(left, in snapshot), NormalizeTextY(bottom, in snapshot), styleSlot, operationIndex);
            Vector4 programBottomRight = new Vector4(NormalizeTextX(right, in snapshot), programBottomLeft.y, styleSlot, operationIndex);
            Vector4 programTopLeft = new Vector4(programBottomLeft.x, NormalizeTextY(top, in snapshot), styleSlot, operationIndex);
            Vector4 programTopRight = new Vector4(programBottomRight.x, programTopLeft.y, styleSlot, operationIndex);
            if (!alreadyPlaced && BuildSurfacePlacement(text, in frame, in snapshot))
            {
                EmitPlacedSurface(target, new Vector4(left, bottom, right, top), bounds, color,
                    new Vector4(width, height, emScale, -(representative + 1)),
                    new Vector4(surface.Handle.Slot, surface.Handle.Generation, 0, 0), in snapshot, styleSlot, operationIndex);
                return;
            }
            target.EmitQuad(new Vector3(left, bottom), new Vector3(right, bottom), new Vector3(left, top), new Vector3(right, top), color,
                uvBottomLeft, uvBottomRight, uvTopLeft, uvTopRight, programBottomLeft, programBottomRight, programTopLeft, programTopRight,
                new Vector4(width, height, emScale, -(representative + 1)), new Vector4(surface.Handle.Slot, surface.Handle.Generation, 0, 0));
        }

        sealed class ConnectedSurface
        {
            internal TextStudio2DSurfaceHandle Handle;
            internal TextStudio2DSurfaceGeometry Geometry;
            internal ulong InputFingerprint;
        }
    }
}
