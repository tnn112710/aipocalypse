using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Aipocalypse.Core.UI
{
    // Everything a hotbar slot looks like. Sizes and placement stay in the HotbarSlot prefab.
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Hotbar Slot Style", fileName = "HotbarSlotStyle")]
    public sealed class HotbarSlotStyle : ScriptableObject
    {
        [BoxGroup("Tile")] public Sprite BackgroundSprite;
        [BoxGroup("Tile")] public Color BackgroundColor = new(0.08f, 0.08f, 0.08f, 0.55f);
        [BoxGroup("Tile"), Tooltip("Background tint when the slot has no item")] public Color EmptyBackgroundColor = new(0.08f, 0.08f, 0.08f, 0.35f);
        [BoxGroup("Tile")] public Color IconColor = Color.white;

        [BoxGroup("Selected")] public Sprite FrameSprite;
        [BoxGroup("Selected")] public Color FrameColor = new(1f, 0.72f, 0.6f, 1f);
        [BoxGroup("Selected"), Min(0.5f), Tooltip("Tile scale while selected")] public float SelectedScale = 1.06f;

        [BoxGroup("Key")] public Sprite KeySprite;
        [BoxGroup("Key")] public Color KeyColor = new(0.95f, 0.95f, 0.95f, 0.95f);
        [BoxGroup("Key")] public Color SelectedKeyColor = new(1f, 0.72f, 0.6f, 1f);
        [BoxGroup("Key")] public TMP_FontAsset KeyFont;
        [BoxGroup("Key")] public Color KeyLabelColor = new(0.1f, 0.1f, 0.1f, 1f);
        [BoxGroup("Key"), Min(1f)] public float KeyFontSize = 20f;

        [BoxGroup("Ammo"), Tooltip("Magazine / reserve count in the tile corner, for guns")] public TMP_FontAsset AmmoFont;
        [BoxGroup("Ammo")] public Color AmmoColor = new(0.95f, 0.95f, 0.95f, 1f);
        [BoxGroup("Ammo"), Tooltip("When the magazine is empty or the gun is jammed")] public Color AmmoWarningColor = new(1f, 0.35f, 0.3f, 1f);
        [BoxGroup("Ammo"), Min(1f)] public float AmmoFontSize = 16f;

#if UNITY_EDITOR
        // Edit-time preview only: slots re-apply their style in Awake.
        void OnValidate() => UnityEditor.EditorApplication.delayCall += RefreshOpenSlots;

        void RefreshOpenSlots()
        {
            if (this == null)
                return;

            foreach (HotbarSlotView slot in FindObjectsByType<HotbarSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (slot.Style == this)
                    slot.ApplyStyle();
            }
        }
#endif
    }
}
