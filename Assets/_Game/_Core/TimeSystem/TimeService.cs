using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Aipocalypse.Core.TimeSystem
{
    public sealed class TimeService : ITickable, IDisposable
    {
        readonly Dictionary<object, float> _requests = new();
        readonly HashSet<object> _pauseOwners = new();
        readonly float _fixedStep = Time.fixedDeltaTime;
        readonly float _initialScale = Time.timeScale;
        readonly object _hitStop = new();
        float _hitStopRemaining;

        // Paused while any owner holds a pause request (pause menu, inventory…); explicit pause beats every slow-motion request.
        public bool IsPaused => _pauseOwners.Count > 0;
        public float Scale { get; private set; } = 1f;

        public void Request(object owner, float scale)
        {
            _requests[owner] = Mathf.Clamp01(scale);
            Apply();
        }

        public void Release(object owner)
        {
            if (_requests.Remove(owner))
                Apply();
        }

        // A short slow-down timed in real seconds; overlapping stops keep the longest time and the lowest scale.
        public void RequestHitStop(float duration, float scale)
        {
            if (duration <= 0f || IsPaused)
                return;
            scale = Mathf.Clamp01(scale);
            if (_requests.TryGetValue(_hitStop, out float current))
                scale = Mathf.Min(scale, current);
            _hitStopRemaining = Mathf.Max(_hitStopRemaining, duration);
            Request(_hitStop, scale);
        }

        public void RequestPause(object owner)
        {
            if (owner != null && _pauseOwners.Add(owner))
                Apply();
        }

        public void ReleasePause(object owner)
        {
            if (owner != null && _pauseOwners.Remove(owner))
                Apply();
        }

        public void Tick() => AdvanceHitStop(Time.unscaledDeltaTime);

        public void AdvanceHitStop(float unscaledDeltaTime)
        {
            if (_hitStopRemaining <= 0f || IsPaused)
                return;
            _hitStopRemaining -= unscaledDeltaTime;
            if (_hitStopRemaining <= 0f)
                Release(_hitStop);
        }

        void Apply()
        {
            float scale = 1f;
            foreach (float request in _requests.Values)
                scale = Mathf.Min(scale, request);
            Scale = IsPaused ? 0f : scale;
            Time.timeScale = Scale;
            Time.fixedDeltaTime = _fixedStep * Mathf.Max(0.01f, Scale);
        }

        public void Dispose()
        {
            _requests.Clear();
            _pauseOwners.Clear();
            _hitStopRemaining = 0f;
            Time.timeScale = _initialScale;
            Time.fixedDeltaTime = _fixedStep;
        }
    }
}
