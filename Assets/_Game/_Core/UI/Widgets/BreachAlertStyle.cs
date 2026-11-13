using MB13.Aipocalypse.Core.LocalizationSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.UI
{
    // What the breach alert says and how it moves. Font and placement stay in the HUD prefab.
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Breach Alert Style", fileName = "BreachAlertStyle")]
    public sealed class BreachAlertStyle : ScriptableObject
    {
        [BoxGroup("Text"), Tooltip("HUD table")] public LocalizedString FenceText = Loc.Hud("breach.fence");
        [BoxGroup("Text")] public LocalizedString GateText = Loc.Hud("breach.gate");
        [BoxGroup("Text"), Tooltip("Added after the text, from where the break is seen by the player")]
        public LocalizedString AheadSuffix = Loc.Hud("breach.ahead");
        [BoxGroup("Text")] public LocalizedString BehindSuffix = Loc.Hud("breach.behind");
        [BoxGroup("Text")] public LocalizedString LeftSuffix = Loc.Hud("breach.left");
        [BoxGroup("Text")] public LocalizedString RightSuffix = Loc.Hud("breach.right");
        [BoxGroup("Text")] public Color Color = new(1f, 0.45f, 0.3f, 1f);

        [BoxGroup("Timing"), Min(0f)] public float FadeIn = 0.15f;
        [BoxGroup("Timing"), Min(0f), Tooltip("Seconds it stays")] public float Hold = 2.2f;
        [BoxGroup("Timing"), Min(0f)] public float FadeOut = 0.6f;
        [BoxGroup("Timing"), Min(1f), Tooltip("Scale the text starts at, settling to 1")] public float PunchScale = 1.15f;
    }
}
