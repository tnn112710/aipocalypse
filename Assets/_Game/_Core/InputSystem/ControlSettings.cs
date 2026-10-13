using System;
using UnityEngine;

namespace Aipocalypse.Input
{
    // Player-facing control options. The future SettingsService will load and save these.
    [Serializable]
    public sealed class ControlSettings
    {
        [Min(0f), Tooltip("Degrees per mouse-delta unit")]
        public float MouseSensitivity = 0.1f;

        [Min(0f), Tooltip("Degrees per second at full stick")]
        public float GamepadLookSpeed = 180f;

        public bool InvertY;
    }
}
