using UnityEngine;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // Where released animals graze: a disc on the meadow. An animal standing in it earns at its roaming rate.
    [DisallowMultipleComponent]
    public sealed class AnimalPasture : MonoBehaviour
    {
        [SerializeField, Min(1f)] float _radius = 20f;

        public Vector3 Center => transform.position;
        public float Radius => _radius;

        public bool Contains(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= _radius * _radius;
        }

        public void Setup(float radius) => _radius = radius;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            UnityEditor.Handles.color = new Color(0.4f, 0.9f, 0.3f, 0.8f);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, _radius);
        }
#endif
    }
}
