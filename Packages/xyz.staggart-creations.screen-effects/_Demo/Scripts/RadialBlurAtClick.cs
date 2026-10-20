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
using sc.screeneffects.runtime;
using UnityEngine;
#if USE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace sc.screeneffects.examples.runtime
{
    public class RadialBlurAtClick : MonoBehaviour
    {
        public float maxIntensity = 0.2f;

        private RadialBlur effect;

        private float currentWeight = 0f;

        #if USE_INPUT_SYSTEM
        private InputAction pointerPositionAction;
        private InputAction pointerPressAction;
        #endif

        private void Awake()
        {
#if USE_INPUT_SYSTEM
            pointerPositionAction = new InputAction(
                name: "Pointer Position",
                type: InputActionType.Value,
                binding: "<Pointer>/position"
            );

            pointerPressAction = new InputAction(
                name: "Pointer Press",
                type: InputActionType.Button,
                binding: "<Pointer>/press"
            );

            pointerPressAction.performed += OnPointerPressed;
#else
            Debug.LogError("[RadialBlurAtClick] Input System is not enabled, this script will not be functional");
            #endif
        }

#if USE_INPUT_SYSTEM
        private void Start()
        {
            effect = PostProcessingComponent.Create<RadialBlur>(true, true);
        }

        private void OnEnable()
        {
            pointerPositionAction.Enable();
            pointerPressAction.Enable();
        }

        private void Update()
        {
            //Decreased weight
            currentWeight = Mathf.Clamp01(currentWeight - Time.deltaTime * 4f);

            effect.blurLength.Override(maxIntensity);
            effect.intensity.value = currentWeight;
        }

        private void OnPointerPressed(InputAction.CallbackContext context)
        {
            Vector2 pointerPosition = pointerPositionAction.ReadValue<Vector2>();

            currentWeight = 1f;
            effect.CenterOnScreenPosition(pointerPosition);
        }

        private void OnDestroy()
        {
            pointerPressAction.performed -= OnPointerPressed;

            pointerPositionAction.Dispose();
            pointerPressAction.Dispose();

            effect.Dispose();
        }

#endif
    }
}