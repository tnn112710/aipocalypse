using UnityEngine;
using UnityEngine.EventSystems;

namespace SunfishCreations.UI
{
    public class UIButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            OnPress?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            OnRelease?.Invoke();
        }

        [System.Serializable]
        public class VoidEvent : UnityEngine.Events.UnityEvent { }

        public VoidEvent OnPress = new VoidEvent();
        public VoidEvent OnRelease = new VoidEvent();
    }
}