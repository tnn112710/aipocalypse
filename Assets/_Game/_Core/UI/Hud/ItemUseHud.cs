using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // The ring that fills under the crosshair while a consumable in hand is being used (HeldConsumable), with its name; or why
    // it couldn't be used ("Health full") for a moment.
    public sealed class ItemUseHud : PlayerHudView
    {
        [SerializeField, Tooltip("Shown while using or refusing")] GameObject _root;
        [SerializeField, Tooltip("Radial Filled image")] Image _fill;
        [SerializeField] TMP_Text _label;
        [SerializeField] Color _color = new(0.95f, 0.95f, 0.95f, 1f);
        [SerializeField] Color _refusedColor = new(1f, 0.45f, 0.35f, 1f);

        string _shown;

        protected override void Unbind(IPlayer player) => Show(false);

        void LateUpdate()
        {
            var held = Player?.HeldItem as HeldConsumable;
            if (held == null)
            {
                Show(false);
                return;
            }
            if (held.IsUsing)
            {
                Show(true);
                _fill.enabled = true;
                _fill.fillAmount = held.UseProgress;
                SetLabel(held.Consumable.DisplayName, _color);
                return;
            }
            string refusal = held.Refusal;
            Show(refusal != null);
            if (refusal == null)
                return;
            _fill.enabled = false;
            SetLabel(refusal, _refusedColor);
        }

        void SetLabel(string text, Color color)
        {
            if (_label == null || ReferenceEquals(text, _shown))
                return;
            _shown = text;
            _label.SetText(text);
            _label.color = color;
        }

        void Show(bool show)
        {
            if (_root != null && _root.activeSelf != show)
                _root.SetActive(show);
        }
    }
}
