using System;
using System.Collections.Generic;
using Aipocalypse.Core.WorldTimeSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Aipocalypse.Core.UI
{
    [Serializable]
    public struct PhaseLabel
    {
        public string Text;
        public Color Color;

        public PhaseLabel(string text, Color color)
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
        [BoxGroup("Labels"), Tooltip("{0} = day number")] public string DayFormat = "Day {0}";

        [BoxGroup("Backing"), Tooltip("Soft shape behind the clock so it reads over a bright sky. Empty sprite + clear colour = none")]
        public Sprite BackingSprite;
        [BoxGroup("Backing")] public Color BackingColor = new(0f, 0f, 0f, 0.45f);

        [BoxGroup("Phases"), SerializeField, Tooltip("Name and colour shown for each DayPhase")]
        public Dictionary<DayPhase, PhaseLabel> Phases = new()
        {
            { DayPhase.Dawn, new PhaseLabel("Dawn", new Color(1f, 0.72f, 0.66f, 1f)) },
            { DayPhase.Morning, new PhaseLabel("Morning", new Color(1f, 0.86f, 0.45f, 1f)) },
            { DayPhase.Afternoon, new PhaseLabel("Afternoon", new Color(1f, 0.76f, 0.4f, 1f)) },
            { DayPhase.Dusk, new PhaseLabel("Dusk", new Color(1f, 0.56f, 0.32f, 1f)) },
            { DayPhase.Evening, new PhaseLabel("Evening", new Color(0.72f, 0.7f, 1f, 1f)) },
            { DayPhase.Midnight, new PhaseLabel("Midnight", new Color(0.62f, 0.74f, 1f, 1f)) },
        };

        // A missing phase shows its enum name in the day colour.
        public PhaseLabel Phase(DayPhase phase) => Phases.TryGetValue(phase, out PhaseLabel label) ? label : new PhaseLabel(phase.ToString(), DayColor);

#if UNITY_EDITOR
        // Edit-time preview only: clocks re-apply their style in Awake.
        void OnValidate() => StyleUtils.RefreshViews<ClockView>(this, clock => clock.Style, clock => clock.ApplyStyle());
#endif
    }
}
