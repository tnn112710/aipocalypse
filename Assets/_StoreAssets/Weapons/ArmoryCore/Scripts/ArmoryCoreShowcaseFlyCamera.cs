using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ArmoryCore
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ArmoryCoreShowcaseFlyCamera : MonoBehaviour
    {
        [Min(0.01f)] public float moveSpeed = 12f;
        [Min(1f)] public float fastMultiplier = 4f;
        [Min(0.01f)] public float lookSensitivity = 2.5f;
        [Min(0.01f)] public float firstPersonSpeed = 4.6f;
        [Min(1f)] public float sprintMultiplier = 1.65f;
        [Min(0.1f)] public float jumpHeight = 1.1f;
        [Min(1f)] public float gravity = 24f;

        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private float yaw;
        private float pitch;
        private bool weaponTestMode;
        private Camera attachedCamera;
        private float initialFieldOfView;
        private CharacterController characterController;
        private float verticalVelocity;
        private Vector2 cameraRecoil;
        private Vector2 cameraRecoilVelocity;
        private float cameraRoll;
        private float cameraRollVelocity;

        public bool WeaponTestMode => weaponTestMode;
        public float MovementAmount { get; private set; }
        public bool IsSprinting { get; private set; }
        public Vector2 LookDelta { get; private set; }

        private void Awake()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            attachedCamera = GetComponent<Camera>();
            characterController = GetComponent<CharacterController>();
            if (characterController != null)
                characterController.enabled = false;
            initialFieldOfView = attachedCamera == null ? 48f : attachedCamera.fieldOfView;
            ReadAngles();
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var resetPressed = !weaponTestMode && keyboard != null && keyboard.rKey.wasPressedThisFrame;
            var escapePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            var rightPressed = !weaponTestMode && mouse != null && mouse.rightButton.wasPressedThisFrame;
            var rightReleased = !weaponTestMode && mouse != null && mouse.rightButton.wasReleasedThisFrame;
            var rightHeld = mouse != null && mouse.rightButton.isPressed;
            var lookDelta = mouse == null ? Vector2.zero : mouse.delta.ReadValue() * 0.05f;
            var zoomDelta = mouse == null ? 0f : mouse.scroll.ReadValue().y * 0.02f;
            var horizontal = keyboard == null ? 0f
                : (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            var forward = keyboard == null ? 0f
                : (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            var vertical = keyboard == null ? 0f
                : (keyboard.spaceKey.isPressed ? 1f : 0f)
                    - (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed ? 1f : 0f);
            var fast = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            var jumpPressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
#else
            var resetPressed = !weaponTestMode && Input.GetKeyDown(KeyCode.R);
            var escapePressed = Input.GetKeyDown(KeyCode.Escape);
            var rightPressed = !weaponTestMode && Input.GetMouseButtonDown(1);
            var rightReleased = !weaponTestMode && Input.GetMouseButtonUp(1);
            var rightHeld = Input.GetMouseButton(1);
            var lookDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            var zoomDelta = Input.mouseScrollDelta.y * 2.4f;
            var horizontal = Input.GetAxisRaw("Horizontal");
            var forward = Input.GetAxisRaw("Vertical");
            var vertical = (Input.GetKey(KeyCode.Space) ? 1f : 0f)
                - (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ? 1f : 0f);
            var fast = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var jumpPressed = Input.GetKeyDown(KeyCode.Space);
#endif

            LookDelta = weaponTestMode ? lookDelta : Vector2.zero;
            var planarInput = new Vector2(horizontal, forward);
            MovementAmount = weaponTestMode ? Mathf.Clamp01(planarInput.magnitude) : 0f;
            IsSprinting = weaponTestMode && fast && MovementAmount > 0.15f;

            if (resetPressed)
            {
                transform.SetPositionAndRotation(initialPosition, initialRotation);
                if (attachedCamera != null)
                    attachedCamera.fieldOfView = initialFieldOfView;
                ReadAngles();
            }

            if (!weaponTestMode && attachedCamera != null && Mathf.Abs(zoomDelta) > 0.001f)
                attachedCamera.fieldOfView = Mathf.Clamp(attachedCamera.fieldOfView - zoomDelta, 28f, 65f);

            if (weaponTestMode)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (rightPressed)
                Cursor.lockState = CursorLockMode.Locked;
            if (!weaponTestMode && (rightReleased || escapePressed))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (weaponTestMode || rightHeld)
            {
                yaw += lookDelta.x * lookSensitivity;
                pitch = Mathf.Clamp(pitch - lookDelta.y * lookSensitivity, -89f, 89f);
            }

            UpdateCameraRecoil(Time.unscaledDeltaTime);
            if (weaponTestMode || rightHeld)
                transform.rotation = Quaternion.Euler(
                    pitch - cameraRecoil.y,
                    yaw + cameraRecoil.x,
                    cameraRoll);

            if (weaponTestMode && characterController != null && characterController.enabled)
            {
                var flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                var flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                var movement = flatRight * horizontal + flatForward * forward;
                if (movement.sqrMagnitude > 1f)
                    movement.Normalize();

                if (characterController.isGrounded && verticalVelocity < 0f)
                    verticalVelocity = -2f;
                if (jumpPressed && characterController.isGrounded)
                    verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                verticalVelocity -= gravity * Time.deltaTime;

                var speed = firstPersonSpeed * (fast ? sprintMultiplier : 1f);
                characterController.Move(
                    (movement * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
            }
            else
            {
                var movement = transform.right * horizontal + Vector3.up * vertical + transform.forward * forward;
                if (movement.sqrMagnitude > 1f)
                    movement.Normalize();

                var speed = moveSpeed * (fast ? fastMultiplier : 1f);
                transform.position += movement * speed * Time.unscaledDeltaTime;
            }
        }

        public void SetWeaponTestMode(bool value)
        {
            weaponTestMode = value;
            verticalVelocity = 0f;
            MovementAmount = 0f;
            IsSprinting = false;
            LookDelta = Vector2.zero;
            cameraRecoil = Vector2.zero;
            cameraRecoilVelocity = Vector2.zero;
            cameraRoll = 0f;
            cameraRollVelocity = 0f;
            if (characterController != null)
                characterController.enabled = value;
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !value;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            var controllerWasEnabled = characterController != null && characterController.enabled;
            if (controllerWasEnabled)
                characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            if (controllerWasEnabled)
                characterController.enabled = true;
            verticalVelocity = 0f;
            cameraRecoil = Vector2.zero;
            cameraRecoilVelocity = Vector2.zero;
            cameraRoll = 0f;
            cameraRollVelocity = 0f;
            ReadAngles();
        }

        public void ApplyWeaponRecoil(Vector2 impulse)
        {
            if (!weaponTestMode)
                return;
            var vertical = Mathf.Clamp(impulse.y, 0f, 3.5f);
            var horizontal = Mathf.Clamp(impulse.x, -1.5f, 1.5f);

            pitch = Mathf.Clamp(pitch - vertical * 0.22f, -89f, 89f);
            yaw += horizontal * 0.12f;
            cameraRecoilVelocity += new Vector2(horizontal * 10f, vertical * 16f);
            cameraRecoilVelocity = Vector2.ClampMagnitude(cameraRecoilVelocity, 30f);
            cameraRollVelocity = Mathf.Clamp(
                cameraRollVelocity - horizontal * 6f,
                -10f,
                10f);
        }

        private void UpdateCameraRecoil(float deltaTime)
        {
            var step = Mathf.Min(Mathf.Max(0f, deltaTime), 0.05f);
            const float recoilStiffness = 78f;
            const float recoilDamping = 16.2f;
            cameraRecoilVelocity += (
                -cameraRecoil * recoilStiffness
                - cameraRecoilVelocity * recoilDamping) * step;
            cameraRecoil += cameraRecoilVelocity * step;
            cameraRecoil.x = Mathf.Clamp(cameraRecoil.x, -1.8f, 1.8f);
            if (cameraRecoil.y < 0f)
            {
                cameraRecoil.y = 0f;
                if (cameraRecoilVelocity.y < 0f)
                    cameraRecoilVelocity.y = 0f;
            }
            else
            {
                cameraRecoil.y = Mathf.Min(cameraRecoil.y, 3.2f);
            }

            const float rollStiffness = 95f;
            const float rollDamping = 19f;
            cameraRollVelocity += (-cameraRoll * rollStiffness - cameraRollVelocity * rollDamping) * step;
            cameraRoll += cameraRollVelocity * step;
            cameraRoll = Mathf.Clamp(cameraRoll, -0.8f, 0.8f);
        }

        private void ReadAngles()
        {
            var angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x > 180f ? angles.x - 360f : angles.x;
        }

        private void OnGUI()
        {
            if (GetComponent<ArmoryCoreShowcaseInteraction>() != null)
                return;
            const int width = 410;
            GUI.Box(new Rect(14, 14, width, 74), string.Empty);
            GUI.Label(new Rect(28, 23, width - 20, 22), "WEAPON SHOWCASE — 191 WEAPONS / 59 MODULES");
            GUI.Label(new Rect(28, 47, width - 20, 22),
                "WASD move | RMB look | Wheel zoom | Space/Ctrl height | R reset");
        }
    }
}
