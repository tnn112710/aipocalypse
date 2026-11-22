using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MB13.Aipocalypse.Core.UI
{
    // A slot the inventory screen drags from and drops on: its own tiles (ItemSlotView), or the HUD hotbar's while the screen
    // is open (HotbarSlotView). Reports pointer and drag events; the presenter decides what they do.
    public interface ISlotView
    {
        event Action<ISlotView, PointerEventData> Clicked;
        event Action<ISlotView, PointerEventData> DragStarted;
        event Action<ISlotView, PointerEventData> Dragging;
        event Action<ISlotView, PointerEventData> DragEnded;
        event Action<ISlotView, PointerEventData> DroppedOn;
        event Action<ISlotView> HoverChanged;

        // Set by the presenter to find what this view shows.
        int Index { get; set; }
        bool IsHovered { get; }
        RectTransform Rect { get; }

        // The item is being dragged out of this slot: its icon fades.
        void SetDragged(bool dragged);
    }
}
