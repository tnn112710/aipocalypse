using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // The dog's home in the yard: the door point in front of the opening (on the navmesh) and the bed inside, facing out.
    // The dog walks to the door, then is moved in by hand (its agent off) and lies on the bed with its head out.
    public sealed class DogHouse : MonoBehaviour
    {
        [SerializeField, Tooltip("On the navmesh in front of the opening, facing in")] Transform _door;
        [SerializeField, Tooltip("Where it lies inside, facing out of the opening")] Transform _bed;

        public Vector3 Door => _door != null ? _door.position : transform.position + transform.forward;
        public Vector3 Bed => _bed != null ? _bed.position : transform.position;
        public Quaternion BedFacing => _bed != null ? _bed.rotation : transform.rotation;

        public void Setup(Transform door, Transform bed)
        {
            _door = door;
            _bed = bed;
        }
    }
}
