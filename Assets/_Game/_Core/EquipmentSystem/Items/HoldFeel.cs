using System;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // How heavy an item feels in hand, on top of the player's held-item tuning (1 = as tuned).
    [Serializable]
    public sealed class HoldFeel
    {
        public const float MinMobility = 0.5f;
        public const float MaxMobility = 1f;

        [Range(MinMobility, MaxMobility), Tooltip("Multiplies walk and sprint speed while it's held (empty hands = 1; lower = heavier)")]
        public float Mobility = 0.8f;
        [Range(0f, 3f), Tooltip("Multiplies the sway and bob while it's held (higher = heavier, less steady)")]
        public float SwayScale = 1f;

        public static float ClampMobility(float mobility) => Mathf.Clamp(mobility, MinMobility, MaxMobility);

        // Sprinting with it costs stamina ÷ mobility: empty hands ×1, the heaviest ×2.
        public static float SprintCostScale(float mobility) => 1f / ClampMobility(mobility);
    }
}
