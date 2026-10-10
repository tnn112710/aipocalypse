using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio
{
    /// <summary>Pooled panel-native particle quads for Play Mode reactions.</summary>
    internal sealed class TextStudioUITKReactionParticles :
        ITextStudioReactionUIParticleOutput, IDisposable
    {
        const int MaximumFrames = 32;
        const int MaximumParticles = 32;

        sealed class Frame
        {
            internal readonly VisualElement Element;
            internal readonly TextStudioReactionUIParticle[] Particles =
                new TextStudioReactionUIParticle[MaximumParticles];
            internal readonly TextStudioUITKReactionParticles Owner;
            internal VisualElement Parent;
            internal IPanel Panel;
            internal Vector3 Anchor;
            internal int Count;
            internal uint Version;
            internal bool Active;

            internal Frame(TextStudioUITKReactionParticles owner)
            {
                Owner = owner;
                Element = new VisualElement { name = "text-studio-reaction-particles" };
                Element.pickingMode = PickingMode.Ignore;
                Element.focusable = false;
                Element.style.position = Position.Absolute;
                Element.style.left = 0f;
                Element.style.top = 0f;
                Element.style.backgroundColor = Color.clear;
                Element.generateVisualContent += Draw;
            }

            internal void Prepare(TextElement source, int count,
                in TextStudioReactionAnchor anchor)
            {
                Parent = source;
                Panel = source.panel;
                Anchor = anchor.Position;
                Count = count;
                Active = true;
                unchecked { Version++; }
                if (Version == 0) Version = 1;
                Element.style.left = 0f;
                Element.style.top = 0f;
                Element.style.width = source.layout.width;
                Element.style.height = source.layout.height;
                Element.style.display = DisplayStyle.Flex;
                Parent.Add(Element);
            }

            internal bool IsCurrent(uint version) =>
                Active && Version == version && Panel != null &&
                Owner.Source == Parent && Owner.Source.panel == Panel &&
                Element.parent == Parent &&
                Element.panel == Panel;

            internal void Stop()
            {
                Active = false;
                Count = 0;
                Parent = null;
                Panel = null;
                Element.RemoveFromHierarchy();
            }

            internal void Dispose()
            {
                Stop();
                Element.generateVisualContent -= Draw;
            }

            void Draw(MeshGenerationContext context)
            {
                if (!Active || Panel == null || Element.panel != Panel ||
                    Owner.Source != Parent || Owner.Source.panel != Panel ||
                    Element.parent != Parent)
                    return;
                int visible = 0;
                for (int i = 0; i < Count; i++)
                    if (Particles[i].Size > 0f && Particles[i].Color.a > 0) visible++;
                if (visible == 0) return;
                MeshWriteData mesh = context.Allocate(visible * 5, visible * 12);
                Matrix4x4 panelToLocal = Element.worldTransform.inverse;
                int vertex = 0;
                for (int i = 0; i < Count; i++)
                {
                    TextStudioReactionUIParticle particle = Particles[i];
                    if (particle.Size <= 0f || particle.Color.a == 0) continue;
                    Vector3 center = panelToLocal.MultiplyPoint3x4(
                        Anchor + new Vector3(particle.Offset.x, particle.Offset.y, 0f));
                    Vector2 direction = particle.Offset.sqrMagnitude > .000001f
                        ? particle.Offset.normalized : Vector2.right;
                    Vector3 radial = panelToLocal.MultiplyVector(
                        new Vector3(direction.x, direction.y, 0f));
                    Vector3 tangent = panelToLocal.MultiplyVector(
                        new Vector3(-direction.y, direction.x, 0f));
                    Vector3 outer = radial * (particle.Size * 1.15f);
                    Vector3 inner = radial * (particle.Size * .65f);
                    Vector3 side = tangent * (particle.Size * .48f);
                    float z = Vertex.nearZ;
                    Color32 core = particle.Color;
                    core.r = (byte)((core.r + 255) / 2);
                    core.g = (byte)((core.g + 255) / 2);
                    core.b = (byte)((core.b + 255) / 2);
                    Color32 edge = particle.Color;
                    edge.a = 0;
                    Vector3 origin = new Vector3(center.x, center.y, z);
                    mesh.SetNextVertex(new Vertex { position = origin, tint = core });
                    mesh.SetNextVertex(new Vertex { position = origin + outer, tint = edge });
                    mesh.SetNextVertex(new Vertex { position = origin + side, tint = edge });
                    mesh.SetNextVertex(new Vertex { position = origin - inner, tint = edge });
                    mesh.SetNextVertex(new Vertex { position = origin - side, tint = edge });
                    mesh.SetNextIndex((ushort)vertex);
                    mesh.SetNextIndex((ushort)(vertex + 1));
                    mesh.SetNextIndex((ushort)(vertex + 2));
                    mesh.SetNextIndex((ushort)vertex);
                    mesh.SetNextIndex((ushort)(vertex + 2));
                    mesh.SetNextIndex((ushort)(vertex + 3));
                    mesh.SetNextIndex((ushort)vertex);
                    mesh.SetNextIndex((ushort)(vertex + 3));
                    mesh.SetNextIndex((ushort)(vertex + 4));
                    mesh.SetNextIndex((ushort)vertex);
                    mesh.SetNextIndex((ushort)(vertex + 4));
                    mesh.SetNextIndex((ushort)(vertex + 1));
                    vertex += 5;
                }
            }
        }

        readonly TextStudioBinding _binding;
        readonly List<Frame> _frames = new List<Frame>(2);
        bool _disposed;

        internal TextStudioUITKReactionParticles(TextStudioBinding binding) =>
            _binding = binding;

        internal TextElement Source => _binding.Element;

        internal bool CanAcquire(int count)
        {
            TextElement source = Source;
            if (_disposed || !Application.isPlaying || source?.panel == null ||
                source.parent == null || count < 1 || count > MaximumParticles)
                return false;
            if (_frames.Count < MaximumFrames) return true;
            for (int i = 0; i < _frames.Count; i++)
                if (!_frames[i].Active) return true;
            return false;
        }

        internal bool TryAcquire(in TextStudioReactionAnchor anchor, int count,
            out TextStudioReactionUIParticleLease lease)
        {
            lease = default;
            if (anchor.Space != TextStudioReactionAnchorSpace.Panel ||
                !CanAcquire(count)) return false;
            int slot = -1;
            for (int i = 0; i < _frames.Count; i++)
                if (!_frames[i].Active) { slot = i; break; }
            if (slot < 0)
            {
                slot = _frames.Count;
                _frames.Add(new Frame(this));
            }
            Frame frame = _frames[slot];
            frame.Prepare(Source, count, in anchor);
            lease = new TextStudioReactionUIParticleLease(this, slot, frame.Version);
            return true;
        }

        public bool TrySet(int slot, uint version, int index,
            in TextStudioReactionUIParticle particle)
        {
            if (!TryGetFrame(slot, version, out Frame frame) ||
                index < 0 || index >= frame.Count ||
                !Finite(particle.Offset.x) || !Finite(particle.Offset.y) ||
                !Finite(particle.Size) || particle.Size < 0f)
                return false;
            frame.Particles[index] = particle;
            return true;
        }

        public bool Commit(int slot, uint version)
        {
            if (!TryGetFrame(slot, version, out Frame frame)) return false;
            frame.Element.MarkDirtyRepaint();
            return true;
        }

        public void Release(int slot, uint version)
        {
            if (TryGetFrame(slot, version, out Frame frame)) frame.Stop();
        }

        bool TryGetFrame(int slot, uint version, out Frame frame)
        {
            if (!_disposed && slot >= 0 && slot < _frames.Count)
            {
                frame = _frames[slot];
                if (frame.Active && frame.Version == version)
                {
                    if (frame.IsCurrent(version)) return true;
                    frame.Stop();
                }
            }
            frame = null;
            return false;
        }

        static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        public void Dispose()
        {
            if (_disposed) return;
            for (int i = 0; i < _frames.Count; i++) _frames[i].Dispose();
            _frames.Clear();
            _disposed = true;
        }
    }
}
