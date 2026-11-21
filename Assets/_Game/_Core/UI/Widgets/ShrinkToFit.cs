using UnityEngine;
using UnityEngine.EventSystems;

namespace MB13.Aipocalypse.Core.UI
{
    // On a stretched full-screen layout of fixed-size panels (inventory, menus) built at the reference size: scales it down
    // uniformly when the canvas is shorter (21:9) or narrower (16:10, Steam Deck) than that, and widens its rect to match,
    // so the panels keep their spacing instead of overlapping. Never grows it: the CanvasScaler handles bigger screens.
    // Runtime only, so editing the prefab at another Game view size writes nothing.
    [RequireComponent(typeof(RectTransform))]
    public sealed class ShrinkToFit : UIBehaviour
    {
        [SerializeField] Vector2 _referenceSize = new(1920f, 1080f);

        bool _fitting;

        protected override void OnEnable()
        {
            base.OnEnable();
            Fit();
        }

        protected override void OnRectTransformDimensionsChange() => Fit();

        void Fit()
        {
            if (_fitting || !Application.isPlaying || transform.parent is not RectTransform parent)
                return;

            Vector2 size = parent.rect.size;
            float scale = Mathf.Min(1f, size.x / _referenceSize.x, size.y / _referenceSize.y);
            if (scale <= 0f)
                return;

            _fitting = true;
            var rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size / scale - size;
            rect.localScale = new Vector3(scale, scale, 1f);
            _fitting = false;
        }
    }
}
