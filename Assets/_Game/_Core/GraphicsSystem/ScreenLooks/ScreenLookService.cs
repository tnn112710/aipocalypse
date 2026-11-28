using System;
using UnityEngine;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.GraphicsSystem.ScreenLooks
{
    // Scene service: full-screen looks by name and strength (Request), drawn by the backend ScreenLookSettings names. Game code
    // never sees the tool. Without a backend, or turned off, every look is 0.
    public sealed class ScreenLookService : ITickable, IDisposable
    {
        static readonly ScreenLookId[] Looks = (ScreenLookId[])Enum.GetValues(typeof(ScreenLookId));

        readonly ScreenLookSettings _settings;
        readonly float[] _strength = new float[Looks.Length];
        readonly GameObject _root;
        readonly IScreenLookBackend _backend;

        public ScreenLookService(ScreenLookSettings settings)
        {
            _settings = settings;
            if (settings == null || settings.Backend == null)
                return;
            _root = new GameObject("Screen Looks");
            _backend = settings.Backend.Create(_root.transform);
        }

        public bool Enabled => _settings != null && _settings.Enabled && _backend != null;

        // Kept until changed; 0 = off.
        public void Request(ScreenLookId look, float strength) => _strength[(int)look] = Mathf.Clamp01(strength);

        public float Strength(ScreenLookId look) => _strength[(int)look];

        public void Tick() => Advance();

        public void Advance()
        {
            if (_backend == null)
                return;
            bool on = _settings.Enabled;
            for (int i = 0; i < Looks.Length; i++)
                _backend.Apply(Looks[i], on ? _strength[i] : 0f);
        }

        public void Dispose()
        {
            _backend?.Dispose();
            if (_root == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_root);
            else
                UnityEngine.Object.DestroyImmediate(_root);
        }
    }
}
