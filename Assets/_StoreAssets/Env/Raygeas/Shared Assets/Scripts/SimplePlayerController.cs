using UnityEngine;
using UnityEngine.InputSystem;

namespace Raygeas
{
    [RequireComponent(typeof(CharacterController))]
    public class SimplePlayerController : MonoBehaviour
    {
        [SerializeField] private Transform playerCamera;
        [SerializeField, Min(0f)] private float walkSpeed = 4f;
        [SerializeField, Min(0f)] private float sprintSpeed = 7f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
        [SerializeField] private float gravity = -20f;

        private CharacterController controller;
        private float pitch;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            pitch = Mathf.DeltaAngle(0f, playerCamera.localEulerAngles.x);
        }

        private void OnEnable() => SetCursorLocked(true);
        private void OnDisable() => SetCursorLocked(false);

        private void Update()
        {
            if (!Application.isFocused || !controller.enabled)
                return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard?.escapeKey.wasPressedThisFrame == true)
                SetCursorLocked(false);
            else if (mouse?.leftButton.wasPressedThisFrame == true)
                SetCursorLocked(true);

            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;

            if (cursorLocked && mouse != null)
            {
                Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(Vector3.up, look.x);
                pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
                playerCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            Vector3 velocity = Vector3.zero;

            if (cursorLocked && keyboard != null)
            {
                Vector2 input = new Vector2(
                    keyboard.dKey.ReadValue() - keyboard.aKey.ReadValue(),
                    keyboard.wKey.ReadValue() - keyboard.sKey.ReadValue());

                input = Vector2.ClampMagnitude(input, 1f);
                float speed = keyboard.shiftKey.isPressed ? sprintSpeed : walkSpeed;
                velocity = (transform.right * input.x + transform.forward * input.y) * speed;

                if (controller.isGrounded && keyboard.spaceKey.wasPressedThisFrame)
                    verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
            }

            verticalVelocity += gravity * Time.deltaTime;
            velocity.y = verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}