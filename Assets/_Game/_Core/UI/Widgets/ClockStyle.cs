using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.UI
{
    [Serializable]
    public struct PhaseLabel
    {
        [Tooltip("HUD table")] public LocalizedString Text;
        public Color Color;

        public PhaseLabel(LocalizedString text, Color color)
        {
            Text = text;
            Color = color;
        }
    }

    // Everything the HUD clock looks like. Sizes and placement stay in the HUD prefab (SafeFrame/TopRight/Clock).
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Clock Style", fileName = "ClockStyle")]
    public sealed class ClockStyle : ScriptableObject
    {
        [BoxGroup("Time")] public TMP_FontAsset TimeFont;
        [BoxGroup("Time"), Tooltip("Material preset of the time font (e.g. its Underlay variant for a drop shadow). Empty: the font's default")]
        public Material TimeMaterial;
        [BoxGroup("Time"), Min(1f)] public float TimeFontSize = 58f;
        [BoxGroup("Time")] public Color TimeColor = new(0.97f, 0.97f, 0.95f, 1f);

        [BoxGroup("Labels")] public TMP_FontAsset LabelFont;
        [BoxGroup("Labels")] public Material LabelMaterial;
        [BoxGroup("Labels"), Min(1f)] public float LabelFontSize = 26f;
        [BoxGroup("Labels")] public Color DayColor = new(0.97f, 0.97f, 0.95f, 1f);
        [BoxGroup("Labels"), Tooltip("HUD table; {0} = day number")] public LocalizedString DayFormat = Loc.Hud("clock.day");

        [BoxGroup("Backing"), Tooltip("Soft shape behind the clock so it reads over a bright sky. Empty sprite + clear colour = none")]
        public Sprite BackingSprite;
        [BoxGroup("Backing")] public Color BackingColor = new(0f, 0f, 0f, 0.45f);

        [BoxGroup("Phases"), SerializeField, Tooltip("Name and colour shown for each DayPhase")]
        public Dictionary<DayPhase, PhaseLabel> Phases = new()
        {
            { DayPhase.Dawn, new PhaseLabel(Loc.Hud("phase.dawn"), new Color(1f, 0.72f, 0.66f, 1f)) },
            { DayPhase.Morning, new PhaseLabel(Loc.Hud("phase.morning"), new Color(1f, 0.86f, 0.45f, 1f)) },
            { DayPhase.Afternoon, new PhaseLabel(Loc.Hud("phase.afternoon"), new Color(1f, 0.76f, 0.4f, 1f)) },
            { DayPhase.Dusk, new PhaseLabel(Loc.Hud("phase.dusk"), new Color(1f, 0.56f, 0.32f, 1f)) },
            { DayPhase.Evening, new PhaseLabel(Loc.Hud("phase.evening"), new Color(0.72f, 0.7f, 1f, 1f)) },
            { DayPhase.Midnight, new PhaseLabel(Loc.Hud("phase.midnight"), new Color(0.62f, 0.74f, 1f, 1f)) },
        };

        // A missing phase shows its enum name in the day colour.
        public string PhaseText(DayPhase phase) =>
            Phases.TryGetValue(phase, out PhaseLabel label) && label.Text != null && !label.Text.IsEmpty ? label.Text.Text() : phase.ToString();

        public Color PhaseColor(DayPhase phase) => Phases.TryGetValue(phase, out PhaseLabel label) ? label.Color : DayColor;

#if UNITY_EDITOR
        // Edit-time preview only: clocks re-apply their style in Awake.
        void OnValidate() => StyleUtils.RefreshViews<ClockView>(this, clock => clock.Style, clock => clock.ApplyStyle());
#endif
    }
}
