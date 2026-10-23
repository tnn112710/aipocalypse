using Unity.Cinemachine;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // Narrows the Cinemachine lens by the held item's zoom (aiming down sights). The held model keeps the unzoomed field
    // of view (FirstPersonView), so the gun doesn't swell on screen.
    public sealed class PlayerZoom
    {
        readonly CinemachineCamera _camera;
        readonly float _baseFov;
        readonly float _baseTan;

        public PlayerZoom(CinemachineCamera camera, FirstPersonView firstPersonView)
        {
            _camera = camera;
            _baseFov = camera != null ? camera.Lens.FieldOfView : 60f;
            _baseTan = Mathf.Tan(_baseFov * 0.5f * Mathf.Deg2Rad);
            if (firstPersonView != null)
                firstPersonView.SetFieldOfView(_baseFov);
        }

        // Look speed scale that keeps aim feel the same at any zoom (1 unzoomed, < 1 zoomed in).
        public float LookScale { get; private set; } = 1f;
        // tan(half the world camera's vertical field of view): maps eye-space directions to the screen.
        public float TanHalfFov => _baseTan * LookScale;

        // `zoom` multiplies the base field of view: 1 = none, 0.85 = 15% narrower, above 1 wider (the death sequence).
        public void Tick(float zoom)
        {
            if (_camera == null)
                return;
            float fov = _baseFov * Mathf.Clamp(zoom, 0.1f, 1.5f);
            _camera.Lens.FieldOfView = fov;
            LookScale = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / _baseTan;
        }
    }
}
