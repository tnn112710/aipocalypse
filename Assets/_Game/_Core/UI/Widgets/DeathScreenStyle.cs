using System;
using DG.Tweening;
using MB13.Aipocalypse.Core.LocalizationSystem;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.UI
{
    // Look and timing of the death screen: the eyes closing over the last view, then the full-screen "You're dead" menu.
    [CreateAssetMenu(fileName = "DeathScreenStyle", menuName = "Aipocalypse/UI/Death Screen Style")]
    public sealed class DeathScreenStyle : ScriptableObject
    {
        [Header("Eyes closing")]
        [Tooltip("Upper eyelid: black above an arched, soft edge (the lower lid is the same texture flipped)")]
        public Texture2D EyelidTexture;
        [Tooltip("Where the lid turns fully black in the middle, as a share of its height from its bottom (the texture's arch + " +
            "soft edge). A shut lid goes this far past the screen's middle, so a shut pair leaves no seam")]
        [Range(0f, 0.6f)] public float EyelidSolidAt = 0.3f;
        [Tooltip("Played in order: each step moves the lids (how much of the view they cover in the middle: 0 open, 0.4 = 40%, " +
            "1 shut) and the darkness over the view (0 none, 1 black)")]
        public EyeStep[] EyeSteps =
        {
            new(1.3f, 0.4f, 0.25f, Ease.InOutSine),
            new(0.7f, 0.3f, 0.2f, Ease.InOutSine),
            new(1f, 0.8f, 0.5f, Ease.InOutSine),
            new(0.7f, 0.7f, 0.45f, Ease.InOutSine),
            new(1.1f, 1f, 1f, Ease.InOutQuad),
            new(0.6f, 1f, 1f, Ease.Linear),
        };

        [Header("Menu")]
        public Color BackgroundColor = new(0.17f, 0.61f, 0.71f);
        [Tooltip("Soft light in the middle of the background")] public Texture2D GlowTexture;
        public Color GlowColor = new(0.55f, 0.85f, 0.89f, 0.9f);
        public Color FooterColor = new(0.04f, 0.33f, 0.45f);
        public Color FooterLineColor = new(0.6f, 0.85f, 0.9f, 0.85f);
        public TMP_FontAsset TitleFont;
        [Tooltip("Optional material preset for the title (soft shadow underlay)")] public Material TitleMaterial;
        public float TitleSize = 230f;
        public Color TitleColor = new(0.93f, 0.9f, 0.88f);
        public TMP_FontAsset StatusFont;
        public float StatusSize = 44f;
        public Color StatusColor = Color.white;
        [Tooltip("HUD table")] public LocalizedString Title = Loc.Hud("death.title");
        public LocalizedString LoadingText = Loc.Hud("death.loading");
        public LocalizedString ContinueText = Loc.Hud("death.continue");

        [Header("Timing")]
        [Min(0f)] public float MenuFadeIn = 0.6f;
        [Min(0f)] public float MenuFadeOut = 0.5f;
        [Min(0f), Tooltip("\"Loading...\" stays at least this long, so it can be read")] public float MinLoadingTime = 1.2f;
        [Range(0f, 1f), Tooltip("\"Click to continue\" pulses down to this alpha")] public float ContinuePulseAlpha = 0.45f;
        [Min(0.1f)] public float ContinuePulsePeriod = 1.6f;
    }

    [Serializable]
    public struct EyeStep
    {
        [Min(0f)] public float Duration;
        [Range(0f, 1f), Tooltip("Share of the view the lids cover in the middle: 0 open, 1 shut")] public float Closed;
        [Range(0f, 1f), Tooltip("Black over the view: 0 none, 1 black")] public float Darkness;
        public Ease Ease;

        public EyeStep(float duration, float closed, float darkness, Ease ease)
        {
            Duration = duration;
            Closed = closed;
            Darkness = darkness;
            Ease = ease;
        }
    }
}
