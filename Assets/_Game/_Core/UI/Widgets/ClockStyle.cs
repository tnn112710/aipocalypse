using Aipocalypse.Core.WorldTimeSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Aipocalypse.Core.UI
{
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

        [BoxGroup("Phases")] public string DawnLabel = "Dawn";
        [BoxGroup("Phases")] public Color DawnColor = new(1f, 0.72f, 0.66f, 1f);
        [BoxGroup("Phases")] public string MorningLabel = "Morning";
        [BoxGroup("Phases")] public Color MorningColor = new(1f, 0.86f, 0.45f, 1f);
        [BoxGroup("Phases")] public string AfternoonLabel = "Afternoon";
        [BoxGroup("Phases")] public Color AfternoonColor = new(1f, 0.76f, 0.4f, 1f);
        [BoxGroup("Phases")] public string DuskLabel = "Dusk";
        [BoxGroup("Phases")] public Color DuskColor = new(1f, 0.56f, 0.32f, 1f);
        [BoxGroup("Phases")] public string EveningLabel = "Evening";
        [BoxGroup("Phases")] public Color EveningColor = new(0.72f, 0.7f, 1f, 1f);
        [BoxGroup("Phases")] public string MidnightLabel = "Midnight";
        [BoxGroup("Phases")] public Color MidnightColor = new(0.62f, 0.74f, 1f, 1f);

        public string PhaseLabel(DayPhase phase) => phase switch
        {
            DayPhase.Dawn => DawnLabel,
            DayPhase.Morning => MorningLabel,
            DayPhase.Afternoon => AfternoonLabel,
            DayPhase.Dusk => DuskLabel,
            DayPhase.Evening => EveningLabel,
            _ => MidnightLabel,
        };

        public Color PhaseColor(DayPhase phase) => phase switch
        {
            DayPhase.Dawn => DawnColor,
            DayPhase.Morning => MorningColor,
            DayPhase.Afternoon => AfternoonColor,
            DayPhase.Dusk => DuskColor,
            DayPhase.Evening => EveningColor,
            _ => MidnightColor,
        };

#if UNITY_EDITOR
        // Edit-time preview only: clocks re-apply their style in Awake.
        void OnValidate() => UnityEditor.EditorApplication.delayCall += RefreshOpenClocks;

        void RefreshOpenClocks()
        {
            if (this == null)
                return;
            foreach (ClockView clock in FindObjectsByType<ClockView>(FindObjectsInactive.Include))
                if (clock.Style == this)
                    clock.ApplyStyle();
        }
#endif
    }
}
