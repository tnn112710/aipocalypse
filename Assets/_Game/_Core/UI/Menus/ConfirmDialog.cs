using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // A yes / no question over a menu ("Overwrite Save 2?"). No is selected first, so Enter never destroys anything; closing
    // gives the focus back to the button that asked.
    public sealed class ConfirmDialog : MonoBehaviour
    {
        [SerializeField] TMP_Text _message;
        [SerializeField] Button _yes;
        [SerializeField] TMP_Text _yesLabel;
        [SerializeField] Button _no;

        Action _onYes;
        GameObject _asker;
        bool _hooked;

        public bool IsOpen => gameObject.activeSelf;

        public void Ask(string message, string yes, Action onYes)
        {
            Hook();
            _message.text = message;
            _yesLabel.text = yes.ToUpperInvariant();
            _onYes = onYes;
            _asker = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            gameObject.SetActive(true);
            MenuButton.SelectSilently(_no);
        }

        public void Close()
        {
            _onYes = null;
            if (!IsOpen)
                return;
            gameObject.SetActive(false);
            if (_asker != null && _asker.activeInHierarchy && _asker.TryGetComponent(out Selectable asker))
                MenuButton.SelectSilently(asker);
            _asker = null;
        }

        void Hook()
        {
            if (_hooked)
                return;
            _hooked = true;
            _yes.onClick.AddListener(Confirm);
            _no.onClick.AddListener(Close);
        }

        void Confirm()
        {
            Action onYes = _onYes;
            Close();
            onYes?.Invoke();
        }
    }
}
