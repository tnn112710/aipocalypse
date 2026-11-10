using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // A menu button's feel: the mouse over it selects it (one highlight for mouse and gamepad), getting the focus ticks,
    // a click clicks (MenuSounds above it), and its labels dim while it can't be pressed (the colour tint only reaches the
    // strip behind them).
    [RequireComponent(typeof(Button))]
    public sealed class MenuButton : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        const float DisabledAlpha = 0.35f;

        Button _button;
        TMP_Text[] _labels;
        MenuSounds _sounds;
        bool _interactable = true;
        bool _silent;

        // Gives `target` the focus without the tick (a menu opening on its first button).
        public static void SelectSilently(Selectable target)
        {
            if (target == null || EventSystem.current == null)
                return;
            target.TryGetComponent(out MenuButton button);
            if (button != null)
                button._silent = true;
            EventSystem.current.SetSelectedGameObject(target.gameObject);
            if (button != null)
                button._silent = false;
        }

        void Awake()
        {
            _button = GetComponent<Button>();
            _labels = GetComponentsInChildren<TMP_Text>(true);
            _sounds = GetComponentInParent<MenuSounds>(true);
            _button.onClick.AddListener(() => _sounds?.Click());
            Dim(_button.IsInteractable());
        }

        void LateUpdate()
        {
            bool interactable = _button.IsInteractable();
            if (interactable != _interactable)
                Dim(interactable);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button.IsInteractable() && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!_silent)
                _sounds?.Hover();
        }

        void Dim(bool interactable)
        {
            _interactable = interactable;
            foreach (TMP_Text label in _labels)
                label.alpha = interactable ? 1f : DisabledAlpha;
        }
    }
}
