using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Scene component: the fenced home yard the robots never leave, as a box seen from above (this object's position and
    // turn; scale ignored). Whistled from outside, a robot comes to the nearest point inside it.
    public sealed class RobotYard : MonoBehaviour
    {
        [SerializeField, Tooltip("Half the yard's width (x) and depth (z), in this object's space (m)")] Vector3 _halfSize = new(17f, 2f, 13.5f);

        public Matrix4x4 WorldToLocal => Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one).inverse;
        public Vector3 HalfSize => _halfSize;

        public bool Contains(Vector3 point, float margin = 0f) => YardMath.Contains(WorldToLocal, _halfSize, point, margin);
        public Vector3 ClosestInside(Vector3 point, float margin) => YardMath.ClosestInside(WorldToLocal, _halfSize, point, margin);

        public void Setup(Vector3 halfSize) => _halfSize = halfSize;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, _halfSize * 2f);
        }
#endif
    }
}
