using System.Collections.Generic;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.EquipmentSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    // Everything the inventory screen and its item slots look and sound like (pickup sounds too). Sizes and placement stay
    // in the prefabs.
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Inventory Style", fileName = "InventoryStyle")]
    public sealed class InventoryStyle : ScriptableObject
    {
        [BoxGroup("Text")] public TMP_FontAsset Font;
        [BoxGroup("Text")] public Color TextColor = new(0.95f, 0.95f, 0.95f, 1f);
        [BoxGroup("Text")] public Color DimTextColor = new(0.75f, 0.75f, 0.75f, 0.9f);
        [BoxGroup("Text"), Tooltip("Refusals and warnings (\"Empty the backpack first\")")] public Color WarningColor = new(1f, 0.45f, 0.35f, 1f);

        [BoxGroup("Slot")] public Sprite SlotSprite;
        [BoxGroup("Slot")] public Color SlotColor = new(0.16f, 0.17f, 0.19f, 0.92f);
        [BoxGroup("Slot")] public Color EmptySlotColor = new(0.12f, 0.13f, 0.15f, 0.75f);
        [BoxGroup("Slot"), Tooltip("Hovered slot, and the slot a dragged item would land on")] public Sprite HighlightSprite;
        [BoxGroup("Slot")] public Color HighlightColor = new(1f, 0.85f, 0.55f, 1f);
        [BoxGroup("Slot")] public Color IconColor = Color.white;
        [BoxGroup("Slot"), Tooltip("Faded silhouette in an empty equipment slot")] public Color EmptyIconColor = new(1f, 1f, 1f, 0.18f);
        [BoxGroup("Slot"), Range(0f, 1f), Tooltip("Icon alpha of the slot an item is being dragged out of")] public float DraggedAlpha = 0.35f;
        [BoxGroup("Slot"), Min(1f)] public float NameFontSize = 13f;
        [BoxGroup("Slot"), Min(1f)] public float CountFontSize = 16f;
        [BoxGroup("Slot"), Min(1f)] public float KeyFontSize = 15f;
        [BoxGroup("Slot"), Min(1f)] public float CaptionFontSize = 15f;
        [BoxGroup("Slot"), Tooltip("Silhouette per kind of slot (gun, melee, head, body, face, backpack, lantern)")]
        [SerializeField] Dictionary<ItemCategory, Sprite> _emptyIcons = new();

        [BoxGroup("Rarity"), Tooltip("Slot border and tooltip title per rarity")]
        [SerializeField] Dictionary<ItemRarity, Color> _rarityColors = new();
        [BoxGroup("Rarity"), Range(0f, 1f), Tooltip("Alpha of the rarity border on a slot")] public float RarityBorderAlpha = 0.7f;

        [BoxGroup("Panel")] public Sprite PanelSprite;
        [BoxGroup("Panel")] public Color PanelColor = new(0.06f, 0.07f, 0.08f, 0.82f);
        [BoxGroup("Panel"), Tooltip("Full-screen tint behind the panels; dropping an item on it throws the item away")]
        public Color BackdropColor = new(0f, 0f, 0f, 0.45f);
        [BoxGroup("Panel")] public Color WeightBarColor = new(0.85f, 0.85f, 0.85f, 1f);
        [BoxGroup("Panel"), Tooltip("Weight bar past the carry limit")] public Color OverweightColor = new(1f, 0.45f, 0.35f, 1f);

        [BoxGroup("Inspector"), Tooltip("Gun part slots a dragged part fits")] public Color FitColor = new(0.45f, 0.75f, 0.4f, 0.95f);
        [BoxGroup("Inspector"), Tooltip("A stat a part would make better")] public Color BetterColor = new(0.45f, 0.9f, 0.45f, 1f);
        [BoxGroup("Inspector"), Tooltip("A stat a part would make worse")] public Color WorseColor = new(1f, 0.45f, 0.35f, 1f);
        [BoxGroup("Inspector"), Tooltip("The line from a hovered part slot to its mount on the gun")] public Color LeaderColor = new(1f, 0.85f, 0.55f, 0.9f);

        [BoxGroup("Sounds")] public SoundID OpenSound;
        [BoxGroup("Sounds")] public SoundID CloseSound;
        [BoxGroup("Sounds"), Tooltip("An item dropped into a slot (moved, merged, swapped, taken)")] public SoundID PlaceSound;
        [BoxGroup("Sounds"), Tooltip("A move or pickup refused (full, no room, wrong slot)")] public SoundID RefusedSound;
        [BoxGroup("Sounds"), Tooltip("A searched stack revealed")] public SoundID RevealSound;
        [BoxGroup("Sounds"), Tooltip("Something picked up from the ground")] public SoundID PickupSound;
        [BoxGroup("Sounds"), Tooltip("Tokens picked up (instead of the pickup sound)")] public SoundID TokensSound;
        [BoxGroup("Sounds"), Tooltip("A part fitted to the inspected gun")] public SoundID AttachSound;
        [BoxGroup("Sounds"), Tooltip("A part taken off the inspected gun")] public SoundID DetachSound;

        public Color RarityColor(ItemRarity rarity) =>
            _rarityColors != null && _rarityColors.TryGetValue(rarity, out Color color) ? color : TextColor;

        // The silhouette for an empty slot that takes `accepts`: the first category with one.
        public Sprite EmptyIcon(ItemCategory accepts)
        {
            if (_emptyIcons == null)
                return null;
            foreach ((ItemCategory category, Sprite sprite) in _emptyIcons)
                if ((accepts & category) != 0 && sprite != null)
                    return sprite;
            return null;
        }

#if UNITY_EDITOR
        // Setup scripts only.
        public void SetRarityColor(ItemRarity rarity, Color color)
        {
            _rarityColors ??= new Dictionary<ItemRarity, Color>();
            _rarityColors[rarity] = color;
        }

        public void SetEmptyIcon(ItemCategory category, Sprite sprite)
        {
            _emptyIcons ??= new Dictionary<ItemCategory, Sprite>();
            _emptyIcons[category] = sprite;
        }

        // Edit-time preview only: slots re-apply their style in Awake.
        void OnValidate() => StyleUtils.RefreshViews<ItemSlotView>(this, slot => slot.Style, slot => slot.ApplyStyle());
#endif
    }
}
