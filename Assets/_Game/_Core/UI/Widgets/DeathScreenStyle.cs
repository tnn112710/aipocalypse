using System;
using DG.Tweening;
using MB13.Aipocalypse.Core.LocalizationSystem;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.UI
{
    // Look and timing of the death screen: the eyes closing over the last view, then the full-screen "Walk it off!" menu
    // (a sunset sky drawn from a gradient, layered art tinted from here, text, a progress bar).
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

        [Header("Scene colours")]
        [Tooltip("Sky, top (time 0) to the horizon (time 1): drawn as a texture by code, so retint it here")]
        public Gradient Sky = DefaultSky();
        [Tooltip("The art layers are white-tinted by default (their own colours); change a tint to recolour that layer")]
        public Color SunTint = Color.white;
        public Color CloudTint = Color.white;
        public Color MountainTint = Color.white;
        public Color TreeTint = Color.white;
        [Tooltip("The dark ground band under the dog and the bar")] public Color GroundColor = new(0.17f, 0.09f, 0.06f);
        public Color DogTint = Color.white;
        [Min(0f), Tooltip("How far the clouds drift sideways, in canvas units")] public float CloudDrift = 28f;
        [Min(1f)] public float CloudDriftPeriod = 11f;

        [Header("Text")]
        public TMP_FontAsset TitleFont;
        [Tooltip("Optional material preset for the title (soft shadow underlay)")] public Material TitleMaterial;
        public float TitleSize = 170f;
        public Color TitleColor = new(1f, 0.945f, 0.82f);
        public float SubtitleSize = 52f;
        public Color SubtitleColor = new(1f, 0.945f, 0.82f);
        public TMP_FontAsset StatusFont;
        public float StatusSize = 40f;
        public Color StatusColor = new(1f, 0.945f, 0.82f);
        [Tooltip("HUD table")] public LocalizedString Title = Loc.Hud("death.title");
        public LocalizedString Subtitle = Loc.Hud("death.subtitle");
        public LocalizedString LoadingText = Loc.Hud("death.loading");
        public LocalizedString ContinueText = Loc.Hud("death.continue");

        [Header("Progress bar")]
        public Color BarFrameTint = Color.white;
        [Tooltip("The empty part of the bar")] public Color BarTrackColor = new(1f, 0.945f, 0.82f);
        public Color BarFillTint = Color.white;
        [Range(0f, 1f), Tooltip("The bar fills this far over MinLoadingTime, then waits for the respawn")] public float BarBeforeReady = 0.9f;
        [Min(0f), Tooltip("Time to fill the rest once the respawn is ready")] public float BarFinishTime = 0.3f;

        [Header("Timing")]
        [Min(0f)] public float MenuFadeIn = 0.6f;
        [Min(0f)] public float MenuFadeOut = 0.5f;
        [Min(0f), Tooltip("The loading line stays at least this long, so it can be read")] public float MinLoadingTime = 1.2f;
        [Range(0f, 1f), Tooltip("\"Click to continue\" pulses down to this alpha")] public float ContinuePulseAlpha = 0.45f;
        [Min(0.1f)] public float ContinuePulsePeriod = 1.6f;

        static Gradient DefaultSky()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.937f, 0.357f, 0.239f), 0f),
                    new GradientColorKey(new Color(0.996f, 0.545f, 0.208f), 0.45f),
                    new GradientColorKey(new Color(1f, 0.761f, 0.29f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }
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
