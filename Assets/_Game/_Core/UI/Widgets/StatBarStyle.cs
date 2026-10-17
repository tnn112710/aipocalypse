using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Aipocalypse.Core.UI
{
    // Everything a StatBar looks like. Swap sprites, colors or font here; sizes and placement stay in the prefab.
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Stat Bar Style", fileName = "StatBarStyle")]
    public sealed class StatBarStyle : ScriptableObject
    {
        [BoxGroup("Background")] public Sprite BackgroundSprite;
        [BoxGroup("Background")] public Color BackgroundColor = new(0.08f, 0.08f, 0.08f, 0.6f);

        [BoxGroup("Icon")] public Sprite IconSprite;
        [BoxGroup("Icon")] public Color IconColor = Color.white;
        [BoxGroup("Icon")] public Sprite IconFrameSprite;
        [BoxGroup("Icon")] public Color IconFrameColor = new(0.22f, 0.22f, 0.22f, 0.85f);

        [BoxGroup("Fill")] public Sprite FillSprite;
        [BoxGroup("Fill")] public Color FillColor = Color.white;
        [BoxGroup("Fill"), Tooltip("Used while the owner flags a warning, e.g. stamina exhausted")] public Color WarningColor = new(0.85f, 0.3f, 0.22f);

        [BoxGroup("Trail"), Tooltip("A second fill that lags behind drops so the lost amount stays visible for a moment")] public bool UseTrail;
        [BoxGroup("Trail"), ShowIf(nameof(UseTrail))] public Color TrailColor = new(1f, 0.9f, 0.6f, 0.85f);
        [BoxGroup("Trail"), ShowIf(nameof(UseTrail)), Min(0f)] public float TrailDelay = 0.4f;
        [BoxGroup("Trail"), ShowIf(nameof(UseTrail)), Min(0.01f), Tooltip("Bar fractions per second")] public float TrailSpeed = 0.6f;
        [BoxGroup("Trail"), ShowIf(nameof(UseTrail)), Range(0f, 1f), Tooltip("Only drops at least this big (bar fraction) restart the delay, so a steady drain (sprinting) is followed instead of frozen. 0 = every drop")]
        public float TrailHoldMinDrop;

        [BoxGroup("Label")] public bool ShowLabel = true;
        [BoxGroup("Label"), ShowIf(nameof(ShowLabel))] public TMP_FontAsset Font;
        [BoxGroup("Label"), ShowIf(nameof(ShowLabel))] public Color LabelColor = Color.white;
        [BoxGroup("Label"), ShowIf(nameof(ShowLabel)), Min(1f)] public float FontSize = 20f;

#if UNITY_EDITOR
        // Edit-time preview only: bars re-apply their style in Awake, so play mode and builds never depend on this.
        void OnValidate() => UnityEditor.EditorApplication.delayCall += RefreshOpenBars;

        void RefreshOpenBars()
        {
            if (this == null)
                return;

            foreach (StatBar bar in FindObjectsByType<StatBar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (bar.Style == this)
                    bar.ApplyStyle();
            }
        }
#endif
    }
}
