using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunfishCreations.UI
{
    [RequireComponent(typeof(Button))]
    public class AutoFocusButton : MonoBehaviour
    {
        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        public virtual void GiveFocus()
        {
            StartCoroutine(SetFocusNextFrame());
        }

        private void OnEnable()
        {
            // Delay one frame to ensure EventSystem is ready
            StartCoroutine(SetFocusNextFrame());
        }

        private System.Collections.IEnumerator SetFocusNextFrame()
        {
            yield return null;

            // If there is no EventSystem, don’t crash.
            if (EventSystem.current == null)
            {
                Debug.LogWarning("No EventSystem in the scene! AutoFocusButton needs one.");
                yield break;
            }

            EventSystem.current.SetSelectedGameObject(_button.gameObject);
        }
    }
}