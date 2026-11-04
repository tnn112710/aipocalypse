using UnityEngine;

namespace Aipocalypse.Core.DefenseSystem
{
    // How damaged barriers show while the player holds the hammer (RepairHighlight): a see-through tint over each, through
    // walls, coloured by how much health it has left.
    [CreateAssetMenu(fileName = "RepairHighlightStyle", menuName = "Aipocalypse/Defense/Repair Highlight Style")]
    public sealed class RepairHighlightStyle : ScriptableObject
    {
        [Tooltip("Drawn over each damaged barrier: the x-ray ghost shader (Aipocalypse/Ghosts/GhostXray)")]
        public Material Look;
        [Min(0f), Tooltip("Metres from the player within which damaged barriers show")]
        public float Range = 45f;
        [Tooltip("Fill colour by health share: left = no health (broken), right = almost whole. Alpha = how see-through")]
        public Gradient Fill = new()
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(1f, 0.12f, 0.08f), 0f),
                new GradientColorKey(new Color(1f, 0.5f, 0.05f), 0.5f),
                new GradientColorKey(new Color(1f, 0.9f, 0.15f), 1f),
            },
            alphaKeys = new[] { new GradientAlphaKey(0.35f, 0f), new GradientAlphaKey(0.35f, 1f) },
        };
        [Min(0f), Tooltip("Edge glow: the fill colour times this (HDR)")]
        public float RimIntensity = 2.5f;
        [Range(0f, 1f), Tooltip("Edge glow opacity")]
        public float RimAlpha = 0.7f;
        [Header("Up close"), Min(0f), Tooltip("Metres from the player where the tint starts to fade (so a segment being mended isn't covered)")]
        public float FadeFrom = 8f;
        [Min(0f), Tooltip("Metres from the player where it's faded to Near Opacity")]
        public float FadeTo = 2.5f;
        [Range(0f, 1f), Tooltip("Share of its opacity left at Fade To and closer")]
        public float NearOpacity = 0.25f;

        // 1 far off, down to NearOpacity up close.
        public float OpacityAt(float distance) =>
            Mathf.Lerp(NearOpacity, 1f, Mathf.InverseLerp(FadeTo, Mathf.Max(FadeTo + 0.01f, FadeFrom), distance));
    }
}
