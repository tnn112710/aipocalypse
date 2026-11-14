using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    // How the crosshair's dot and cross behave. Sprites, sizes and colors stay in the HUD prefab.
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Crosshair Style", fileName = "CrosshairStyle")]
    public sealed class CrosshairStyle : ScriptableObject
    {
        [Min(0f), Tooltip("Pixels: the cross never closes tighter than this, so it stays clear of the dot and the hit marker")]
        public float MinGap = 10f;
        [Min(0.1f), Tooltip("How fast the cross opens as the wobble grows (higher = snappier)")] public float OpenSpeed = 40f;
        [Min(0.1f), Tooltip("How fast it closes as the wobble settles")] public float CloseSpeed = 8f;
        [Min(0.1f), Tooltip("Alpha per second when sprinting starts or stops")] public float FadeSpeed = 8f;
    }
}
