using UnityEngine;

namespace ControlRigPro
{
    public class PendulumMover : MonoBehaviour
    {
        [Header("Pendulum Settings")]
        public float swingAngle = 45f;
        public float swingSpeed = 1f;
        public float pendulumLength = 2f;
        
        public enum SwingAxis { X, Z }
        
        [Header("Swing Axis")]
        public SwingAxis swingAxis = SwingAxis.Z;
        
        [Header("Options")]
        public bool startFromLeft = true;
        public bool useSmoothSwing = true;
        public float damping = 0f;
        
        [Header("Visual Options")]
        public bool rotateToPendulumAngle = true;
        public bool drawPendulumLine = false;
        public LineRenderer pendulumLineRenderer;
        
        private Vector3 pivotPoint;
        private float currentTime;
        private float currentAmplitude;
        
        private void Start()
        {
            pivotPoint = transform.position + Vector3.up * pendulumLength;
            currentAmplitude = swingAngle;
            
            if (startFromLeft)
                currentTime = Mathf.PI * 0.5f;
        }
        
        private void Update()
        {
            currentTime += Time.deltaTime * swingSpeed;
            
            if (damping > 0f)
            {
                currentAmplitude = Mathf.Lerp(currentAmplitude, 0f, damping * Time.deltaTime);
            }
            
            float angle;
            if (useSmoothSwing)
            {
                angle = Mathf.Sin(currentTime) * currentAmplitude;
            }
            else
            {
                angle = Mathf.PingPong(currentTime * 57.2958f, currentAmplitude * 2f) - currentAmplitude;
            }
            
            UpdatePendulumPosition(angle);
        }
        
        private void UpdatePendulumPosition(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            
            Vector3 offset = Vector3.down * pendulumLength;
            
            if (swingAxis == SwingAxis.X)
            {
                float sinAngle = Mathf.Sin(radians);
                float cosAngle = Mathf.Cos(radians);
                
                offset.x = sinAngle * pendulumLength;
                offset.y = -cosAngle * pendulumLength;
            }
            else
            {
                float sinAngle = Mathf.Sin(radians);
                float cosAngle = Mathf.Cos(radians);
                
                offset.z = sinAngle * pendulumLength;
                offset.y = -cosAngle * pendulumLength;
            }
            
            transform.position = pivotPoint + offset;
            
            if (rotateToPendulumAngle)
            {
                UpdateRotation(angle);
            }
            
            if (drawPendulumLine && pendulumLineRenderer != null)
            {
                UpdatePendulumLine();
            }
        }
        
        private void UpdateRotation(float angle)
        {
            Vector3 rotationEuler = transform.rotation.eulerAngles;
            
            if (swingAxis == SwingAxis.X)
            {
                rotationEuler.z = -angle;
            }
            else
            {
                rotationEuler.x = angle;
            }
            
            transform.rotation = Quaternion.Euler(rotationEuler);
        }
        
        private void UpdatePendulumLine()
        {
            pendulumLineRenderer.positionCount = 2;
            pendulumLineRenderer.SetPosition(0, pivotPoint);
            pendulumLineRenderer.SetPosition(1, transform.position);
        }
        
        public void SetPivotPoint(Vector3 newPivot)
        {
            pivotPoint = newPivot;
        }
        
        public void ResetSwing()
        {
            currentTime = startFromLeft ? Mathf.PI * 0.5f : 0f;
            currentAmplitude = swingAngle;
        }
        
        public void SetSwingAngle(float newAngle)
        {
            swingAngle = Mathf.Clamp(newAngle, 0f, 90f);
            currentAmplitude = swingAngle;
        }
        
        public void SetSwingSpeed(float newSpeed)
        {
            swingSpeed = Mathf.Max(0f, newSpeed);
        }
        
        public void ApplyImpulse(float force)
        {
            currentAmplitude = Mathf.Min(currentAmplitude + force, 90f);
        }
        
        private void OnDrawGizmosSelected()
        {
            Vector3 pivot = Application.isPlaying ? pivotPoint : transform.position + Vector3.up * pendulumLength;
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(pivot, 0.1f);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pivot, transform.position);
            
            Gizmos.color = Color.green;
            float maxAngle = Application.isPlaying ? currentAmplitude : swingAngle;
            
            for (int i = -1; i <= 1; i += 2)
            {
                float radians = maxAngle * i * Mathf.Deg2Rad;
                Vector3 endPoint = pivot;
                
                if (swingAxis == SwingAxis.X)
                {
                    endPoint += new Vector3(
                        Mathf.Sin(radians) * pendulumLength,
                        -Mathf.Cos(radians) * pendulumLength,
                        0f
                    );
                }
                else
                {
                    endPoint += new Vector3(
                        0f,
                        -Mathf.Cos(radians) * pendulumLength,
                        Mathf.Sin(radians) * pendulumLength
                    );
                }
                
                Gizmos.DrawLine(pivot, endPoint);
            }
        }
    }
}