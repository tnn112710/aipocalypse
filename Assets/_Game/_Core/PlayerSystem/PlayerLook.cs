using Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // Yaw turns the body, pitch tilts the camera pivot. A view kick (gun recoil) is added on top of the pitch and springs back.
    public sealed class PlayerLook
    {
        readonly Transform _body;
        readonly Transform _pitchPivot;
        readonly PlayerTuning _tuning;
        readonly Vector3 _pivotPosition;
        float _pitch;
        ViewKick _kick;

        public float Pitch => _pitch;

        public PlayerLook(Transform body, Transform pitchPivot, PlayerTuning tuning)
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
