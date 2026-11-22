using System;
using MB13.Aipocalypse.Core.LocalizationSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // An item's right-click menu in the inventory screen: Use (consumables), Drop (greyed out while locked), Lock / Unlock.
    // Sits on a clear full-screen blocker: a click anywhere off its buttons closes it.
    public sealed class ItemContextMenu : MonoBehaviour, IPointerClickHandler
    {
        public enum Choice { Use, Drop, Lock }

        static readonly LocalizedString UseText = Loc.UI("inventory.menu_use");
        static readonly LocalizedString DropText = Loc.UI("inventory.menu_drop");
        static readonly LocalizedString LockText = Loc.UI("inventory.menu_lock");
        static readonly LocalizedString UnlockText = Loc.UI("inventory.menu_unlock");

        [SerializeField, Tooltip("The buttons' column, placed at the pointer")] RectTransform _panel;
        [SerializeField] Button _use;
        [SerializeField] Button _drop;
        [SerializeField] Button _lock;
        [SerializeField] TMP_Text _useLabel;
        [SerializeField] TMP_Text _dropLabel;
        [SerializeField] TMP_Text _lockLabel;

        public event Action<Choice> Chosen;

        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            _use.onClick.AddListener(() => Choose(Choice.Use));
            _drop.onClick.AddListener(() => Choose(Choice.Drop));
            _lock.onClick.AddListener(() => Choose(Choice.Lock));
        }

        // Opens at `screenPosition` (its top-left corner), flipped to stay on screen.
        public void Show(Vector2 screenPosition, bool canUse, bool locked)
        {
            _use.gameObject.SetActive(canUse);
            _drop.interactable = !locked;
            _useLabel.SetText(UseText.Text());
            _dropLabel.SetText(DropText.Text());
            _lockLabel.SetText((locked ? UnlockText : LockText).Text());
            gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
            Vector2 size = _panel.rect.size * _panel.lossyScale.x;
            bool left = screenPosition.x + size.x > Screen.width;
            bool up = screenPosition.y - size.y < 0f;
            _panel.pivot = new Vector2(left ? 1f : 0f, up ? 0f : 1f);
            _panel.position = screenPosition;
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        void Choose(Choice choice)
        {
            Hide();
            Chosen?.Invoke(choice);
        }

        public void OnPointerClick(PointerEventData eventData) => Hide();
    }
}
