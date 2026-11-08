using UnityEngine;

namespace MB13.Aipocalypse.Core.EnvironmentSystem
{
    // How renderers on the highlight layer (enemies) stay readable at night: a light only they receive, scaled with darkness,
    // so it is off by day.
    [CreateAssetMenu(menuName = "Aipocalypse/Environment/Night Highlight Settings", fileName = "NightHighlight")]
    public sealed class NightHighlightSettings : ScriptableObject
    {
        [Tooltip("Renderers on this rendering layer get the light. Enemies join it through Enemy System Settings")]
        public RenderingLayerMask Layer;

        [Tooltip("A directional light from behind the player that lights only the layer")]
        public bool LightEnabled = true;
        public Color LightColor = new(0.75f, 0.82f, 1f, 1f);
        [Min(0f), Tooltip("At full darkness")] public float LightIntensity = 0.8f;
        [Range(0f, 89f), Tooltip("Degrees the light points down from the view")] public float LightPitch = 35f;
        [Range(-90f, 90f), Tooltip("Degrees the light turns to the side of the view, so faces get some shape")]
        public float LightYaw = 35f;

        [Tooltip("Enemies' glowing eyes (EnemyEyes) at night")] public bool EyesEnabled = true;
        [Min(0f), Tooltip("Eye brightness at full darkness, times each eye material's colour")] public float EyeBrightness = 1f;
    }
}
