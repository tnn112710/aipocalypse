using UnityEngine;

namespace ControlRigPro
{
    public class WaveMover : MonoBehaviour
    {
        [Header("Wave Settings")]
        public float amplitude = 1f;
        public float frequency = 1f;
        public float speed = 1f;
        
        public enum WaveAxis { X, Y, Z }
        
        [Header("Wave Direction")]
        public WaveAxis primaryAxis = WaveAxis.Y;
        public WaveAxis secondaryAxis = WaveAxis.X;
        
        [Header("Options")]
        public bool useSecondaryWave = false;
        public float secondaryAmplitude = 0.5f;
        public float secondaryFrequency = 2f;
        public float phaseOffset = 0f;
        
        [Header("Visual Options")]
        public bool rotateAlongWave = false;
        public float tiltAngle = 15f;
        public bool smoothRotation = true;
        
        private Vector3 startPosition;
        private float elapsedTime;
        private Vector3 previousPosition;
        
        private void Start()
        {
            startPosition = transform.position;
            elapsedTime = phaseOffset;
            previousPosition = transform.position;
        }
        
        private void Update()
        {
            previousPosition = transform.position;
            elapsedTime += Time.deltaTime * speed;
            
            Vector3 waveOffset = CalculateWaveOffset(elapsedTime);
            transform.position = startPosition + waveOffset;
            
            if (rotateAlongWave)
            {
                UpdateWaveRotation();
            }
        }
        
        private void UpdateWaveRotation()
        {
            float waveDerivative = Mathf.Cos(elapsedTime * frequency * Mathf.PI * 2f) * amplitude * frequency * Mathf.PI * 2f;
            float rotationAngle = Mathf.Atan(waveDerivative) * Mathf.Rad2Deg * (tiltAngle / 90f);
            
            Vector3 rotationAxis = Vector3.right;
            if (primaryAxis == WaveAxis.X)
                rotationAxis = Vector3.forward;
            else if (primaryAxis == WaveAxis.Z)
                rotationAxis = Vector3.right;
            
            Quaternion targetRotation = Quaternion.AngleAxis(rotationAngle, rotationAxis);
            
            if (smoothRotation)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            }
            else
            {
                transform.rotation = targetRotation;
            }
        }
        
        private Vector3 CalculateWaveOffset(float time)
        {
            Vector3 offset = Vector3.zero;
            
            float primaryWave = Mathf.Sin(time * frequency * Mathf.PI * 2f) * amplitude;
            offset = ApplyWaveToAxis(offset, primaryAxis, primaryWave);
            
            if (useSecondaryWave)
            {
                float secondaryWave = Mathf.Sin(time * secondaryFrequency * Mathf.PI * 2f) * secondaryAmplitude;
                offset = ApplyWaveToAxis(offset, secondaryAxis, secondaryWave);
            }
            
            return offset;
        }
        
        private Vector3 ApplyWaveToAxis(Vector3 currentOffset, WaveAxis axis, float waveValue)
        {
            switch (axis)
            {
                case WaveAxis.X:
                    currentOffset.x += waveValue;
                    break;
                case WaveAxis.Y:
                    currentOffset.y += waveValue;
                    break;
                case WaveAxis.Z:
                    currentOffset.z += waveValue;
                    break;
            }
            return currentOffset;
        }
        
        public void ResetWave()
        {
            elapsedTime = phaseOffset;
            transform.position = startPosition;
        }
        
        public void SetAmplitude(float newAmplitude)
        {
            amplitude = newAmplitude;
        }
        
        public void SetFrequency(float newFrequency)
        {
            frequency = Mathf.Max(0.01f, newFrequency);
        }
        
        public void SetSpeed(float newSpeed)
        {
            speed = newSpeed;
        }
        
        public void SetStartPosition(Vector3 newStart)
        {
            startPosition = newStart;
        }
        
        public void EnableSecondaryWave(bool enable)
        {
            useSecondaryWave = enable;
        }
        
        private void OnDrawGizmosSelected()
        {
            Vector3 origin = Application.isPlaying ? startPosition : transform.position;
            Gizmos.color = Color.cyan;
            
            int segments = 50;
            Vector3 previousPoint = origin;
            
            for (int i = 0; i <= segments; i++)
            {
                float t = (i / (float)segments) * Mathf.PI * 4f;
                Vector3 wavePoint = origin + CalculateWaveOffset(t / (frequency * Mathf.PI * 2f));
                
                if (i > 0)
                {
                    Gizmos.DrawLine(previousPoint, wavePoint);
                }
                previousPoint = wavePoint;
            }
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(origin, 0.1f);
            
            Gizmos.color = Color.green;
            Vector3 maxPoint = origin;
            maxPoint = ApplyWaveToAxis(maxPoint, primaryAxis, amplitude);
            Gizmos.DrawLine(origin, maxPoint);
            
            if (useSecondaryWave)
            {
                Gizmos.color = Color.blue;
                Vector3 secondaryMax = origin;
                secondaryMax = ApplyWaveToAxis(secondaryMax, secondaryAxis, secondaryAmplitude);
                Gizmos.DrawLine(origin, secondaryMax);
            }
        }
    }
}