using MB13.Aipocalypse.Input;
using Unity.Cinemachine;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Sole writer of the world camera's FOV: the player's setting narrowed by the held item's zoom or scaled by a pose sequence.
    // The held model's lens (FirstPersonView) takes only its own zoom (aiming), never the shot punch, sprint or sequence.
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
        // World lens over held lens (tan of half fields of view): the same angle lands this much further out on the held
        // layer, so the model turns further by it to keep its sights on the crosshair (PlayerAim.ModelRotation).
        public float ModelScale { get; private set; } = 1f;

        // `zoom` multiplies the base field of view: 1 = none, 0.85 = 15% narrower, above 1 wider (the death sequence).
        // `heldZoom` does the same for the held model's lens (aiming only).
        public void Tick(float zoom, float heldZoom = 1f)
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

            float heldFov = _baseFov * Mathf.Clamp(heldZoom, MinScale, 1f);
            if (_firstPersonView != null)
                _firstPersonView.SetFieldOfView(heldFov);
            ModelScale = LensRatio(fov, heldFov);
        }

        public static float LensRatio(float worldFov, float heldFov) =>
            Mathf.Tan(worldFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(heldFov * 0.5f * Mathf.Deg2Rad);

        // A full scope's view covers the screen: the held model isn't drawn.
        public void SetHeldHidden(bool hidden)
        {
            if (_firstPersonView != null)
                _firstPersonView.HideHeld = hidden;
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
