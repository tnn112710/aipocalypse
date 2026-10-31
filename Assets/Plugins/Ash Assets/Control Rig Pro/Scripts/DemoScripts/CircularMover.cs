using UnityEngine;

namespace ControlRigPro
{
    public class CircularMover : MonoBehaviour
    {
        [Header("Circle Settings")]
        public float radius = 2f;
        public float rotationSpeed = 50f;
        
        public enum RotationPlane { XZ_Horizontal, XY_Vertical, YZ_Vertical }
        
        [Header("Movement Plane")]
        public RotationPlane rotationPlane = RotationPlane.XZ_Horizontal;
        
        [Header("Options")]
        public bool clockwise = true;
        public float startAngle = 0f;
        public bool autoStart = true;
        
        [Header("Rotation Options")]
        public bool faceMovementDirection = false;
        public bool maintainUpDirection = true;
        public float rotationSmoothness = 5f;
        
        [Header("Raycast Ground Following")]
        public bool useRaycastGroundHeight = false;
        public float raycastDistance = 10f;
        public float groundOffset = 0f;
        public LayerMask groundLayers = -1;
        public bool smoothHeightTransition = true;
        public float heightSmoothSpeed = 5f;
        
        [Header("Axis Control")]
        public bool lockXAxis = false;
        public bool lockYAxis = false;
        public bool lockZAxis = false;
        public Vector3 lockedAxisValues = Vector3.zero;
        
        private Vector3 centerPosition;
        private float currentAngle;
        private bool isMoving;
        private Vector3 previousPosition;
        
        private void Start()
        {
            centerPosition = transform.position;
            currentAngle = startAngle;
            isMoving = autoStart;
            previousPosition = transform.position;
            
            UpdatePosition();
        }
        
        private void Update()
        {
            if (!isMoving)
                return;
            
            float angleChange = rotationSpeed * Time.deltaTime;
            if (!clockwise)
                angleChange = -angleChange;
            
            currentAngle += angleChange;
            
            if (currentAngle > 360f)
                currentAngle -= 360f;
            else if (currentAngle < 0f)
                currentAngle += 360f;
            
            UpdatePosition();
        }
        
        private void UpdatePosition()
        {
            previousPosition = transform.position;
            float radians = currentAngle * Mathf.Deg2Rad;
            Vector3 offset = Vector3.zero;
            
            switch (rotationPlane)
            {
                case RotationPlane.XZ_Horizontal:
                    offset.x = Mathf.Cos(radians) * radius;
                    offset.z = Mathf.Sin(radians) * radius;
                    break;
                    
                case RotationPlane.XY_Vertical:
                    offset.x = Mathf.Cos(radians) * radius;
                    offset.y = Mathf.Sin(radians) * radius;
                    break;
                    
                case RotationPlane.YZ_Vertical:
                    offset.y = Mathf.Sin(radians) * radius;
                    offset.z = Mathf.Cos(radians) * radius;
                    break;
            }
            
            Vector3 newPosition = centerPosition + offset;
            
            if (useRaycastGroundHeight)
            {
                newPosition = ApplyGroundHeight(newPosition);
            }
            
            newPosition = ApplyAxisLocks(newPosition);
            
            transform.position = newPosition;
            
            if (faceMovementDirection)
            {
                UpdateFacingDirection();
            }
        }
        
        private Vector3 ApplyGroundHeight(Vector3 position)
        {
            Vector3 rayStart = position + Vector3.up * raycastDistance;
            RaycastHit hit;
            
            if (Physics.Raycast(rayStart, Vector3.down, out hit, raycastDistance * 2f, groundLayers))
            {
                float targetHeight = hit.point.y + groundOffset;
                
                if (smoothHeightTransition)
                {
                    float currentHeight = transform.position.y;
                    position.y = Mathf.Lerp(currentHeight, targetHeight, heightSmoothSpeed * Time.deltaTime);
                }
                else
                {
                    position.y = targetHeight;
                }
            }
            
            return position;
        }
        
