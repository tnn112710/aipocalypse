using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // The details of the hovered item, beside its slot: name (in its rarity's colour), then lines of facts (rarity, weight,
    // value), then a dim line of what the mouse and keys do with it.
    public sealed class ItemTooltipView : MonoBehaviour
    {
        static readonly Vector3[] Corners = new Vector3[4];

        [SerializeField] RectTransform _panel;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _details;
        [SerializeField, Tooltip("Optional: small dim line of controls at the bottom")] TMP_Text _hint;
        [SerializeField, Tooltip("Gap between the slot and the tooltip (px at 1080p)")] Vector2 _offset = new(12f, 0f);

        public void Show(RectTransform slot, string title, string details, Color titleColor, string hint = null)
        {
            _title.SetText(title);
            _title.color = titleColor;
            _details.SetText(details);
            if (_hint != null)
            {
                _hint.gameObject.SetActive(!string.IsNullOrEmpty(hint));
                _hint.SetText(hint ?? string.Empty);
            }
            gameObject.SetActive(true);
            // To the right of the slot, flipped left when it would leave the screen.
            slot.GetWorldCorners(Corners);
            float scale = _panel.lossyScale.x;
            Vector3 right = Corners[2] + new Vector3(_offset.x * scale, _offset.y * scale, 0f);
            bool flip = right.x + _panel.rect.width * scale > Screen.width;
            Vector3 position = flip ? Corners[1] - new Vector3(_offset.x * scale, -_offset.y * scale, 0f) : right;
            // Hangs down from the slot's top; beside a slot near the bottom (the hotbar row) it would leave the screen, so its
            // bottom lines up with the slot's instead. Its height comes from a ContentSizeFitter: the new text is laid out first.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
            bool up = position.y - _panel.rect.height * scale < 0f;
            if (up)
                position.y = Corners[0].y;
            _panel.pivot = new Vector2(flip ? 1f : 0f, up ? 0f : 1f);
            _panel.position = position;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
