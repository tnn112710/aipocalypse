using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.TimeSystem
{
    public sealed class TimeService : IDisposable
    {
        readonly Dictionary<object, float> _requests = new();
        readonly float _fixedStep = Time.fixedDeltaTime;
        readonly float _initialScale = Time.timeScale;

        public bool IsPaused { get; private set; }
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

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            Apply();
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
            Time.timeScale = _initialScale;
            Time.fixedDeltaTime = _fixedStep;
        }
    }
}
