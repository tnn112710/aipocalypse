using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // Look of the menus (pause, main menu, save slots, confirm dialog). One asset: Data/UI/MenuStyle.
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Menu Style", fileName = "MenuStyle")]
    public sealed class MenuStyle : ScriptableObject
    {
        public TMP_FontAsset Font;
        [Tooltip("Rounded box for buttons and panels (9-sliced)")] public Sprite ButtonSprite;
        [Tooltip("Below 1 rounds the sliced corners more")] public float CornerScale = 0.35f;
        public Color Backdrop = new(0.02f, 0.04f, 0.07f, 0.72f);
        public Color Button = new(0.34f, 0.40f, 0.48f, 0.95f);
        public Color ButtonHover = new(0.46f, 0.53f, 0.62f, 1f);
        public Color ButtonPressed = new(0.82f, 0.46f, 0.26f, 1f);
        public Color ButtonDisabled = new(0.24f, 0.27f, 0.31f, 0.7f);
        [Tooltip("The current game's slot")] public Color Accent = new(0.96f, 0.56f, 0.30f, 1f);
        public Color Text = Color.white;
        public Color InfoText = new(1f, 1f, 1f, 0.85f);
        public Color InfoBox = new(0f, 0f, 0f, 0.18f);
        public Color Danger = new(0.78f, 0.25f, 0.22f, 1f);

        // Colours for a button: normal, hover, pressed, disabled; `normal` overrides the plain button colour (the accent).
        public ColorBlock Colors(Color? normal = null)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = normal ?? Button;
            colors.highlightedColor = normal.HasValue ? Color.Lerp(normal.Value, Color.white, 0.2f) : ButtonHover;
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = ButtonPressed;
            colors.disabledColor = ButtonDisabled;
            colors.fadeDuration = 0.08f;
            return colors;
        }
    }
}
