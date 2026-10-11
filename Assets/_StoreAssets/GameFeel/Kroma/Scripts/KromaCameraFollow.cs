using UnityEngine;

namespace FayLabs.Kroma
{
    [ExecuteAlways]
    public class KromaCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform targetCamera;
        [SerializeField] private Vector3 _offset; 
        [SerializeField] private bool _fixY;
        private float _initialY;
        private bool _initialized;
        private float _lastSearchTime;
        private const float SEARCH_INTERVAL = 0.5f;

        void Start()
        {
            Initialize();
        }

        void OnEnable()
        {
            // Ensure we re-initialize if re-enabled to catch any new camera or position changes if needed
            if (!_initialized) Initialize();
        }

        void Initialize()
        {
            // Auto-detect camera
            if (targetCamera == null && Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }

            // Capture initial offset (respects Spawner positioning)
            if (targetCamera != null)
            {
                _offset = transform.position - targetCamera.position;
                _initialY = transform.position.y;
                _initialized = true;
            }
        }

        void LateUpdate()
        {
            // Continuous auto-detection with interval (useful in Editor or if Camera spawns late)
            if (targetCamera == null)
            {
                if (Time.realtimeSinceStartup - _lastSearchTime > SEARCH_INTERVAL)
                {
                    _lastSearchTime = Time.realtimeSinceStartup;
                    if (Camera.main != null) 
                    {
                        targetCamera = Camera.main.transform;
                        // If initializing late, capture offset now
                        if (!_initialized)
                        {
                            _offset = transform.position - targetCamera.position;
                            _initialY = transform.position.y;
                            _initialized = true;
                        }
                    }
                }
            }

            if (targetCamera != null)
            {
                Vector3 targetPosition = targetCamera.position + _offset;
                if (_fixY)
                {
                    targetPosition.y = _initialY;
                }
                transform.position = targetPosition;
                transform.rotation = Quaternion.identity;
            }
        }
    }
}
