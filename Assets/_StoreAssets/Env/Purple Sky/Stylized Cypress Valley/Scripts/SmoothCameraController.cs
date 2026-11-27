namespace Sergio.Rendering
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    public class SmoothCameraController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 5f;
        public float sprintMultiplier = 2f;
        public float acceleration = 10f;

        [Header("Speed Control")]
        public float minSpeed = 1f;
        public float maxSpeed = 20f;
        public float scrollSensitivity = 10f;

        [Header("Rotation Settings")]
        public float mouseSensitivity = 0.1f;
        public float rotationSmoothTime = 0.1f;

        private Vector3 currentVelocity;
        private Vector3 targetVelocity;

        private Vector2 currentRotation;
        private Vector2 targetRotation;
        private Vector2 rotationVelocity;

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            HandleSpeedControl();
            HandleMovement();
            HandleRotation();
        }

        void HandleSpeedControl()
        {
            float scroll = Mouse.current.scroll.ReadValue().y;

            if (Mathf.Abs(scroll) > 0.01f)
            {
                moveSpeed += scroll * scrollSensitivity * Time.deltaTime;
                moveSpeed = Mathf.Clamp(moveSpeed, minSpeed, maxSpeed);
            }
        }

        void HandleMovement()
        {
            Vector2 moveInput = Vector2.zero;

            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1;

            moveInput = moveInput.normalized;

            Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);

            if (Keyboard.current.eKey.isPressed) input.y += 1f;
            if (Keyboard.current.qKey.isPressed) input.y -= 1f;

            float sprint = Keyboard.current.leftShiftKey.isPressed ? sprintMultiplier : 1f;
            float speed = moveSpeed * sprint;

            targetVelocity = transform.TransformDirection(input) * speed;
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, acceleration * Time.deltaTime);

            transform.position += currentVelocity * Time.deltaTime;
        }

        void HandleRotation()
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            targetRotation.x += mouseDelta.x * mouseSensitivity;
            targetRotation.y -= mouseDelta.y * mouseSensitivity;
            targetRotation.y = Mathf.Clamp(targetRotation.y, -89f, 89f);

            currentRotation = Vector2.SmoothDamp(
                currentRotation,
                targetRotation,
                ref rotationVelocity,
                rotationSmoothTime
            );

            transform.rotation = Quaternion.Euler(currentRotation.y, currentRotation.x, 0f);
        }
    }
}
