using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Scene component: where a robot lives. It idles here facing this object's forward and strolls within Radius, never
    // into a NoGo box (a doorway it would block); during a raid it hides at Shelter (or here).
    public sealed class RobotSpot : MonoBehaviour
    {
        [SerializeField, Min(0.5f), Tooltip("Strolls stay within this far of the spot (m)")] float _radius = 4f;
        [SerializeField, Tooltip("Boxes in this object's space it never stands in (a doorway)")] Bounds[] _noGo = System.Array.Empty<Bounds>();
        [SerializeField, Tooltip("Where it hides during a raid (facing its forward); none = here")] Transform _shelter;

        public Vector3 Center => transform.position;
        public Vector3 Facing => transform.forward;
        public float Radius => _radius;
        public Vector3 Shelter => _shelter != null ? _shelter.position : transform.position;
        public Vector3 ShelterFacing => _shelter != null ? _shelter.forward : transform.forward;

        // Within its radius and outside every NoGo box.
        public bool Allows(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= _radius * _radius && !InNoGo(point);
        }

        public bool InNoGo(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            foreach (Bounds box in _noGo)
            {
                local.y = box.center.y;
                if (box.Contains(local))
                    return true;
            }
            return false;
        }

        public void Setup(float radius, Bounds[] noGo, Transform shelter = null)
        {
            _radius = radius;
            _noGo = noGo;
            _shelter = shelter;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward);
            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, _radius);
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.7f);
            Gizmos.matrix = transform.localToWorldMatrix;
            foreach (Bounds box in _noGo)
                Gizmos.DrawWireCube(box.center, box.size);
        }
#endif
    }
}
