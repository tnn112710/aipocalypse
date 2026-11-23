using UnityEngine;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // Puts MenuStyle's colours on a button when it wakes, so the menu theme (Highlight, Button) is changed in one asset and not
    // in every prefab. The Button's own colour block is only a baked copy (`Apply` refreshes it in the editor).
    [RequireComponent(typeof(Button))]
    public sealed class MenuButtonColors : MonoBehaviour
    {
        public enum ButtonLook
        {
            [Tooltip("Strip in MenuStyle.Button, Highlight when hovered / selected")] Filled,
            [Tooltip("No strip until hovered / selected")] Text,
            [Tooltip("Keeps its own normal colour (a danger button, a tinted sprite), only hover and pressed follow the style")] Keep,
        }

        [SerializeField] MenuStyle _style;
        [SerializeField] ButtonLook _look;

        void Awake() => Apply();

        public void Apply()
        {
            if (_style == null || !TryGetComponent(out Button button))
                return;
            ColorBlock colors = button.colors;
            colors.highlightedColor = colors.selectedColor = _style.ButtonHover;
            colors.pressedColor = _style.ButtonPressed;
            if (_look == ButtonLook.Filled)
                colors.normalColor = _style.Button;
            else if (_look == ButtonLook.Text)
                colors.normalColor = Color.clear;
            button.colors = colors;
        }
    }
}
