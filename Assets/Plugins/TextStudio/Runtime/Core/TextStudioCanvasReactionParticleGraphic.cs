using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio
{
    /// <summary>Pooled Canvas output for the same UI spark command used by UI Toolkit.</summary>
    internal sealed class TextStudioCanvasReactionParticles :
        ITextStudioReactionUIParticleOutput, IDisposable
    {
        const int MaximumFrames = 32;
        const int MaximumParticles = 32;

        sealed class Frame
        {
            internal readonly GameObject Root;
            internal readonly TextStudioCanvasReactionParticleGraphic Graphic;
            internal readonly TextStudioReactionUIParticle[] Particles =
                new TextStudioReactionUIParticle[MaximumParticles];
            internal RectTransform Source;
            internal int Count;
            internal uint Version;
            internal bool Active;

            internal Frame()
            {
                Root = new GameObject("Text Studio UI Reaction Particles",
                    typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(TextStudioCanvasReactionParticleGraphic));
                Root.SetActive(false);
                Graphic = Root.GetComponent<TextStudioCanvasReactionParticleGraphic>();
                Graphic.raycastTarget = false;
                Graphic.Bind(Particles);
            }

            internal void Prepare(RectTransform source, int count,
                in TextStudioReactionAnchor anchor)
            {
                Source = source;
                Count = count;
                Active = true;
                unchecked { Version++; }
                if (Version == 0) Version = 1;
                RectTransform rect = (RectTransform)Root.transform;
                rect.SetParent(source, false);
                rect.anchorMin = rect.anchorMax = source.pivot;
                rect.pivot = source.pivot;
                rect.anchoredPosition = Vector2.zero;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                rect.SetAsLastSibling();
                Graphic.SetFrame(count, source.InverseTransformPoint(anchor.Position));
                Root.SetActive(true);
            }

            internal bool IsCurrent(uint version) =>
                Active && Version == version && Source != null &&
                Source.gameObject.activeInHierarchy &&
                Root != null && Root.transform.parent == Source &&
                Graphic != null && Graphic.isActiveAndEnabled;

            internal void Stop()
            {
                Active = false;
                Count = 0;
                Source = null;
                if (Root != null) Root.SetActive(false);
            }
        }

        readonly List<Frame> _frames = new List<Frame>(2);
        RectTransform _source;
        bool _disposed;

        internal TextStudioCanvasReactionParticles(RectTransform source) => _source = source;
        internal bool MatchesSource(RectTransform source) => !_disposed && _source == source;

        internal bool CanAcquire(RectTransform source, int count)
        {
            if (_disposed || !Application.isPlaying || source == null ||
                !source.gameObject.activeInHierarchy || count < 1 ||
                count > MaximumParticles || source.GetComponentInParent<Canvas>() == null)
                return false;
            if (_frames.Count < MaximumFrames) return true;
            for (int i = 0; i < _frames.Count; i++)
                if (!_frames[i].Active) return true;
            return false;
        }

        internal bool TryAcquire(RectTransform source,
            in TextStudioReactionAnchor anchor, int count,
            out TextStudioReactionUIParticleLease lease)
        {
            lease = default;
            if (anchor.Space != TextStudioReactionAnchorSpace.World ||
                !CanAcquire(source, count)) return false;
            if (_source != source)
            {
                Dispose();
                return false;
            }
            int slot = -1;
            for (int i = 0; i < _frames.Count; i++)
                if (!_frames[i].Active) { slot = i; break; }
            if (slot < 0)
            {
                slot = _frames.Count;
                _frames.Add(new Frame());
            }
            Frame frame = _frames[slot];
            frame.Prepare(source, count, in anchor);
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
            frame.Graphic.SetVerticesDirty();
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

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = 0; i < _frames.Count; i++)
                if (_frames[i].Root != null)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(_frames[i].Root);
                    else
                        UnityEngine.Object.DestroyImmediate(_frames[i].Root);
                }
            _frames.Clear();
            _source = null;
        }
    }

    /// <summary>Draws pooled reaction particles as spark geometry through normal Canvas masking.</summary>
    [AddComponentMenu("")]
    public sealed class TextStudioCanvasReactionParticleGraphic : MaskableGraphic
    {
        TextStudioReactionUIParticle[] _particles;
        Vector2 _anchor;
        int _count;

        internal void Bind(TextStudioReactionUIParticle[] particles) =>
            _particles = particles;

        internal void SetFrame(int count, Vector3 anchor)
        {
            _count = count;
            _anchor = anchor;
            color = Color.white;
            SetVerticesDirty();
        }

        /// <summary>Builds faded spark geometry for visible particles around their Canvas-local anchor.</summary>
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (_particles == null || _count == 0) return;
            for (int i = 0; i < _count; i++)
            {
                TextStudioReactionUIParticle particle = _particles[i];
                if (particle.Size <= 0f || particle.Color.a == 0) continue;
                Vector2 direction = particle.Offset.sqrMagnitude > .000001f
                    ? particle.Offset.normalized : Vector2.right;
                Vector2 tangent = new Vector2(-direction.y, direction.x);
                Vector2 center = _anchor + particle.Offset;
                Vector2 outer = direction * (particle.Size * 1.15f);
                Vector2 inner = direction * (particle.Size * .65f);
                Vector2 side = tangent * (particle.Size * .48f);
                Color32 core = particle.Color;
                core.r = (byte)((core.r + 255) / 2);
                core.g = (byte)((core.g + 255) / 2);
                core.b = (byte)((core.b + 255) / 2);
                Color32 edge = particle.Color;
                edge.a = 0;
                int vertex = helper.currentVertCount;
                AddVertex(helper, center, core);
                AddVertex(helper, center + outer, edge);
                AddVertex(helper, center + side, edge);
                AddVertex(helper, center - inner, edge);
                AddVertex(helper, center - side, edge);
                helper.AddTriangle(vertex, vertex + 2, vertex + 1);
                helper.AddTriangle(vertex, vertex + 3, vertex + 2);
                helper.AddTriangle(vertex, vertex + 4, vertex + 3);
                helper.AddTriangle(vertex, vertex + 1, vertex + 4);
            }
        }

        static void AddVertex(VertexHelper helper, Vector2 position, Color32 tint)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = tint;
            helper.AddVert(vertex);
        }
    }
}
