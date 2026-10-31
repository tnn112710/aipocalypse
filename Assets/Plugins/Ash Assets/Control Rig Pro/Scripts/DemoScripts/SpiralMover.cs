using UnityEngine;

namespace ControlRigPro
{
    public class SpiralMover : MonoBehaviour
    {
        [Header("Spiral Settings")]
        public float initialRadius = 0.5f;
        public float radiusGrowthRate = 0.5f;
        public float rotationSpeed = 60f;
        public float verticalSpeed = 0.5f;
        
        public enum SpiralMode { Expanding, Contracting, Oscillating }
        
        [Header("Spiral Type")]
        public SpiralMode spiralMode = SpiralMode.Expanding;
        
        [Header("Limits")]
        public float maxRadius = 5f;
        public float minRadius = 0.1f;
        public float maxHeight = 10f;
        
        [Header("Options")]
        public bool resetAtMaxHeight = true;
        public bool smoothTransition = true;
        
        [Header("Rotation Options")]
        public bool faceCenter = false;
        public bool faceMovementDirection = false;
        public bool bankOnTurns = false;
        public float bankingAngle = 30f;
        
        private Vector3 centerPosition;
        private float currentRadius;
        private float currentAngle;
        private float currentHeight;
        private bool isExpanding = true;
        private Vector3 previousPosition;
        
        private void Start()
        {
            centerPosition = transform.position;
            currentRadius = initialRadius;
            currentAngle = 0f;
            currentHeight = 0f;
            previousPosition = transform.position;
            
            if (spiralMode == SpiralMode.Contracting)
            {
                currentRadius = maxRadius;
                isExpanding = false;
            }
        }
        
        private void Update()
        {
            UpdateSpiral();
            UpdatePosition();
        }
        
        private void UpdateSpiral()
        {
            currentAngle += rotationSpeed * Time.deltaTime;
            
            if (currentAngle >= 360f)
                currentAngle -= 360f;
            
            UpdateRadius();
            UpdateHeight();
        }
        
        private void UpdateRadius()
        {
            float radiusChange = radiusGrowthRate * Time.deltaTime;
            
            switch (spiralMode)
            {
                case SpiralMode.Expanding:
                    currentRadius += radiusChange;
                    if (currentRadius >= maxRadius)
                    {
                        if (resetAtMaxHeight && currentHeight >= maxHeight)
                        {
                            currentRadius = initialRadius;
                        }
                        else
                        {
                            currentRadius = maxRadius;
                        }
                    }
                    break;
                    
                case SpiralMode.Contracting:
                    currentRadius -= radiusChange;
                    if (currentRadius <= minRadius)
                    {
                        if (resetAtMaxHeight && currentHeight >= maxHeight)
                        {
                            currentRadius = maxRadius;
                        }
                        else
                        {
                            currentRadius = minRadius;
                        }
                    }
                    break;
                    
                case SpiralMode.Oscillating:
                    if (isExpanding)
                    {
                        currentRadius += radiusChange;
                        if (currentRadius >= maxRadius)
                        {
                            currentRadius = maxRadius;
                            isExpanding = false;
                        }
                    }
                    else
                    {
                        currentRadius -= radiusChange;
                        if (currentRadius <= minRadius)
                        {
                            currentRadius = minRadius;
                            isExpanding = true;
                        }
                    }
                    break;
            }
        }
        
        private void UpdateHeight()
        {
            currentHeight += verticalSpeed * Time.deltaTime;
            
            if (currentHeight >= maxHeight)
            {
                if (resetAtMaxHeight)
                {
                    currentHeight = 0f;
                    if (spiralMode == SpiralMode.Expanding)
                        currentRadius = initialRadius;
                    else if (spiralMode == SpiralMode.Contracting)
                        currentRadius = maxRadius;
                }
                else
                {
                    currentHeight = maxHeight;
                }
            }
        }
        
        private void UpdatePosition()
        {
            previousPosition = transform.position;
            float radians = currentAngle * Mathf.Deg2Rad;
            
            Vector3 offset = new Vector3(
                Mathf.Cos(radians) * currentRadius,
                currentHeight,
                Mathf.Sin(radians) * currentRadius
            );
            
            Vector3 targetPosition = centerPosition + offset;
            
            if (smoothTransition)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 5f);
            }
            else
            {
                transform.position = targetPosition;
            }
            
            UpdateRotation();
        }
        
        private void UpdateRotation()
        {
            if (faceCenter)
            {
                Vector3 directionToCenter = centerPosition - transform.position;
                directionToCenter.y = 0f;
                if (directionToCenter.magnitude > 0.001f)
                {
                    Quaternion lookRotation = Quaternion.LookRotation(-directionToCenter, Vector3.up);
                    
                    if (bankOnTurns)
                    {
                        float bankAngle = (rotationSpeed / 60f) * bankingAngle;
                        lookRotation *= Quaternion.Euler(0f, 0f, bankAngle);
                    }
                    
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
                }
            }
            else if (faceMovementDirection)
            {
                Vector3 movementDirection = transform.position - previousPosition;
                if (movementDirection.magnitude > 0.001f)
                {
                    Quaternion lookRotation = Quaternion.LookRotation(movementDirection, Vector3.up);
                    
                    if (bankOnTurns)
                    {
                        float bankAngle = (rotationSpeed / 60f) * bankingAngle;
                        lookRotation *= Quaternion.Euler(0f, 0f, -bankAngle);
                    }
                    
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
                }
            }
        }
        
        public void ResetSpiral()
        {
            currentAngle = 0f;
            currentHeight = 0f;
            
            switch (spiralMode)
            {
                case SpiralMode.Expanding:
                    currentRadius = initialRadius;
                    break;
                case SpiralMode.Contracting:
                    currentRadius = maxRadius;
                    break;
                case SpiralMode.Oscillating:
                    currentRadius = initialRadius;
                    isExpanding = true;
                    break;
            }
            
            UpdatePosition();
        }
        
        public void SetCenterPosition(Vector3 newCenter)
        {
            centerPosition = newCenter;
        }
        
        public void SetSpiralMode(SpiralMode newMode)
        {
            spiralMode = newMode;
            ResetSpiral();
        }
        
        public void SetRotationSpeed(float newSpeed)
        {
            rotationSpeed = newSpeed;
        }
        
        public void SetVerticalSpeed(float newSpeed)
        {
            verticalSpeed = Mathf.Max(0f, newSpeed);
        }
        
        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? centerPosition : transform.position;
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, 0.1f);
            
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            
            int spiralSegments = 100;
            float heightPerSegment = maxHeight / spiralSegments;
            float anglePerSegment = 720f / spiralSegments;
            
            Vector3 previousPoint = center;
            float drawRadius = initialRadius;
            
            for (int i = 1; i <= spiralSegments; i++)
            {
                float angle = anglePerSegment * i * Mathf.Deg2Rad;
                float height = heightPerSegment * i;
                
                if (spiralMode == SpiralMode.Expanding)
                {
                    drawRadius = Mathf.Lerp(initialRadius, maxRadius, i / (float)spiralSegments);
                }
                else if (spiralMode == SpiralMode.Contracting)
                {
                    drawRadius = Mathf.Lerp(maxRadius, minRadius, i / (float)spiralSegments);
                }
                
                Vector3 point = center + new Vector3(
                    Mathf.Cos(angle) * drawRadius,
                    height,
                    Mathf.Sin(angle) * drawRadius
                );
                
                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center + Vector3.up * maxHeight, new Vector3(maxRadius * 2f, 0.1f, maxRadius * 2f));
        }
    }
}