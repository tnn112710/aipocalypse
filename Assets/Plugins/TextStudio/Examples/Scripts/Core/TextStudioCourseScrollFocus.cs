using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Keeps the keyboard-selected lesson inside its scroll viewport.</summary>
    [UnityEngine.AddComponentMenu("")]
    public sealed class TextStudioCourseScrollFocus : MonoBehaviour, ISelectHandler
    {
        [SerializeField] ScrollRect _scroll;
        public void Configure(ScrollRect scroll) => _scroll = scroll;

        public void OnSelect(BaseEventData eventData)
        {
            if (_scroll == null || _scroll.content == null || _scroll.viewport == null) return;
            Canvas.ForceUpdateCanvases();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_scroll.viewport, transform);
            Rect viewport = _scroll.viewport.rect;
            float adjustment = bounds.max.y > viewport.yMax ? viewport.yMax - bounds.max.y : bounds.min.y < viewport.yMin ? viewport.yMin - bounds.min.y : 0;
            if (Mathf.Abs(adjustment) < .01f) return;
            _scroll.StopMovement();
            _scroll.content.anchoredPosition += new Vector2(0, adjustment);
        }
    }
}
