using UnityEngine;

namespace MB13.Aipocalypse.Core.GraphicsSystem.ScreenLooks
{
    // Which tool draws the screen looks: switch Backend to swap SC Screen Effects, URP-only or another pack's backend asset.
    // Asset: Data/Graphics/ScreenLooks/ScreenLookSettings.
    [CreateAssetMenu(fileName = "ScreenLookSettings", menuName = "Aipocalypse/Graphics/Screen Look Settings")]
    public sealed class ScreenLookSettings : ScriptableObject
    {
        [Tooltip("Off: every look is 0")] public bool Enabled = true;
        [Tooltip("The factory asset that builds the backend (VolumeLookBackendAsset for volume-based tools)")] public ScreenLookBackendAsset Backend;
    }
}
