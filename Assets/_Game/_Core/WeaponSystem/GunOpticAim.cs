using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // Aiming through a fitted optic: a copy of the gun's aim pose moved so the optic's sight point lands on the iron sights'
    // line at its EyeDistance from the eye, the full scope's eye offset, and a tube optic's sight window near full aim.
    public sealed class GunOpticAim
    {
        // A tube optic's window hides its inside walls only once the eye is nearly behind it: from further off the walls are
        // what the eye would really see.
        const float SightWindowFrom = 0.6f;
        // The scope's tilt counts over this lever (m) in its eye offset.
        const float TiltLever = 0.1f;

        readonly Transform _aimPose;
        Transform _opticAim;
        bool _throughOptic;
        Renderer _shownWindow;

        public GunOpticAim(Transform aimPose) => _aimPose = aimPose;

        // The pose to aim with: the optic's copy when one is fitted, else the gun's own.
        public Transform AimPose => _throughOptic ? _opticAim : _aimPose;

        // Measured in `model`'s space (the pose pivot's at rest). With no optic (or its sight is the irons), aims through the
        // irons. Guns with far-forward irons are held closer, so every optic sits at its own EyeDistance.
        public void Place(OpticSettings optic, Transform sight, Transform ironSight, Transform model, Transform eye)
        {
            _throughOptic = optic != null && sight != null && ironSight != null && sight != ironSight && _aimPose != null;
            if (!_throughOptic)
                return;

            Vector3 sightPoint = model.InverseTransformPoint(sight.position);
            Vector3 iron = model.InverseTransformPoint(ironSight.position);
            Vector3 along = model.InverseTransformDirection(ironSight.forward);
            if (Mathf.Abs(along.z) > 0.01f)
                iron += along * ((sightPoint.z - iron.z) / along.z);
            Vector3 offset = iron - sightPoint;
            offset.z = 0f;
            if (eye != null)
            {
                // Depth from the eye is linear in offset.z: solve for the optic's distance.
                Matrix4x4 eyeFromPoses = eye.worldToLocalMatrix * _aimPose.parent.localToWorldMatrix;
                Quaternion rotation = _aimPose.localRotation;
                float depth = eyeFromPoses.MultiplyPoint(_aimPose.localPosition + rotation * (offset + sightPoint)).z;
                float perMetre = eyeFromPoses.MultiplyVector(rotation * Vector3.forward).z;
                if (perMetre > 0.1f)
                    offset.z = (optic.EyeDistance - depth) / perMetre;
            }

            if (_opticAim == null)
            {
                _opticAim = new GameObject("AimOptic").transform;
                _opticAim.SetParent(_aimPose.parent, false);
            }
            _opticAim.SetLocalPositionAndRotation(_aimPose.localPosition + _aimPose.localRotation * offset, _aimPose.localRotation);
        }

        // How far the optic's sight line is off the eye's aim line, in shares of its eye box: the sight point's offset plus
        // the scope's tilt over a short lever (model sway, gun kick, the last of raising it).
        public static Vector2 EyeOffset(OpticSettings optic, Transform sight, Transform eye, Quaternion aimOffset)
        {
            if (sight == null || eye == null)
                return default;
            Quaternion toAim = Quaternion.Inverse(eye.rotation * aimOffset);
            Vector3 point = toAim * (sight.position - eye.position);
            Vector3 along = toAim * sight.forward;
            var offset = new Vector2(point.x + along.x * TiltLever, point.y + along.y * TiltLever);
            return Vector2.ClampMagnitude(offset / optic.EyeBox, 2f);
        }

        // Shows `window` (the fitted tube optic's, or null) past SightWindowFrom of `aimWeight`; 0 hides it.
        public void ShowWindow(Renderer window, float aimWeight)
        {
            if (aimWeight <= SightWindowFrom)
                window = null;
            if (window == _shownWindow)
                return;
            if (_shownWindow != null)
                _shownWindow.enabled = false;
            _shownWindow = window;
            if (window != null)
                window.enabled = true;
        }
    }
}
