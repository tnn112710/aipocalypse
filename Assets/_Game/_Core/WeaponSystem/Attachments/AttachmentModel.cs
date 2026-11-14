using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // Root of a part's model (AttachmentDefinition.Model), spawned on a gun's mount. Marks the points the gun uses: where
    // shots leave a muzzle part, an optic's sight line, where the support hand holds a grip, and lights and lasers that
    // work in hand.
    [DisallowMultipleComponent]
    public sealed class AttachmentModel : MonoBehaviour
    {
        [SerializeField, Tooltip("Muzzle parts: shots and the flash leave here, along its blue (Z) axis")] Transform _tip;
        [SerializeField, Tooltip("Optics: a point on the sight line (the lens centre), blue axis along it")] Transform _sight;
        [SerializeField, Tooltip("Tube optics: a disc just inside the rear opening that writes only depth (Optics/SightWindow) and " +
            "draws the reticle. On near full aim (HeldGun), it hides the tube's inside walls and the gun behind it, so the target " +
            "shows through the whole opening. Off in the prefab")] Renderer _sightWindow;
        [SerializeField, Tooltip("Grips: where the support hand holds, as a HandGrip would sit (Z from little finger to index). " +
            "Read by the gun's animation; the hands don't use it yet")] Transform _handGrip;
        [SerializeField, Tooltip("On only while the gun is in hand and its light is switched on (flashlights)")] Light[] _lights = System.Array.Empty<Light>();
        [SerializeField, Tooltip("Lasers: where the beam leaves, along its blue (Z) axis")] Transform _laser;
        [SerializeField, Tooltip("Lasers: the dot drawn where the beam lands. In hand it moves out of the model onto the world's " +
            "layer (the held layer draws with another field of view)")] Renderer _laserDot;
        [SerializeField, Min(1f), Tooltip("Lasers: metres the beam reaches")] float _laserRange = 80f;
        [SerializeField, Min(0f), Tooltip("Lasers: the dot's width per metre away, so it stays about the same size on screen")]
        float _dotSize = 0.02f;
        [SerializeField, Min(0f), Tooltip("Lasers: metres the dot sits off the surface towards the eye, so it never sinks in")]
        float _dotLift = 0.03f;

        bool _lit;

        public Transform Tip => _tip;
        public Transform Sight => _sight;
        public Renderer SightWindow => _sightWindow;
        public Transform HandGrip => _handGrip;
        // Something the gun light key switches: a light or a laser.
        public bool HasLights => _lights.Length > 0 || _laser != null;
        public bool HasLaser => _laser != null && _laserDot != null;
        // Where the beam leaves (the gun aims it like a shot from here).
        public Transform LaserOrigin => _laser;

        public void SetLights(bool on)
        {
            _lit = on;
            foreach (Light light in _lights)
                if (light != null)
                    light.enabled = on;
            if (!on)
                HideDot();
        }

        // Lasers, in hand, once the gun is in its final place for the frame: puts the dot where the beam (aimed by the gun
        // along its shot line) lands, facing the eye. Nothing in reach (or switched off): no dot.
        public void AimLaser(Vector3 origin, Vector3 direction, int layers, Transform eye)
        {
            if (!HasLaser)
                return;
            if (!_lit || !Physics.Raycast(origin, direction, out RaycastHit hit, _laserRange, layers, QueryTriggerInteraction.Ignore))
            {
                HideDot();
                return;
            }
            Transform dot = _laserDot.transform;
            if (dot.parent != null)
            {
                dot.SetParent(null, false);
                dot.gameObject.layer = 0;
            }
            Vector3 toEye = eye.position - hit.point;
            float distance = toEye.magnitude;
            Vector3 position = hit.point + toEye / Mathf.Max(distance, 0.001f) * Mathf.Min(_dotLift, distance * 0.5f);
            dot.SetPositionAndRotation(position, Quaternion.LookRotation(-toEye));
            dot.localScale = Vector3.one * (distance * _dotSize);
            _laserDot.enabled = true;
        }

        void HideDot()
        {
            if (_laserDot != null)
                _laserDot.enabled = false;
        }

        // The dot may sit outside the model: it goes with the gun.
        void OnDisable() => HideDot();

        void OnDestroy()
        {
            if (_laserDot != null && _laserDot.transform.parent == null)
                Destroy(_laserDot.gameObject);
        }
    }
}
