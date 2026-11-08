using System;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // How heavy an item feels in hand, on top of the player's held-item tuning (1 = as tuned).
    [Serializable]
    public sealed class HoldFeel
    {
        [Range(0.1f, 1.5f), Tooltip("Multiplies the move speed while aiming down sights with it (lower = heavier)")]
        public float AimMoveSpeedScale = 1f;
        [Range(0f, 3f), Tooltip("Multiplies the sway and bob while it's held (higher = heavier, less steady)")]
        public float SwayScale = 1f;
    }
}
