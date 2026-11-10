using TMPro;
using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    // The details of the hovered item, beside its slot: name, then a line of facts (count, weight).
    public sealed class ItemTooltipView : MonoBehaviour
    {
        static readonly Vector3[] Corners = new Vector3[4];

        [SerializeField] RectTransform _panel;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _details;
        [SerializeField, Tooltip("Gap between the slot and the tooltip (px at 1080p)")] Vector2 _offset = new(12f, 0f);

        public void Show(RectTransform slot, string title, string details)
        {
            _title.SetText(title);
            _details.SetText(details);
            gameObject.SetActive(true);
            // To the right of the slot, flipped left when it would leave the screen.
            slot.GetWorldCorners(Corners);
            float scale = _panel.lossyScale.x;
            Vector3 right = Corners[2] + new Vector3(_offset.x * scale, _offset.y * scale, 0f);
            bool flip = right.x + _panel.rect.width * scale > Screen.width;
            _panel.pivot = new Vector2(flip ? 1f : 0f, 1f);
            _panel.position = flip ? Corners[1] - new Vector3(_offset.x * scale, -_offset.y * scale, 0f) : right;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
