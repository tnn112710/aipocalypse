using Ami.BroAudio;
using MB13.Aipocalypse.Core.LocalizationSystem;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.UI
{
    // Look, words, timing and sounds of the raid banner (RaidBannerHud). {0} in a subtitle is the night's day number.
    [CreateAssetMenu(fileName = "RaidBannerStyle", menuName = "Aipocalypse/UI/Raid Banner Style")]
    public sealed class RaidBannerStyle : ScriptableObject
    {
        [Tooltip("The words below may only use characters already in its atlas: a missing one is added at runtime (a hitch " +
            "when the banner shows, and a write to the font asset)")]
        public TMP_FontAsset Font;

        [Header("Raid starts")]
        [Tooltip("HUD table; subtitles: {0} = night number")]
        public LocalizedString StartTitle = Loc.Hud("raid.start_title");
        public LocalizedString StartSubtitle = Loc.Hud("raid.start_subtitle");
        public Color StartColor = new(1f, 0.38f, 0.22f, 1f);
        public SoundID StartSound;

        [Header("Every wave killed")]
        public LocalizedString VictoryTitle = Loc.Hud("raid.cleared_title");
        public LocalizedString VictorySubtitle = Loc.Hud("raid.cleared_subtitle");
        public Color VictoryColor = new(1f, 0.82f, 0.35f, 1f);
        public SoundID VictorySound;

        [Header("Dawn came first")]
        public LocalizedString DawnTitle = Loc.Hud("raid.dawn_title");
        public LocalizedString DawnSubtitle = Loc.Hud("raid.dawn_subtitle");
        public Color DawnColor = new(0.8f, 0.88f, 1f, 1f);
        public SoundID DawnSound;

        [Header("Timing (seconds)")]
        [Min(0f)] public float FadeIn = 0.3f;
        [Min(0f)] public float Hold = 2.6f;
        [Min(0f)] public float FadeOut = 0.9f;
        [Min(1f), Tooltip("The title starts this much bigger and settles")] public float PunchScale = 1.35f;
    }
}
