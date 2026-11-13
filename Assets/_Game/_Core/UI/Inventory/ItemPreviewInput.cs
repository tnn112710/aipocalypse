using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MB13.Aipocalypse.Core.UI
{
    // The inspector's 3D view as a pointer target: dragging turns the item, the wheel zooms, and an item dragged from a
    // slot can be dropped on it (a part onto the gun).
    public sealed class ItemPreviewInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler, IDropHandler
    {
        public event Action<Vector2> Turned;
        public event Action<float> Zoomed;
        public event Action Dropped;

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Turned?.Invoke(eventData.delta);
        }

        public void OnScroll(PointerEventData eventData) => Zoomed?.Invoke(eventData.scrollDelta.y);

        public void OnDrop(PointerEventData eventData) => Dropped?.Invoke();
    }
}
