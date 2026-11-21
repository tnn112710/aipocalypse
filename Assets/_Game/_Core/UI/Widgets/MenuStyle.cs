using Ami.BroAudio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // Look and sound of the menus (title screen, pause, save slots, confirm dialog). One asset: Data/UI/MenuStyle.
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Menu Style", fileName = "MenuStyle")]
    public sealed class MenuStyle : ScriptableObject
    {
        [Tooltip("Buttons and labels")] public TMP_FontAsset Font;
        [Tooltip("Screen titles")] public TMP_FontAsset TitleFont;
        [Tooltip("Small text: hints, slot details, messages")] public TMP_FontAsset BodyFont;
        [Tooltip("Strip behind buttons, rows and panels (9-sliced)")] public Sprite ButtonSprite;
        [Tooltip("Above 1 shrinks the sliced edges")] public float CornerScale = 3f;
        [Tooltip("Dark side under the menu's column (horizontal gradient)")] public Sprite ShadeSprite;
        public Color Backdrop = new(0f, 0f, 0f, 0.55f);
        [Tooltip("Rows, dialog buttons")] public Color Button = new(0.086f, 0.188f, 0.302f, 0.9f);
        [Tooltip("Hovered or selected: rows and the strip behind text buttons")] public Color ButtonHover = new(0.114f, 0.561f, 0.769f, 0.94f);
        public Color ButtonPressed = new(0.549f, 0.902f, 1f, 1f);
        public Color ButtonDisabled = new(0.05f, 0.05f, 0.06f, 0.45f);
        [Tooltip("The current game's slot")] public Color Accent = new(0.114f, 0.431f, 0.588f, 0.92f);
        [Tooltip("Titles' second colour, tags")] public Color Tape = new(0.86f, 0.74f, 0.27f, 1f);
        public Color Text = Color.white;
        public Color InfoText = new(1f, 1f, 1f, 0.8f);
        public Color InfoBox = new(0f, 0f, 0f, 0.35f);
        public Color Panel = new(0.047f, 0.094f, 0.157f, 0.96f);
        public Color Danger = new(0.72f, 0.22f, 0.18f, 0.95f);

        [Header("Sounds")]
        [Tooltip("A button gets the focus (mouse over, gamepad move)")] public SoundID HoverSound;
        public SoundID ClickSound;
        [Tooltip("Esc / B steps back")] public SoundID BackSound;

        // Colours for a filled button: normal, hover, pressed, disabled; `normal` overrides the plain colour (the accent).
        public ColorBlock Colors(Color? normal = null)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = normal ?? Button;
            colors.highlightedColor = normal.HasValue ? Color.Lerp(normal.Value, Color.white, 0.2f) : ButtonHover;
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = ButtonPressed;
            colors.disabledColor = ButtonDisabled;
            colors.fadeDuration = 0.06f;
            return colors;
        }

        // Colours for a text button: no strip until hovered or selected.
        public ColorBlock TextColors()
        {
            ColorBlock colors = Colors();
            colors.normalColor = colors.disabledColor = Color.clear;
            return colors;
        }
    }
}
