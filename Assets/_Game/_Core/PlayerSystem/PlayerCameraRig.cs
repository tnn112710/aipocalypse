using MB13.Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // Sole writer of the player's camera transforms: composes look pitch and view kick (pitch pivot), head motion (camera root)
    // and a pose sequence that overrides both. Applied as each share changes, so mid-frame aiming sees the right eye.
    public sealed class PlayerCameraRig
    {
        readonly Transform _root;
        readonly Transform _pitchPivot;
        readonly Quaternion _rootRestRotation;
        readonly Vector3 _pivotRest;
        readonly Vector3 _rootRest;
        float _pitch;
        ViewKick _kick;
        Vector3 _headOffset;
        float _headPitch;
        bool _overridden;
        Vector3 _overridePosition;
        Quaternion _overrideRotation = Quaternion.identity;

        // `eyeHeight` above the feet; 0 keeps the camera root where the prefab put it.
        public PlayerCameraRig(Transform root, Transform pitchPivot, float eyeHeight)
        {
            _root = root;
            _pitchPivot = pitchPivot;
            _rootRest = root.localPosition;
            if (eyeHeight > 0f)
                _rootRest.y = eyeHeight;
            _rootRestRotation = root.localRotation;
            _pivotRest = pitchPivot.localPosition;
            _pitch = Mathf.DeltaAngle(0f, pitchPivot.localEulerAngles.x);
            Apply();
        }

        // The look pitch the prefab starts with (degrees, positive = down).
        public float InitialPitch => _pitch;
        // Where the camera root is now, head motion included, in the body's space.
        public Vector3 RootPosition => _root.localPosition;

        public void SetLook(float pitch, in ViewKick kick)
        {
            _pitch = pitch;
            _kick = kick;
            Apply();
        }

        // Offset from the eye's rest position and a nod (degrees, positive = down).
        public void SetHeadMotion(Vector3 offset, float pitch)
        {
            _headOffset = offset;
            _headPitch = pitch;
            Apply();
        }

        // A pose sequence has the camera: the root goes exactly here (body space), look and head motion wait until Release.
        public void Override(Vector3 position, Quaternion rotation)
        {
            _overridden = true;
            _overridePosition = position;
            _overrideRotation = rotation;
            Apply();
        }

        public void ReleaseOverride()
        {
            if (!_overridden)
                return;
            _overridden = false;
            Apply();
        }

        void Apply()
        {
            if (_overridden)
            {
                _root.localPosition = _overridePosition;
                _root.localRotation = _overrideRotation;
                _pitchPivot.localRotation = Quaternion.identity;
                _pitchPivot.localPosition = _pivotRest;
                return;
            }
            _root.localPosition = _rootRest + _headOffset;
            _root.localRotation = _rootRestRotation * Quaternion.Euler(_headPitch, 0f, 0f);
            _pitchPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f) * Quaternion.Euler(_kick.Euler);
            _pitchPivot.localPosition = _pivotRest + _kick.Offset;
        }
    }
}
