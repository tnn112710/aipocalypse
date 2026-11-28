using System.Collections.Generic;
using MB13.Aipocalypse.Core.PlayerSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    // Everything the carried-weight gauge looks like: the bag icon (also the fill's shape), a colour per load state, the notch
    // at Comfortable and the label's font. Sizes stay in the HUD.
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Bag Gauge Style", fileName = "BagGaugeStyle")]
    public sealed class BagGaugeStyle : ScriptableObject
    {
        [BoxGroup("Icon"), Tooltip("The bag: drawn dim as the empty shape and filled from the bottom in the load's colour")] public Sprite Icon;
        [BoxGroup("Icon")] public Color EmptyColor = new(1f, 1f, 1f, 0.25f);
        [BoxGroup("Icon"), SerializeField, Tooltip("Fill and label colour per load state; every LoadState needs an entry")]
        public Dictionary<LoadState, Color> Colors = new()
        {
            { LoadState.Light, Color.white },
            { LoadState.NearComfortable, new Color(1f, 0.86f, 0.25f) },
            { LoadState.Heavy, new Color(1f, 0.55f, 0.15f) },
            { LoadState.Overloaded, new Color(0.95f, 0.22f, 0.18f) },
        };
        [BoxGroup("Icon")] public Color NotchColor = new(1f, 1f, 1f, 0.85f);

        [BoxGroup("Label")] public TMP_FontAsset Font;
        [BoxGroup("Label"), Min(1f)] public float FontSize = 22f;

        [BoxGroup("Dimming"), Range(0f, 1f), Tooltip("Alpha while Light")] public float DimAlpha = 0.5f;
        [BoxGroup("Dimming"), Min(0.01f), Tooltip("Alpha change per real second")] public float FadeSpeed = 2f;

        public Color ColorFor(LoadState state) => Colors != null && Colors.TryGetValue(state, out Color color) ? color : Color.white;

#if UNITY_EDITOR
        // Edit-time preview only: gauges re-apply their style in Awake.
        void OnValidate() => StyleUtils.RefreshViews<BagGauge>(this, gauge => gauge.Style, gauge => gauge.ApplyStyle());
#endif
    }
}
