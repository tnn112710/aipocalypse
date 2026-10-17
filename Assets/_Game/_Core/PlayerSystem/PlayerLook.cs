using Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // Yaw turns the body, pitch tilts the camera pivot. A view kick (gun recoil) is added on top of the pitch and springs back.
    public sealed class PlayerLook
    {
        readonly Transform _body;
        readonly Transform _pitchPivot;
        readonly PlayerCameraTuning _tuning;
        readonly Vector3 _pivotPosition;
        float _pitch;
        ViewKick _kick;

        public PlayerLook(Transform body, Transform pitchPivot, PlayerCameraTuning tuning)
        {
            _body = body;
            _pitchPivot = pitchPivot;
            _tuning = tuning;
            _pitch = Mathf.DeltaAngle(0f, pitchPivot.localEulerAngles.x);
            _pivotPosition = pitchPivot.localPosition;
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

        void Apply()
        {
            _pitchPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f) * Quaternion.Euler(_kick.Euler);
            _pitchPivot.localPosition = _pivotPosition + _kick.Offset;
        }
    }
}
