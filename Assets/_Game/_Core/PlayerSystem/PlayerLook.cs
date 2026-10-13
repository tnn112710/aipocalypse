using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // Yaw turns the body, pitch tilts the camera pivot.
    public sealed class PlayerLook
    {
        readonly Transform _body;
        readonly Transform _pitchPivot;
        readonly PlayerTuning _tuning;
        float _pitch;

        public float Pitch => _pitch;

        public PlayerLook(Transform body, Transform pitchPivot, PlayerTuning tuning)
        {
            _body = body;
            _pitchPivot = pitchPivot;
            _tuning = tuning;
            _pitch = Mathf.DeltaAngle(0f, pitchPivot.localEulerAngles.x);
        }

        public void Tick(Vector2 lookDegrees)
        {
            if (lookDegrees.x != 0f)
                _body.Rotate(0f, lookDegrees.x, 0f, Space.Self);

            if (lookDegrees.y != 0f)
            {
                _pitch = Mathf.Clamp(_pitch - lookDegrees.y, _tuning.MinPitch, _tuning.MaxPitch);
                _pitchPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }
    }
}
