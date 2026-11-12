using UnityEngine;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // Home of the herd: a box on the ground (local X × Z round this object) the animals stay in until the gate lets them
    // out, and come back into at the bell.
    [DisallowMultipleComponent]
    public sealed class AnimalPen : MonoBehaviour
    {
        [SerializeField, Tooltip("Width (local X) and depth (local Z) of the pen's inside (m)")] Vector2 _size = new(8f, 10f);
        [SerializeField] PenGate _gate;
        [SerializeField, Tooltip("Just outside the gate: where a called animal waits when it can't get in")] Transform _waitPoint;

        public PenGate Gate => _gate;
        public Vector3 Center => transform.position;
        // A circle inside the box, clear of the fence: where animals pick their spots.
        public float Radius => Mathf.Max(0.5f, Mathf.Min(_size.x, _size.y) * 0.5f - 1f);
        public Vector3 WaitPoint => _waitPoint != null ? _waitPoint.position : transform.position;
        // The rails and posts round it: the gate's parent (AnimalSetup builds the fence that way). Token bursts fly through them.
        public Transform Fence => _gate != null ? _gate.transform.parent : null;

        // Anywhere in the box, `margin` clear of the fence: the whole pen, where Radius only reaches round the middle.
        public Vector3 RandomPoint(float margin)
        {
            float x = Mathf.Max(0f, _size.x * 0.5f - margin), z = Mathf.Max(0f, _size.y * 0.5f - margin);
            return transform.TransformPoint(new Vector3(Random.Range(-x, x), 0f, Random.Range(-z, z)));
        }

        public bool Contains(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) <= _size.x * 0.5f && Mathf.Abs(local.z) <= _size.y * 0.5f;
        }

        public void Setup(Vector2 size, PenGate gate, Transform waitPoint)
        {
            _size = size;
            _gate = gate;
            _waitPoint = waitPoint;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.7f, 0.2f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up * 0.5f, new Vector3(_size.x, 1f, _size.y));
        }
#endif
    }
}
