using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Lets actual pointer or directional input take ownership without treating programmatic focus as input.</summary>
    [UnityEngine.AddComponentMenu("")]
    public sealed class TextStudioCourseManualInput : MonoBehaviour, IPointerDownHandler, IMoveHandler
    {
        [SerializeField] UnityEvent _onInput = new UnityEvent();
        public UnityEvent OnInput => _onInput;
        public void OnPointerDown(PointerEventData eventData) => _onInput.Invoke();
        public void OnMove(AxisEventData eventData) { if (eventData.moveDir != MoveDirection.None) _onInput.Invoke(); }
    }
}
