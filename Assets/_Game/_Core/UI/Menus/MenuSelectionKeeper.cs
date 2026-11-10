using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // Next to a menu's EventSystem: a mouse click on empty space clears the selection, which would leave a gamepad with
    // nothing to move from; this gives the focus back to the last button that had it, if it's still on screen.
    [RequireComponent(typeof(EventSystem))]
    public sealed class MenuSelectionKeeper : MonoBehaviour
    {
        EventSystem _events;
        GameObject _last;

        void Awake() => _events = GetComponent<EventSystem>();

        void Update()
        {
            GameObject current = _events.currentSelectedGameObject;
            if (current != null && current.activeInHierarchy)
                _last = current;
            else if (_last != null && _last.activeInHierarchy && _last.TryGetComponent(out Selectable selectable))
                MenuButton.SelectSilently(selectable);
        }
    }
}