        private Vector3 ApplyAxisLocks(Vector3 position)
        {
            if (lockXAxis)
                position.x = lockedAxisValues.x;
            
            if (lockYAxis)
                position.y = lockedAxisValues.y;
            
            if (lockZAxis)
                position.z = lockedAxisValues.z;
            
            return position;
        }
        
        private void UpdateFacingDirection()
        {
            Vector3 movementDirection = transform.position - previousPosition;
            
            if (maintainUpDirection && useRaycastGroundHeight)
            {
                movementDirection.y = 0;
            }
            
            if (movementDirection.magnitude > 0.001f)
            {
                Quaternion targetRotation;
                
                if (maintainUpDirection)
                {
                    targetRotation = Quaternion.LookRotation(movementDirection, Vector3.up);
                }
                else
                {
                    targetRotation = Quaternion.LookRotation(movementDirection);
                }
                
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothness * Time.deltaTime);
            }
        }
        
        public void StartMovement()
        {
            isMoving = true;
        }
        
        public void StopMovement()
        {
            isMoving = false;
        }
        
        public void ToggleMovement()
        {
            isMoving = !isMoving;
        }
        
        public void SetCenter(Vector3 newCenter)
        {
            centerPosition = newCenter;
            
            if (lockXAxis)
                centerPosition.x = lockedAxisValues.x;
            if (lockYAxis)
                centerPosition.y = lockedAxisValues.y;
            if (lockZAxis)
                centerPosition.z = lockedAxisValues.z;
            
            UpdatePosition();
        }
        
        public void ResetToStart()
        {
            currentAngle = startAngle;
            UpdatePosition();
        }
        
        public void SetRadius(float newRadius)
        {
            radius = Mathf.Max(0.1f, newRadius);
            UpdatePosition();
        }
        
        public void SetSpeed(float newSpeed)
        {
            rotationSpeed = newSpeed;
        }
        
        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? centerPosition : transform.position;
            Gizmos.color = Color.cyan;
            
            int segments = 32;
            Vector3 previousPoint = GetPointOnCircle(0, center);
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * 360f;
                Vector3 nextPoint = GetPointOnCircle(angle, center);
                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, 0.1f);
            
            if (useRaycastGroundHeight)
            {
                Gizmos.color = Color.green;
                Vector3 rayStart = transform.position + Vector3.up * raycastDistance;
                Gizmos.DrawLine(rayStart, rayStart + Vector3.down * raycastDistance * 2f);
                
                RaycastHit hit;
                if (Physics.Raycast(rayStart, Vector3.down, out hit, raycastDistance * 2f, groundLayers))
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(hit.point, 0.2f);
                }
            }
            
            if (lockXAxis || lockYAxis || lockZAxis)
            {
                Gizmos.color = Color.magenta;
                Vector3 lockIndicator = transform.position;
                
                if (lockXAxis)
                {
                    Gizmos.DrawLine(lockIndicator + Vector3.left * 0.5f, lockIndicator + Vector3.right * 0.5f);
                }
                if (lockYAxis)
                {
                    Gizmos.DrawLine(lockIndicator + Vector3.down * 0.5f, lockIndicator + Vector3.up * 0.5f);
                }
                if (lockZAxis)
                {
                    Gizmos.DrawLine(lockIndicator + Vector3.back * 0.5f, lockIndicator + Vector3.forward * 0.5f);
                }
            }
        }
        
        private Vector3 GetPointOnCircle(float angle, Vector3 center)
        {
            float radians = angle * Mathf.Deg2Rad;
            Vector3 offset = Vector3.zero;
            
            switch (rotationPlane)
            {
                case RotationPlane.XZ_Horizontal:
                    offset.x = Mathf.Cos(radians) * radius;
                    offset.z = Mathf.Sin(radians) * radius;
                    break;
                    
                case RotationPlane.XY_Vertical:
                    offset.x = Mathf.Cos(radians) * radius;
                    offset.y = Mathf.Sin(radians) * radius;
                    break;
                    
                case RotationPlane.YZ_Vertical:
                    offset.y = Mathf.Sin(radians) * radius;
                    offset.z = Mathf.Cos(radians) * radius;
                    break;
            }
            
            return center + offset;
        }
    }
}