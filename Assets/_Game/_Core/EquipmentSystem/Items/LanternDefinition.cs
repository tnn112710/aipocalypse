using UnityEngine;

namespace Aipocalypse.Core.EquipmentSystem
{
    // A light worn on the belt: works without being held, widening the player's light in the dark (PlayerVision).
    [CreateAssetMenu(fileName = "Lantern", menuName = "Aipocalypse/Items/Lantern")]
    public sealed class LanternDefinition : ItemDefinition
    {
        [field: SerializeField, Min(0f), Tooltip("Metres added to the player's vision radius")]
        public float Radius { get; private set; } = 6f;

        [field: SerializeField, ColorUsage(false)] public Color Color { get; private set; } = new(1f, 0.72f, 0.42f);

        [field: SerializeField, Min(0f), Tooltip("How bright the light still is at the radius' edge, in place of the vision light's (PlayerVisionTuning.Brightness)")]
        public float Brightness { get; private set; } = 0.15f;

        [field: SerializeField, Range(0f, 0.5f), Tooltip("Share of the intensity the flame wavers by")]
        public float Flicker { get; private set; } = 0.08f;

        [field: SerializeField, Min(0f), Tooltip("How fast the flame wavers")]
        public float FlickerSpeed { get; private set; } = 6f;

        [field: SerializeField, Tooltip("Point-light shadows render six shadow maps; measure before turning them on")]
        public LightShadows Shadows { get; private set; } = LightShadows.None;

        public override ItemCategory Category => ItemCategory.Lantern;
    }
}
