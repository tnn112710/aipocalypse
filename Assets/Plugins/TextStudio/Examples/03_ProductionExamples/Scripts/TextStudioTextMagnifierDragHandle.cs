using UnityEngine;
using UnityEngine.EventSystems;

namespace TextStudio.Examples.Production
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class TextStudioTextMagnifierDragHandle : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        [SerializeField] TextStudioTextMagnifierExample _controller;

        public TextStudioTextMagnifierExample Controller => _controller;

        public void Configure(TextStudioTextMagnifierExample controller)
        {
            _controller = controller;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _controller?.BeginPointerDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _controller?.ContinuePointerDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _controller?.EndPointerDrag(eventData);
        }
    }
}
