using MB13.Aipocalypse.Input;
using Unity.Cinemachine;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Sole writer of the world camera's FOV: the player's setting narrowed by the held item's zoom or scaled by a pose sequence.
    // The held model keeps the unzoomed FOV (FirstPersonView) so the gun doesn't swell on screen.
    public sealed class PlayerZoom
    {
        public const float MinScale = 0.1f;
        public const float MaxScale = 1.5f;

        readonly CinemachineCamera _camera;
        readonly FirstPersonView _firstPersonView;
        readonly ControlSettings _controls;
        readonly float _prefabFov;
        float _baseFov;
        float _baseTan;

        public PlayerZoom(CinemachineCamera camera, FirstPersonView firstPersonView, ControlSettings controls)
        {
            _camera = camera;
            _firstPersonView = firstPersonView;
            _controls = controls;
            _prefabFov = camera != null ? camera.Lens.FieldOfView : 60f;
            SetBase(BaseFieldOfView);
            Tick(1f);
        }

        float BaseFieldOfView => _controls != null && _controls.FieldOfView > 0f ? _controls.FieldOfView : _prefabFov;

        // Look speed scale that keeps aim feel the same at any zoom (1 unzoomed, < 1 zoomed in). A wider view (sprint, shot
        // punch) doesn't speed the look up.
        public float LookScale { get; private set; } = 1f;
        // tan(half the world camera's vertical field of view): maps eye-space directions to the screen.
        public float TanHalfFov { get; private set; }

        // `zoom` multiplies the base field of view: 1 = none, 0.85 = 15% narrower, above 1 wider (the death sequence).
        public void Tick(float zoom)
        {
            if (_camera == null)
                return;
            float baseFov = BaseFieldOfView;
            if (!Mathf.Approximately(baseFov, _baseFov))
                SetBase(baseFov);
            float fov = _baseFov * Mathf.Clamp(zoom, MinScale, MaxScale);
            _camera.Lens.FieldOfView = fov;
            TanHalfFov = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            LookScale = Mathf.Min(1f, TanHalfFov / _baseTan);
        }

        void SetBase(float fieldOfView)
        {
            _baseFov = fieldOfView;
            _baseTan = Mathf.Tan(_baseFov * 0.5f * Mathf.Deg2Rad);
            if (_firstPersonView != null)
                _firstPersonView.SetFieldOfView(_baseFov);
        }
    }
}
