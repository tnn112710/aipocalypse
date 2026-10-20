// SC Screen Effects © Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//
// ⚠️ WARNING: UNAUTHORIZED USE OR DISTRIBUTION IS STRICTLY PROHIBITED
// • Copying, referencing, or reverse-engineering this source code for the creation of new Asset Store or derivative products,
//   or any other publicly distributed content is strictly forbidden and will result in legal action.
// • Studying this file for the purpose of reproducing its functionality in your own assets or tools is not permitted.
// • If you are viewing this file as a reference, please close it immediately to avoid unintentional design influence or potential EULA violations.
// • Uploading this file or any derivative of it to a public GitHub or similar repository will trigger an automated DMCA takedown request.
// • Studying to understand for personal, educational or integration purposes is allowed, studying to reproduce is not.

using System;
using UnityEngine;
#if USE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace sc.screeneffects.demo
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Movement Settings")] public float moveSpeed = 5f;
        public float gravity = -9.81f;
        public float jumpHeight = 0.5f;
        
        [Header("Look Settings")] public float lookSensitivity = 2f;
        public float bobbing;
        
        [Header("Camera Reference")] public Camera baseCamera;

        private CharacterController controller;

        private Vector2 moveInput;
        private Vector2 lookInput;
        private bool jumpInput;
        private float verticalVelocity;
        private float cameraPitch;

        #if USE_INPUT_SYSTEM
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        #endif

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            //Create input actions
            CreateInputActions();

            //Hide cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            baseCamera = Camera.main;
            
            #if !USE_INPUT_SYSTEM
            Debug.LogError("Input System is not enabled, this demo will not work without it!");
            #endif
        }

        private void CreateInputActions()
        {
#if USE_INPUT_SYSTEM
            var map = new InputActionMap("Player");

            lookAction = map.AddAction("look", binding: "<Mouse>/delta");
            moveAction = map.AddAction("move", binding: "<Gamepad>/leftStick");
            
            lookAction.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=15, y=15)");
            moveAction.AddCompositeBinding("Dpad")
                .With("Up", "<Keyboard>/w")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/s")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/a")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/d")
                .With("Right", "<Keyboard>/rightArrow");
            
            jumpAction = map.AddAction("jump", binding: "<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");
            
            moveAction.Enable();
            lookAction.Enable();
            jumpAction.Enable();

            map.Enable();
#endif
        }

        private void Update()
        {
#if USE_INPUT_SYSTEM
            moveInput = moveAction.ReadValue<Vector2>();
            lookInput = lookAction.ReadValue<Vector2>();
            jumpInput = jumpAction.WasPressedThisFrame();
            
            bool leftShift = Keyboard.current?.leftShiftKey?.isPressed ?? false;

            if (leftShift)
            {
                moveInput *= 3f;
            }
#endif
            
            HandleMovement();
            HandleLook();
        }

        private void HandleMovement()
        {
            Vector3 move = baseCamera.transform.right * moveInput.x + baseCamera.transform.forward * moveInput.y;

            if (controller.isGrounded && verticalVelocity < 0)
                verticalVelocity = -2f;
            
            if (controller.isGrounded && jumpInput)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            verticalVelocity += gravity * Time.deltaTime;
            move.y = verticalVelocity;

            controller.Move(move * (moveSpeed * Time.deltaTime));
            
            baseCamera.transform.position = transform.position + Vector3.up * (controller.height * 0.5f);

            if (bobbing > 0)
            {
                float bobY = Mathf.Sin(Time.time * 12f) * bobbing * 0.01f * controller.velocity.magnitude;
                baseCamera.transform.localPosition += new Vector3(0, bobY, 0);
            }
        }

        float inputRotateAxisX, inputRotateAxisY;
        
        private void HandleLook()
        {
            float mouseSensitivity = lookSensitivity * 0.1f;
            
            inputRotateAxisX = lookInput.x * mouseSensitivity;
            inputRotateAxisY = lookInput.y * mouseSensitivity;
            
            float rotationX = baseCamera.transform.localEulerAngles.x;
            float newRotationY = baseCamera.transform.localEulerAngles.y + inputRotateAxisX;
            
            // Weird clamping code due to weird Euler angle mapping...
            float newRotationX = (rotationX - inputRotateAxisY);
            if (rotationX <= 90.0f && newRotationX >= 0.0f)
                newRotationX = Mathf.Clamp(newRotationX, 0.0f, 90.0f);
            if (rotationX >= 270.0f)
                newRotationX = Mathf.Clamp(newRotationX, 270.0f, 360.0f);
            
            // For mouse input, normalize sensitivity since delta is already in pixels
            baseCamera.transform.localRotation = Quaternion.Euler(newRotationX, newRotationY, baseCamera.transform.localEulerAngles.z);
        }

        private void OnDestroy()
        {
            #if USE_INPUT_SYSTEM
            moveAction?.Dispose();
            lookAction?.Dispose();
            jumpAction?.Dispose();
            #endif
        }
    }
}