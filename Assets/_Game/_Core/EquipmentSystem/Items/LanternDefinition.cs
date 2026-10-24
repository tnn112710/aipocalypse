using UnityEngine;

namespace Aipocalypse.Core.EquipmentSystem
{
    // A light worn on the belt: works without being held, widening the player's light in the dark (PlayerVision).
    [CreateAssetMenu(fileName = "Lantern", menuName = "Aipocalypse/Items/Lantern")]
    public sealed class LanternDefinition : ItemDefinition
    {
        [SerializeField, Min(0f), Tooltip("Metres added to the player's vision radius")]
        float _radius = 6f;

        [SerializeField, ColorUsage(false)] Color _color = new(1f, 0.72f, 0.42f);

        [SerializeField, Min(0f), Tooltip("How bright the light still is at the radius' edge, in place of the vision light's (PlayerVisionTuning.Brightness)")]
        float _brightness = 0.15f;

        [SerializeField, Range(0f, 0.5f), Tooltip("Share of the intensity the flame wavers by")]
        float _flicker = 0.08f;

        [SerializeField, Min(0f), Tooltip("How fast the flame wavers")]
        float _flickerSpeed = 6f;

        [SerializeField, Tooltip("Point-light shadows render six shadow maps; measure before turning them on")]
        LightShadows _shadows = LightShadows.None;

        public float Radius => _radius;
        public Color Color => _color;
        public float Brightness => _brightness;
        public float Flicker => _flicker;
        public float FlickerSpeed => _flickerSpeed;
        public LightShadows Shadows => _shadows;

        public override ItemCategory Category => ItemCategory.Lantern;
    }
}
