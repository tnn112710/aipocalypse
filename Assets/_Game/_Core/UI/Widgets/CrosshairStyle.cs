using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    // How the crosshair's dot and spread ticks behave. Sprites, sizes and colors stay in the HUD prefab.
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Crosshair Style", fileName = "CrosshairStyle")]
    public sealed class CrosshairStyle : ScriptableObject
    {
        [Min(0f), Tooltip("Pixels: ticks hide when the cone is smaller than this")] public float MinSpread = 8f;
        [Min(0.1f), Tooltip("Pixels over which the ticks fade in past the minimum")] public float SpreadFade = 6f;
        [Min(0.1f), Tooltip("How fast the ticks follow the spread")] public float SpreadSpeed = 20f;
        [Min(0.1f), Tooltip("Alpha per second when sprinting starts or stops")] public float FadeSpeed = 8f;
    }
}
