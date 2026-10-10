#if UNITY_6000_2_OR_NEWER

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio
{
    /// <summary>Retains a presented UI Toolkit text frame through native panel text output.</summary>
    internal sealed class TextStudioUITKReactionCapture :
        ITextStudioReactionCaptureProvider,
        ITextStudioReactionVisualOutputProvider,
        IDisposable
    {
        const int MaximumFrames = 8;
        const int MaximumGlyphs = 512;

        struct GlyphQuad
        {
            internal int Unit;
            internal Vector3 A, B, C, D;
            internal Color32 ColorA, ColorB, ColorC, ColorD;
        }

        sealed class Frame
        {
            internal readonly Label Proxy;
            internal readonly TextStudioUITKReactionCapture Owner;
            internal GlyphQuad[] Quads = Array.Empty<GlyphQuad>();
            internal TextStudioReactionVisualSample[] Samples =
                Array.Empty<TextStudioReactionVisualSample>();
            internal bool[] ActiveUnits = Array.Empty<bool>();
            internal IPanel Panel;
            internal VisualElement Parent;
            internal int GlyphCount;
            internal int Revision;
            internal int LeaseCount;
            internal bool Active;
            internal bool Valid;

            internal Frame(TextStudioUITKReactionCapture owner)
            {
                Owner = owner;
                Proxy = new Label { name = "text-studio-reaction-output" };
                Proxy.pickingMode = PickingMode.Ignore;
                Proxy.focusable = false;
                Proxy.style.position = Position.Absolute;
                Proxy.style.backgroundColor = Color.clear;
                Proxy.style.left = 0f;
                Proxy.style.top = 0f;
                Proxy.style.flexGrow = 0f;
                Proxy.style.flexShrink = 0f;
                Proxy.PostProcessTextVertices += OnPostProcessTextVertices;
            }

            internal void Prepare(TextElement source, string presentedText,
                GlyphQuad[] quads, int glyphCount, int unitCount, int revision)
            {
                if (Quads.Length < glyphCount)
                    Quads = new GlyphQuad[Mathf.NextPowerOfTwo(glyphCount)];
                if (Samples.Length < unitCount)
                {
                    int capacity = Mathf.NextPowerOfTwo(unitCount);
                    Samples = new TextStudioReactionVisualSample[capacity];
                    ActiveUnits = new bool[capacity];
                }
                else Array.Clear(ActiveUnits, 0, ActiveUnits.Length);
                Array.Copy(quads, Quads, glyphCount);
                GlyphCount = glyphCount;
                Revision = revision;
                LeaseCount = 0;
                Panel = source.panel;
                Parent = source.parent;
                Active = true;
                Valid = true;
                Proxy.text = presentedText;
                Proxy.enableRichText = source.enableRichText;
                IResolvedStyle resolved = source.resolvedStyle;
                Proxy.style.width = source.layout.width;
                Proxy.style.height = source.layout.height;
                Proxy.style.left = 0f;
                Proxy.style.top = 0f;
                Proxy.style.unityFontDefinition = resolved.unityFontDefinition;
                Proxy.style.unityFontStyleAndWeight = resolved.unityFontStyleAndWeight;
                Proxy.style.fontSize = resolved.fontSize;
                Proxy.style.unityTextAlign = resolved.unityTextAlign;
                Proxy.style.whiteSpace = resolved.whiteSpace;
                Proxy.style.letterSpacing = resolved.letterSpacing;
                Proxy.style.wordSpacing = resolved.wordSpacing;
                Proxy.style.unityParagraphSpacing = resolved.unityParagraphSpacing;
                Proxy.style.paddingLeft = resolved.paddingLeft;
                Proxy.style.paddingTop = resolved.paddingTop;
                Proxy.style.paddingRight = resolved.paddingRight;
                Proxy.style.paddingBottom = resolved.paddingBottom;
                Proxy.style.color = resolved.color;
                Proxy.style.opacity = 1f;
                source.Add(Proxy);
            }

            internal void Stop()
            {
                Active = false;
                Valid = false;
                LeaseCount = 0;
                Panel = null;
                Parent = null;
                Array.Clear(ActiveUnits, 0, ActiveUnits.Length);
                Proxy.RemoveFromHierarchy();
            }

            internal void Dispose()
            {
                Stop();
                Proxy.PostProcessTextVertices -= OnPostProcessTextVertices;
            }

            void OnPostProcessTextVertices(TextElement.GlyphsEnumerable glyphs)
            {
                if (!Active || !Valid || Panel == null ||
                    Owner.Source == null || Owner.Source.panel != Panel ||
                    Owner.Source.parent != Parent || Proxy.parent != Owner.Source ||
                    Proxy.panel != Panel)
                {
                    HideAll(glyphs);
                    return;
                }
                int count = 0;
                foreach (TextElement.Glyph glyph in glyphs)
                {
                    if (glyph.vertices.Length > 4)
                    {
                        Valid = false;
                        Owner.InvalidatePresentedFrame(Revision);
                        HideAll(glyphs);
                        return;
                    }
                    if (glyph.vertices.Length == 4) count++;
                }
                if (count != GlyphCount)
                {
                    Valid = false;
                    Owner.InvalidatePresentedFrame(Revision);
                    HideAll(glyphs);
                    return;
                }
                Matrix4x4 sourceToProxy = Proxy.worldTransform.inverse *
                                          Owner.Source.worldTransform;
                int index = 0;
                foreach (TextElement.Glyph glyph in glyphs)
                {
                    NativeSlice<Vertex> vertices = glyph.vertices;
                    if (vertices.Length < 4) continue;
                    GlyphQuad quad = Quads[index++];
                    int unit = quad.Unit;
                    if (unit < 0 || unit >= ActiveUnits.Length ||
                        !ActiveUnits[unit])
                    {
                        Hide(vertices);
                        continue;
                    }
                    TextStudioReactionVisualSample sample = Samples[unit];
                    Vector3 center = (quad.A + quad.C) * .5f;
                    Quaternion rotation = Quaternion.Inverse(sample.LocalRotation);
                    Vector3 panelOffset = new Vector3(sample.LocalOffset.x,
                        -sample.LocalOffset.y, sample.LocalOffset.z);
                    Write(vertices, 0, quad.A, quad.ColorA, center,
                        rotation, panelOffset, in sample, sourceToProxy);
                    Write(vertices, 1, quad.B, quad.ColorB, center,
                        rotation, panelOffset, in sample, sourceToProxy);
                    Write(vertices, 2, quad.C, quad.ColorC, center,
                        rotation, panelOffset, in sample, sourceToProxy);
                    Write(vertices, 3, quad.D, quad.ColorD, center,
                        rotation, panelOffset, in sample, sourceToProxy);
                }
            }

            static void Write(NativeSlice<Vertex> vertices, int index,
                Vector3 position, Color32 color, Vector3 center,
                Quaternion rotation, Vector3 offset,
                in TextStudioReactionVisualSample sample,
                Matrix4x4 sourceToProxy)
            {
                Vector3 local = center + rotation *
                    Vector3.Scale(position - center, sample.LocalScale) + offset;
                Vertex vertex = vertices[index];
                vertex.position = sourceToProxy.MultiplyPoint3x4(local);
                vertex.tint = sample.ApplyColor(color);
                vertices[index] = vertex;
            }

            static void HideAll(TextElement.GlyphsEnumerable glyphs)
            {
                foreach (TextElement.Glyph glyph in glyphs)
                    Hide(glyph.vertices);
            }

            static void Hide(NativeSlice<Vertex> vertices)
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vertex vertex = vertices[i];
                    vertex.tint.a = 0;
                    vertices[i] = vertex;
                }
            }
        }

        sealed class Slot
        {
            internal int FrameIndex;
            internal int Unit;
            internal int Next;
            internal uint Version;
            internal bool Active;
            internal TextStudioReactionAnchor Anchor;
        }

        readonly TextStudioBinding _binding;
        readonly List<Frame> _frames = new List<Frame>(2);
        readonly List<Slot> _slots = new List<Slot>(8);
        GlyphQuad[] _presented = Array.Empty<GlyphQuad>();
        string _presentedText;
        IPanel _presentedPanel;
        Matrix4x4 _presentedWorldTransform;
        int _presentedCount;
        int _presentedUnitCount;
        int _revision;
        int _freeSlot = -1;
        bool _presentedValid;
        bool _disposed;
        string _presentedFailure;

        internal TextStudioUITKReactionCapture(TextStudioBinding binding) =>
            _binding = binding;

        internal TextElement Source => _binding.Element;

        void InvalidatePresentedFrame(int revision)
        {
            if (_revision == revision)
            {
                _presentedValid = false;
                _presentedFailure = "The native UI Toolkit text proxy did not reproduce the presented glyph grouping.";
            }
        }

        internal void CapturePresentedGlyphs(TextElement.GlyphsEnumerable glyphs)
        {
            TextElement source = Source;
            if (_disposed || source?.panel == null) return;
            _presentedText = source.text;
            _presentedPanel = source.panel;
            _presentedWorldTransform = source.worldTransform;
            TextStudioPlanView plan = _binding.Plan;
            string resolvedText = _binding.ResolvedText;
            int unitCount = _binding.StateCount;
            if (!plan.IsCreated || resolvedText == null || unitCount <= 0 ||
                unitCount > MaximumGlyphs)
            {
                _presentedValid = false;
                _presentedFailure = unitCount > MaximumGlyphs
                    ? "The UI Toolkit text exceeds the 512-unit retained-output limit."
                    : "The UI Toolkit text has no compiled glyph layout to retain.";
                return;
            }
            int count = 0;
            bool regularQuads = true;
            foreach (TextElement.Glyph glyph in glyphs)
            {
                if (glyph.vertices.Length > 4) regularQuads = false;
                if (glyph.vertices.Length == 4) count++;
            }
            if (!regularQuads || count <= 0 || count > MaximumGlyphs)
            {
                _presentedValid = false;
                _presentedFailure = !regularQuads
                    ? "This UI Toolkit glyph grouping is not a regular quad and cannot be retained."
                    : "The UI Toolkit text has no regular glyph quads or exceeds the 512-glyph retained-output limit.";
                return;
            }
            if (_presented.Length < count)
                _presented = new GlyphQuad[Mathf.NextPowerOfTwo(count)];
            TextStudioGlyphIndexMappingMode mode =
                TextStudioGlyphIndexMapper.ResolveMappingMode(
                    resolvedText, unitCount, count);
            int searchIndex = 0;
            int rendered = 0;
            bool hasPrevious = false;
            Vector3 previousCenter = Vector3.zero;
            foreach (TextElement.Glyph glyph in glyphs)
            {
                NativeSlice<Vertex> vertices = glyph.vertices;
                if (vertices.Length < 4)
                {
                    if (!TextStudioGlyphIndexMapper.TryConsumeUnrenderedGlyph(
                            resolvedText, ref searchIndex, unitCount, mode))
                        TextStudioGlyphIndexMapper.TryGetNextGlyphIndex(
                            resolvedText, ref searchIndex, unitCount, mode, out _);
                    continue;
                }
                Vertex a = vertices[0], b = vertices[1], c = vertices[2], d = vertices[3];
                Vector3 center = (a.position + c.position) * .5f;
                bool newLine = hasPrevious &&
                    center.x + .5f < previousCenter.x &&
                    Mathf.Abs(center.y - previousCenter.y) > .5f;
                int unit = -1;
                if (TextStudioGlyphIndexMapper.TryGetNextGlyphIndex(
                        resolvedText, ref searchIndex, resolvedText.Length,
                        mode, newLine, out int resolvedChar))
                    unit = plan.GetUnitIndexForResolvedChar(resolvedChar);
                _presented[rendered++] = new GlyphQuad
                {
                    Unit = unit,
                    A = a.position, B = b.position,
                    C = c.position, D = d.position,
                    ColorA = a.tint, ColorB = b.tint,
                    ColorC = c.tint, ColorD = d.tint
                };
                previousCenter = center;
                hasPrevious = true;
            }
            _presentedCount = rendered;
            _presentedUnitCount = unitCount;
            _presentedValid = rendered == count;
            _presentedFailure = _presentedValid ? null :
                "The UI Toolkit glyph mapping changed while capturing the presented frame.";
            unchecked { _revision++; }
            if (_revision == 0) _revision = 1;
        }

        internal bool TryGetPresentedOutgoingAnchor(
            in TextStudioReactionCaptureRequest request,
            out TextStudioReactionAnchor anchor)
        {
            anchor = default;
            return !_disposed && Application.isPlaying && _presentedValid &&
                   Source?.panel == _presentedPanel &&
                   string.Equals(_presentedText, _binding.VisibleText, StringComparison.Ordinal) &&
                   request.Intent == TextStudioReactionCaptureIntent.Copy &&
                   request.Resources == TextStudioReactionResourceClass.None &&
                   request.Changes.IsDefined &&
                   request.Changes.OutgoingGlyphCount == _presentedUnitCount &&
                   TryGetPresentedAnchor(request.Context.UnitIndex, out anchor);
        }

        public TextStudioReactionCaptureCapability QuerySupport(
            in TextStudioReactionCaptureRequest request)
        {
            if (_disposed || !Application.isPlaying || Source?.panel == null ||
                Source.parent == null)
                return TextStudioReactionCaptureCapability.Unsupported(
                    "UI Toolkit retained output requires an attached Play Mode panel.");
            if (request.Intent != TextStudioReactionCaptureIntent.Copy ||
                request.Resources != (TextStudioReactionResourceClass.GlyphGeometry |
                                      TextStudioReactionResourceClass.MaterialState))
                return TextStudioReactionCaptureCapability.Unsupported(
                    "UI Toolkit retains a native text proxy for copied glyph geometry and material together.");
            if (_presentedPanel != Source.panel ||
                !string.Equals(_presentedText, _binding.VisibleText, StringComparison.Ordinal))
                return TextStudioReactionCaptureCapability.Unsupported(
                    "The current UI Toolkit glyph frame has not been presented since the last text change.");
            if (!_presentedValid)
                return TextStudioReactionCaptureCapability.Unsupported(
                    _presentedFailure ??
                    "The current UI Toolkit glyph frame has not been presented since the last text change.");
            int unit = request.Context.UnitIndex;
            if (unit < 0 || unit >= _presentedUnitCount)
                return TextStudioReactionCaptureCapability.Unsupported(
                    "The selected semantic unit is outside the presented text frame.");
            for (int i = 0; i < _presentedCount; i++)
                if (_presented[i].Unit == unit)
                    return new TextStudioReactionCaptureCapability(
                        TextStudioReactionCaptureSupport.SupportedWithDeclaredLimitations,
                        "UI Toolkit retains presented positions and colors with a native text proxy; unsupported glyph grouping ends the tail.");
            return TextStudioReactionCaptureCapability.Unsupported(
                "The selected semantic unit has no presented UI Toolkit glyph.");
        }

        public bool TryCapture(in TextStudioReactionCaptureRequest request,
            out TextStudioReactionLease lease)
        {
            lease = default;
            if (!QuerySupport(in request).IsSupported ||
                !TryGetPresentedAnchor(request.Context.UnitIndex,
                    out TextStudioReactionAnchor anchor)) return false;
            int unit = request.Context.UnitIndex;
            if (_freeSlot < 0 && _slots.Count >= MaximumFrames * MaximumGlyphs)
                return false;
            int frameIndex = FindOrCreateFrame(unit);
            if (frameIndex < 0) return false;
            int slotIndex = AcquireSlot(frameIndex, unit, in anchor);
            if (slotIndex < 0)
            {
                if (_frames[frameIndex].LeaseCount == 0)
                    _frames[frameIndex].Stop();
                return false;
            }
            lease = new TextStudioReactionLease(this, slotIndex,
                _slots[slotIndex].Version);
            return true;
        }

        bool TryGetPresentedAnchor(int unit, out TextStudioReactionAnchor anchor)
        {
            anchor = default;
            TextElement source = Source;
            if (!_presentedValid || source == null ||
                source.panel != _presentedPanel) return false;
            bool found = false;
            Vector3 minimum = default;
            Vector3 maximum = default;
            for (int i = 0; i < _presentedCount; i++)
            {
                GlyphQuad quad = _presented[i];
                if (quad.Unit != unit) continue;
                Vector3 quadMinimum = Vector3.Min(Vector3.Min(quad.A, quad.B),
                    Vector3.Min(quad.C, quad.D));
                Vector3 quadMaximum = Vector3.Max(Vector3.Max(quad.A, quad.B),
                    Vector3.Max(quad.C, quad.D));
                minimum = found ? Vector3.Min(minimum, quadMinimum) : quadMinimum;
                maximum = found ? Vector3.Max(maximum, quadMaximum) : quadMaximum;
                found = true;
            }
            if (!found) return false;
            Matrix4x4 panelTransform = _presentedWorldTransform;
            Vector3 panelRight = panelTransform.MultiplyVector(Vector3.right);
            float angle = panelRight.sqrMagnitude > .000001f
                ? Mathf.Atan2(panelRight.y, panelRight.x) * Mathf.Rad2Deg
                : 0f;
            anchor = new TextStudioReactionAnchor(
                panelTransform.MultiplyPoint3x4((minimum + maximum) * .5f),
                Quaternion.AngleAxis(angle, Vector3.forward),
                TextStudioReactionAnchorSpace.Panel);
            return true;
        }

        int FindOrCreateFrame(int unit)
        {
            for (int i = 0; i < _frames.Count; i++)
            {
                Frame frame = _frames[i];
                if (frame.Active && frame.Valid && frame.Revision == _revision &&
                    frame.Panel == Source.panel && !frame.ActiveUnits[unit])
                    return i;
            }
            int free = -1;
            for (int i = 0; i < _frames.Count; i++)
                if (!_frames[i].Active) { free = i; break; }
            if (free < 0)
            {
                if (_frames.Count >= MaximumFrames) return -1;
                free = _frames.Count;
                _frames.Add(new Frame(this));
            }
            _frames[free].Prepare(Source, _presentedText, _presented,
                _presentedCount, _presentedUnitCount, _revision);
            return free;
        }

        int AcquireSlot(int frameIndex, int unit,
            in TextStudioReactionAnchor anchor)
        {
            int index;
            if (_freeSlot >= 0)
            {
                index = _freeSlot;
                _freeSlot = _slots[index].Next;
            }
            else
            {
                if (_slots.Count >= MaximumFrames * MaximumGlyphs) return -1;
                index = _slots.Count;
                _slots.Add(new Slot());
            }
            Slot slot = _slots[index];
            slot.Active = true;
            slot.FrameIndex = frameIndex;
            slot.Unit = unit;
            slot.Anchor = anchor;
            slot.Next = -1;
            unchecked { slot.Version++; }
            if (slot.Version == 0) slot.Version = 1;
            Frame frame = _frames[frameIndex];
            frame.ActiveUnits[unit] = true;
            frame.Samples[unit] = new TextStudioReactionVisualSample(
                Vector3.zero, Quaternion.identity, Vector3.one, 1f);
            frame.LeaseCount++;
            frame.Proxy.MarkDirtyRepaint();
            return index;
        }

        public bool TryGetAnchor(int index, uint version,
            out TextStudioReactionAnchor anchor)
        {
            if (TryGetSlot(index, version, out Slot slot))
            {
                anchor = slot.Anchor;
                return true;
            }
            anchor = default;
            return false;
        }

        public bool TrySubmit(int index, uint version,
            in TextStudioReactionVisualSample sample)
        {
            if (!TryGetSlot(index, version, out Slot slot)) return false;
            Frame frame = _frames[slot.FrameIndex];
            if (!frame.Active || !frame.Valid || frame.Panel != Source?.panel ||
                frame.Parent != Source.parent || frame.Proxy.parent != Source ||
                frame.Proxy.panel != frame.Panel)
                return false;
            frame.Samples[slot.Unit] = sample;
            frame.Proxy.MarkDirtyRepaint();
            return true;
        }

        public void Release(int index, uint version)
        {
            if (!TryGetSlot(index, version, out Slot slot)) return;
            Frame frame = _frames[slot.FrameIndex];
            frame.ActiveUnits[slot.Unit] = false;
            frame.LeaseCount--;
            if (frame.LeaseCount <= 0) frame.Stop();
            else frame.Proxy.MarkDirtyRepaint();
            slot.Active = false;
            slot.Anchor = default;
            slot.Next = _freeSlot;
            _freeSlot = index;
        }

        bool TryGetSlot(int index, uint version, out Slot slot)
        {
            if (!_disposed && index >= 0 && index < _slots.Count)
            {
                slot = _slots[index];
                if (slot.Active && slot.Version == version) return true;
            }
            slot = null;
            return false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            for (int i = 0; i < _frames.Count; i++)
                _frames[i].Dispose();
            _frames.Clear();
            _slots.Clear();
            _presentedPanel = null;
            _presentedText = null;
            _presentedValid = false;
            _presentedFailure = null;
            _disposed = true;
        }
    }
}

#endif
