using System;
using UnityEngine;

namespace Aipocalypse.Input
{
    // Player-facing control and view options. The future SettingsService will load and save these; changes apply live.
    [Serializable]
    public sealed class ControlSettings
    {
        [Min(0f), Tooltip("Degrees per mouse-delta unit")]
        public float MouseSensitivity = 0.1f;

        [Min(0f), Tooltip("Degrees per second at full stick")]
        public float GamepadLookSpeed = 180f;

        public bool InvertY;

        [Range(50f, 110f), Tooltip("Vertical field of view of the world camera (degrees), before aiming zoom")]
        public float FieldOfView = 70f;
    }
}
