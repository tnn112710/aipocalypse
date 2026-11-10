using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // A yes / no question over a menu ("Overwrite Save 2?"). No is selected first, so Enter never destroys anything.
    public sealed class ConfirmDialog : MonoBehaviour
    {
        [SerializeField] TMP_Text _message;
        [SerializeField] Button _yes;
        [SerializeField] TMP_Text _yesLabel;
        [SerializeField] Button _no;

        Action _onYes;
        bool _hooked;

        public bool IsOpen => gameObject.activeSelf;

        public void Ask(string message, string yes, Action onYes)
        {
            Hook();
            _message.text = message;
            _yesLabel.text = yes;
            _onYes = onYes;
            gameObject.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(_no.gameObject);
        }

        public void Close()
        {
            _onYes = null;
            gameObject.SetActive(false);
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
