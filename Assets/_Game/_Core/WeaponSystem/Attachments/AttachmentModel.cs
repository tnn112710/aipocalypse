using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // Root of a part's model (AttachmentDefinition.Model), spawned on a gun's mount. Marks the points the gun uses: where
    // shots leave a muzzle part, an optic's sight line, where the support hand holds a grip, and lights that work in hand.
    [DisallowMultipleComponent]
    public sealed class AttachmentModel : MonoBehaviour
    {
        [SerializeField, Tooltip("Muzzle parts: shots and the flash leave here, along its blue (Z) axis")] Transform _tip;
        [SerializeField, Tooltip("Optics: a point on the sight line (the lens centre), blue axis along it")] Transform _sight;
        [SerializeField, Tooltip("Grips: where the support hand holds, as a HandGrip would sit (Z from little finger to index). " +
            "Read by the gun's animation; the hands don't use it yet")] Transform _handGrip;
        [SerializeField, Tooltip("On only while the gun is in hand and its light is switched on (flashlights)")] Light[] _lights = System.Array.Empty<Light>();

        public Transform Tip => _tip;
        public Transform Sight => _sight;
        public Transform HandGrip => _handGrip;
        public bool HasLights => _lights.Length > 0;

        public void SetLights(bool on)
        {
            foreach (Light light in _lights)
                if (light != null)
                    light.enabled = on;
        }
    }
}
