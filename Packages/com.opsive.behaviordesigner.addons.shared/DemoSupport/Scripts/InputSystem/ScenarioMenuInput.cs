/// ---------------------------------------------
/// Shared Add-On for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.Shared.Demo
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    /// Supplies Input System keyboard navigation to the add-on demos.
    /// </summary>
    internal static class ScenarioMenuInput
    {
        /// <summary>
        /// Registers the reader before scenes initialize.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() { ScenarioMenu.RegisterNavigationInput(Read); }

        /// <summary>
        /// Reads a single navigation step while preserving player movement keys.
        /// </summary>
        /// <param name="allowWASD">Whether WASD is available for menu navigation.</param><returns>The signed scenario step.</returns>
        private static int Read(bool allowWASD)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return 0;
            var previous = keyboard.upArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame;
            var next = keyboard.downArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame;
            if (allowWASD) { previous |= keyboard.wKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame; next |= keyboard.sKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame; }
            return (next ? 1 : 0) - (previous ? 1 : 0);
        }
    }
}