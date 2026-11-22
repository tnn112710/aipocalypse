using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // One hotbar slot: item icon on a tile, a selection frame, an ammo count in the corner, and the key cap below.
    // Knows nothing about items or input. While the inventory screen is open it's also a drag-and-drop slot (ISlotView):
    // its pointer events only arrive then, when the hotbar's raycaster is on (HotbarHud.SetInteractive).
    public sealed class HotbarSlotView : MonoBehaviour, ISlotView, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField, InlineEditor] HotbarSlotStyle _style;
        [SerializeField, Tooltip("Scaled while selected")] RectTransform _tile;
        [SerializeField] Image _background;
        [SerializeField] Image _icon;
        [SerializeField] Image _frame;
        [SerializeField] Image _keyCap;
        [SerializeField] TMP_Text _keyLabel;
        [SerializeField, Tooltip("Magazine / reserve, bottom-right of the tile")] TMP_Text _ammoLabel;
        [SerializeField, Tooltip("Corner icon of a locked item (can't be dropped)")] Image _lock;

        Sprite _iconSprite;
        bool _selected;
        bool _hovered;
        bool _dragged;
        // Last ammo shown, so a refresh of every slot rebuilds only the labels that changed. Rounds -1 = hidden.
        int _shownRounds = -1;
        int _shownReserve = -1;
        bool _shownInfinite;
        bool _shownWarning;

        public event Action<ISlotView, PointerEventData> Clicked;
        public event Action<ISlotView, PointerEventData> DragStarted;
        public event Action<ISlotView, PointerEventData> Dragging;
        public event Action<ISlotView, PointerEventData> DragEnded;
        public event Action<ISlotView, PointerEventData> DroppedOn;
        public event Action<ISlotView> HoverChanged;

        public HotbarSlotStyle Style => _style;
        public bool IsHovered => _hovered;
        public RectTransform Rect => (RectTransform)transform;
        public int Index { get; set; }

        void Awake() => ApplyStyle();

        public void SetKey(string key) => _keyLabel.SetText(key);

        // null = empty slot.
        public void SetIcon(Sprite icon)
        {
            _iconSprite = icon;
            Refresh();
        }

        // "count" or "count/reserve" (rounds over the ammo left, a stack's size). `warning`: empty magazine or jammed.
        public void SetBadge(int count, int reserve, bool showReserve, bool warning)
        {
            if (_ammoLabel == null)
                return;
            _ammoLabel.enabled = true;
            bool infiniteReserve = !showReserve;
            if (count != _shownRounds || infiniteReserve != _shownInfinite || (!infiniteReserve && reserve != _shownReserve))
            {
                if (infiniteReserve)
                    _ammoLabel.SetText("{0}", (float)count);
                else
                    _ammoLabel.SetText("{0}/{1}", (float)count, (float)reserve);
                _shownRounds = count;
                _shownReserve = reserve;
                _shownInfinite = infiniteReserve;
            }
            if (warning != _shownWarning)
            {
                _shownWarning = warning;
                PaintAmmo();
            }
        }

        public void HideBadge()
        {
            if (_ammoLabel != null)
                _ammoLabel.enabled = false;
            _shownRounds = -1;
        }

        void PaintAmmo()
        {
            if (_style != null)
                _ammoLabel.color = _shownWarning ? _style.AmmoWarningColor : _style.AmmoColor;
        }

        public void SetLocked(bool locked)
        {
            if (_lock != null && _lock.enabled != locked)
                _lock.enabled = locked;
        }

        public void SetDragged(bool dragged)
        {
            if (dragged == _dragged)
                return;
            _dragged = dragged;
            Refresh();
        }

        // The tile takes pointer hits (on only while the inventory screen is open).
        public void SetInteractive(bool interactive)
        {
            _background.raycastTarget = interactive;
            if (interactive || !_hovered)
                return;
            _hovered = false;
            Refresh();
        }

        public void SetSelected(bool selected)
        {
            if (selected == _selected)
                return;
            _selected = selected;
            Refresh();
        }

        [Button, PropertyOrder(-1)]
        public void ApplyStyle()
        {
            if (_style == null)
                return;

            _background.sprite = _style.BackgroundSprite;
            _background.type = StyleUtils.ImageTypeFor(_style.BackgroundSprite);
            _frame.sprite = _style.FrameSprite;
            _frame.type = StyleUtils.ImageTypeFor(_style.FrameSprite);
            _frame.color = _style.FrameColor;
            _icon.preserveAspect = true;

            _keyCap.sprite = _style.KeySprite;
            _keyCap.type = StyleUtils.ImageTypeFor(_style.KeySprite);
            if (_style.KeyFont != null)
                _keyLabel.font = _style.KeyFont;
            _keyLabel.color = _style.KeyLabelColor;
            _keyLabel.fontSize = _style.KeyFontSize;

            if (_ammoLabel != null)
            {
                if (_style.AmmoFont != null)
                    _ammoLabel.font = _style.AmmoFont;
                _ammoLabel.fontSize = _style.AmmoFontSize;
                PaintAmmo();
            }

            Refresh();
        }

        void Refresh()
        {
            bool hasItem = _iconSprite != null;
            _icon.sprite = _iconSprite;
            _icon.enabled = hasItem;

            if (_style == null)
                return;

            _background.color = hasItem ? _style.BackgroundColor : _style.EmptyBackgroundColor;
            Color icon = _style.IconColor;
            icon.a *= _dragged ? _style.DraggedIconAlpha : 1f;
            _icon.color = icon;
            // Hovered while the inventory is open: the frame marks where a drop lands.
            _frame.enabled = _selected || _hovered;
            _frame.color = _selected ? _style.FrameColor : _style.HoverFrameColor;
            _keyCap.color = _selected ? _style.SelectedKeyColor : _style.KeyColor;
            _tile.localScale = Vector3.one * (_selected ? _style.SelectedScale : 1f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Refresh();
            HoverChanged?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Refresh();
            HoverChanged?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this, eventData);
        public void OnBeginDrag(PointerEventData eventData) => DragStarted?.Invoke(this, eventData);
        public void OnDrag(PointerEventData eventData) => Dragging?.Invoke(this, eventData);
        public void OnEndDrag(PointerEventData eventData) => DragEnded?.Invoke(this, eventData);
        public void OnDrop(PointerEventData eventData) => DroppedOn?.Invoke(this, eventData);

#if UNITY_EDITOR
        void OnValidate() => UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
                ApplyStyle();
        };
#endif
    }
}
