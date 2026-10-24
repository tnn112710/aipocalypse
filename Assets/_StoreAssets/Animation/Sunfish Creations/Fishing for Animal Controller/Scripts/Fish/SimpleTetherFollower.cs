using UnityEngine;

namespace SunfishCreations
{
    [RequireComponent(typeof(Rigidbody))]
    public class SimpleTetherFollower : MonoBehaviour
    {
        public Transform followTarget;

        [Header("Response")]
        [Range(0, 50)] public float stiffness = 20f;
        [Range(0, 10)] public float damping = 4f;

        private Vector3 velocity = Vector3.zero;
        private Vector3 initialWorldOffset;
        private Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;

            rb.freezeRotation = true;

            if (followTarget != null)
            {
                initialWorldOffset = transform.position - followTarget.position;
            }
        }

        void FixedUpdate()
        {
            if (followTarget == null) return;

            Vector3 targetPosition = followTarget.position + initialWorldOffset;

            // Damped spring toward target
            Vector3 error = targetPosition - transform.position;
            Vector3 acceleration = error * stiffness - velocity * damping;
            velocity += acceleration * Time.fixedDeltaTime;

            // Move via position
            transform.position += velocity * Time.fixedDeltaTime;
        }
    }
}