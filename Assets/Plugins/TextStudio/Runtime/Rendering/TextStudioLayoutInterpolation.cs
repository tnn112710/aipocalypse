using System;
using UnityEngine;

namespace TextStudio
{
    // One owner per text host. Positions are copied because layout and placement arrays are transient.
    // Offsets decay relative to the current layout so animated placement continues during interpolation.
    internal sealed class TextStudioLayoutInterpolation
    {
        Vector3[] _displayed = Array.Empty<Vector3>();
        Vector3[] _offsets = Array.Empty<Vector3>();
        int _previousCount;
        TextStudioGlyphChangeSet _changes;
        bool _pending;
        bool _animating;
        float _elapsed;
        float _duration;

        internal bool HasWork => _pending || _animating;

        internal void Prepare(in TextStudioGlyphChangeSet changes)
        {
            _changes = changes;
            _pending = true;
            _elapsed = 0f;
        }

        internal bool Advance(float deltaTime)
        {
            bool hadWork = HasWork;
            if (!_pending && _animating && float.IsFinite(deltaTime) && deltaTime > 0f)
            {
                _elapsed += deltaTime;
                if (_elapsed >= _duration) _animating = false;
            }
            return hadWork;
        }

        internal void Apply(GlyphPlacement[] placements, int count, float duration, TextStudioMotionPolicy policy)
        {
            count = Math.Min(Math.Max(0, count), placements?.Length ?? 0);
            if (count == 0) { Reset(); return; }
            if (_displayed.Length < count) Array.Resize(ref _displayed, TextStudioBufferCapacity.ForCount(count));
            if (_offsets.Length < count) Array.Resize(ref _offsets, TextStudioBufferCapacity.ForCount(count));
            _duration = float.IsFinite(duration) ? Mathf.Max(0f, duration) : 0f;
            if (policy == TextStudioMotionPolicy.None) _duration = 0f;
            else if (policy == TextStudioMotionPolicy.Reduced) _duration *= .2f;
            if (_pending)
            {
                bool canMap = _changes.IsDefined && _changes.OutgoingGlyphCount == _previousCount && _changes.IncomingGlyphCount == count;
                _animating = false;
                for (int i = 0; i < count; i++)
                {
                    int oldIndex = canMap ? _changes.GetOutgoingGlyphForIncoming(i) : -1;
                    Vector3 offset = oldIndex >= 0 && oldIndex < _previousCount
                        ? _displayed[oldIndex] - placements[i].position : Vector3.zero;
                    _offsets[i] = offset;
                    _animating |= offset.sqrMagnitude > .00000001f;
                }
                _elapsed = 0f;
                _pending = false;
                _changes = default;
            }
            else if (count != _previousCount)
            {
                Array.Clear(_offsets, 0, count);
                _animating = false;
            }
            if (_duration <= 0f || _elapsed >= _duration) _animating = false;
            float progress = _animating ? Mathf.Clamp01(_elapsed / _duration) : 1f;
            float weight = 1f - progress * progress * (3f - 2f * progress);
            for (int i = 0; i < count; i++)
            {
                placements[i].position += _offsets[i] * weight;
                _displayed[i] = placements[i].position;
            }
            _previousCount = count;
        }

        internal void Reset()
        {
            _previousCount = 0;
            _pending = false;
            _animating = false;
            _elapsed = 0f;
            _changes = default;
        }
    }
}
