using Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // Yaw turns the body, pitch tilts the camera (through the camera rig). A view kick (gun recoil) is added on top of the
    // pitch and springs back.
    public sealed class PlayerLook
    {
        readonly Transform _body;
        readonly PlayerCameraRig _rig;
        readonly PlayerCameraTuning _tuning;
        float _pitch;
        ViewKick _kick;

        public PlayerLook(Transform body, PlayerCameraRig rig, PlayerCameraTuning tuning)
        {
            _body = body;
            _rig = rig;
            _tuning = tuning;
            _pitch = rig.InitialPitch;
        }

        // Degrees, positive = down.
        public float Pitch => _pitch;

        // Snaps the pitch (after a cutscene took the camera) and drops any view kick.
        public void ResetPitch(float pitch)
        {
            _pitch = Mathf.Clamp(pitch, _tuning.MinPitch, _tuning.MaxPitch);
            _kick = default;
            Apply();
        }

        public void Tick(Vector2 lookDegrees)
        {
            if (lookDegrees.x != 0f)
                _body.Rotate(0f, lookDegrees.x, 0f, Space.Self);

            if (lookDegrees.y != 0f)
                _pitch = Mathf.Clamp(_pitch - lookDegrees.y, _tuning.MinPitch, _tuning.MaxPitch);
            Apply();
        }

        // A turn that stays (recoil the player pulls against): x = pitch (negative = up), y = yaw.
        public void Turn(Vector2 euler)
        {
            if (euler.y != 0f)
                _body.Rotate(0f, euler.y, 0f, Space.Self);
            if (euler.x != 0f)
                _pitch = Mathf.Clamp(_pitch + euler.x, _tuning.MinPitch, _tuning.MaxPitch);
        }

        // After equipment ticks, so this frame's recoil shows this frame.
        public void SetKick(in ViewKick kick)
        {
            _kick = kick;
            Apply();
        }

        void Apply() => _rig.SetLook(_pitch, _kick);
    }
}
