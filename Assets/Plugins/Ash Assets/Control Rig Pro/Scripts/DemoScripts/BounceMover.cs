using UnityEngine;

namespace ControlRigPro
{
    public class BounceMover : MonoBehaviour
    {
        [Header("Bounce Settings")]
        public float bounceHeight = 2f;
        public float bounceSpeed = 1f;
        public AnimationCurve bounceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Bounce Direction")]
        public Vector3 bounceDirection = Vector3.up;

        [Header("Options")]
        public bool autoNormalizeDirection = true;
        public bool useSquashStretch = false;
        public float squashAmount = 0.2f;
        public float stretchAmount = 0.3f;

        [Header("Rotation Options")]
        public bool spinWhileBouncing = false;
        public Vector3 spinAxis = Vector3.up;
        public float spinSpeed = 180f;
        public bool tiltOnBounce = false;
        public float tiltAmount = 15f;

        private Vector3 startPosition;
        private Vector3 startScale;
        private Quaternion startRotation;
        private float bounceTime;
        private bool isGoingUp = true;
        private float currentSpinAngle;

        private void Start()
        {
            startPosition = transform.position;
            startScale = transform.localScale;
            startRotation = transform.rotation;
            currentSpinAngle = 0f;

            if (autoNormalizeDirection)
            {
                bounceDirection = bounceDirection.normalized;
            }
        }

        private void Update()
        {
            UpdateBounce();

            if (useSquashStretch)
            {
                UpdateSquashStretch();
            }

            UpdateRotation();
        }

        private void UpdateBounce()
        {
            bounceTime += Time.deltaTime * bounceSpeed;

            if (bounceTime >= 1f)
            {
                bounceTime = 0f;
                isGoingUp = !isGoingUp;
            }

            float curveValue = bounceCurve.Evaluate(bounceTime);

            if (!isGoingUp)
            {
                curveValue = 1f - curveValue;
            }

            Vector3 offset = bounceDirection * (curveValue * bounceHeight);
            transform.position = startPosition + offset;
        }

        private void UpdateSquashStretch()
        {
            Vector3 scale = startScale;
            float curveValue = bounceCurve.Evaluate(bounceTime);

            if (isGoingUp)
            {
                float stretchFactor = 1f + (stretchAmount * curveValue);
                float squashFactor = 1f - (squashAmount * 0.5f * curveValue);

                if (bounceDirection == Vector3.up || bounceDirection == Vector3.down)
                {
                    scale.y *= stretchFactor;
                    scale.x *= squashFactor;
                    scale.z *= squashFactor;
                }
                else if (bounceDirection == Vector3.right || bounceDirection == Vector3.left)
                {
                    scale.x *= stretchFactor;
                    scale.y *= squashFactor;
                    scale.z *= squashFactor;
                }
                else if (bounceDirection == Vector3.forward || bounceDirection == Vector3.back)
                {
                    scale.z *= stretchFactor;
                    scale.x *= squashFactor;
                    scale.y *= squashFactor;
                }
            }
            else
            {
                float impactFactor = 1f - curveValue;

                if (impactFactor < 0.2f)
                {
                    float squashFactor = 1f - (squashAmount * (1f - impactFactor * 5f));
                    float stretchFactor = 1f + (squashAmount * 0.5f * (1f - impactFactor * 5f));

                    if (bounceDirection == Vector3.up || bounceDirection == Vector3.down)
                    {
                        scale.y *= squashFactor;
                        scale.x *= stretchFactor;
                        scale.z *= stretchFactor;
                    }
                    else if (bounceDirection == Vector3.right || bounceDirection == Vector3.left)
                    {
                        scale.x *= squashFactor;
                        scale.y *= stretchFactor;
                        scale.z *= stretchFactor;
                    }
                    else if (bounceDirection == Vector3.forward || bounceDirection == Vector3.back)
                    {
                        scale.z *= squashFactor;
                        scale.x *= stretchFactor;
                        scale.y *= stretchFactor;
                    }
                }
            }

            transform.localScale = scale;
        }

        private void UpdateRotation()
        {
            Quaternion rotation = startRotation;

            if (spinWhileBouncing)
            {
                currentSpinAngle += spinSpeed * Time.deltaTime;
                if (currentSpinAngle > 360f)
                    currentSpinAngle -= 360f;

                rotation *= Quaternion.AngleAxis(currentSpinAngle, spinAxis.normalized);
            }

            if (tiltOnBounce)
            {
                float tiltAngle = 0f;
                if (isGoingUp)
                {
                    tiltAngle = Mathf.Sin(bounceTime * Mathf.PI) * tiltAmount;
                }
                else
                {
                    tiltAngle = -Mathf.Sin(bounceTime * Mathf.PI) * tiltAmount * 0.5f;
                }

                Vector3 tiltAxis = Vector3.Cross(bounceDirection, Vector3.forward);
                if (tiltAxis.magnitude < 0.001f)
                    tiltAxis = Vector3.Cross(bounceDirection, Vector3.right);

                rotation *= Quaternion.AngleAxis(tiltAngle, tiltAxis.normalized);
            }

            transform.rotation = rotation;
        }

        public void ResetBounce()
        {
            bounceTime = 0f;
            isGoingUp = true;
            currentSpinAngle = 0f;
            transform.position = startPosition;
            transform.localScale = startScale;
            transform.rotation = startRotation;
        }

        public void SetBounceHeight(float newHeight)
        {
            bounceHeight = Mathf.Max(0f, newHeight);
        }

        public void SetBounceSpeed(float newSpeed)
        {
            bounceSpeed = Mathf.Max(0f, newSpeed);
        }

        public void SetBounceDirection(Vector3 newDirection)
        {
            bounceDirection = autoNormalizeDirection ? newDirection.normalized : newDirection;
        }

        public void SetStartPosition(Vector3 newPosition)
        {
            startPosition = newPosition;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = Application.isPlaying ? startPosition : transform.position;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(origin, 0.1f);

            Gizmos.color = Color.green;
            Vector3 maxBouncePoint = origin + (bounceDirection.normalized * bounceHeight);
            Gizmos.DrawLine(origin, maxBouncePoint);
            Gizmos.DrawWireSphere(maxBouncePoint, 0.1f);

            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            int segments = 20;
            Vector3 previousPoint = origin;

            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float height = bounceCurve.Evaluate(t) * bounceHeight;
                Vector3 point = origin + (bounceDirection.normalized * height);
                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }
        }
    }
}