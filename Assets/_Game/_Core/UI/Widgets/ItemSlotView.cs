using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // One inventory slot: an item's icon, name and count on a tile with a border in its rarity's colour, an optional key and caption, a silhouette when
    // empty, and a "?" with a filling circle while a search hasn't revealed its item yet. Reports pointer and drag events to its presenter; knows nothing about items or containers.
    public sealed class ItemSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField, InlineEditor] InventoryStyle _style;
        [SerializeField] Image _background;
        [SerializeField] Image _emptyIcon;
        [SerializeField] Image _icon;
        [SerializeField] Image _highlight;
        [SerializeField, Tooltip("Border in the item's rarity colour (InventoryStyle)")] Image _rarityBorder;
        [SerializeField] TMP_Text _name;
        [SerializeField] TMP_Text _count;
        [SerializeField, Tooltip("Hotbar key, top-left")] TMP_Text _key;
        [SerializeField, Tooltip("Slot name under the tile (equipment slots)")] TMP_Text _caption;
        [SerializeField, Tooltip("Shown while the item is still hidden by a search")] GameObject _hidden;
        [SerializeField, Tooltip("Radial Filled image inside Hidden: the reveal in progress")] Image _searchFill;

        bool _hasItem;
        bool _isHidden;
        bool _hovered;
        bool _dragged;
        bool _marked;
        // What the labels show, so a refresh of every slot rebuilds only the text that changed.
        string _shownName;
        int _shownCount = int.MinValue;
        bool _shownWarning;
        string _shownKey;
        string _shownCaption;

        public event Action<ItemSlotView, PointerEventData> Clicked;
        public event Action<ItemSlotView, PointerEventData> DragStarted;
        public event Action<ItemSlotView, PointerEventData> Dragging;
        public event Action<ItemSlotView, PointerEventData> DragEnded;
        public event Action<ItemSlotView, PointerEventData> DroppedOn;
        public event Action<ItemSlotView> HoverChanged;

        public InventoryStyle Style => _style;
        public bool IsHovered => _hovered;
        // Set by the presenter to find what this view shows.
        public int Index { get; set; }

        void Awake() => ApplyStyle();

        // An item: `count` < 0 hides the count; `rarity` colours the border (alpha 0: none).
        public void Show(Sprite icon, string itemName, int count, bool warning = false, Color rarity = default)
        {
            _hasItem = true;
            SetHidden(false);
            if (_rarityBorder != null)
            {
                rarity.a *= _style != null ? _style.RarityBorderAlpha : 1f;
                _rarityBorder.color = rarity;
            }
            _icon.sprite = icon;
            if (_name != null && !ReferenceEquals(itemName, _shownName))
            {
                _shownName = itemName;
                _name.SetText(itemName);
            }
            if (_count != null && (count != _shownCount || warning != _shownWarning))
            {
                _shownCount = count;
                _shownWarning = warning;
                // SetText formats through float, which loses digits past ~16 million (tokens).
                if (count >= 1_000_000)
                    _count.SetText(count.ToString("N0"));
                else if (count >= 0)
                    _count.SetText("{0}", (float)count);
                if (_style != null)
                    _count.color = warning ? _style.WarningColor : _style.TextColor;
            }
            if (_count != null)
                _count.enabled = count >= 0;
            Refresh();
        }

        // Nothing in it: `silhouette` shows what kind of item goes here (equipment slots), or none.
        public void ShowEmpty(Sprite silhouette)
        {
            _hasItem = false;
            SetHidden(false);
            _emptyIcon.sprite = silhouette;
            Refresh();
        }

        // Something not yet revealed: a "?" tile; `progress` 0..1 fills its circle while it's being revealed, < 0 hides it.
        public void ShowHidden(float progress)
        {
            _hasItem = false;
            SetHidden(true);
            SetSearchProgress(progress);
            Refresh();
        }

        public void SetSearchProgress(float progress)
        {
            if (_searchFill == null)
                return;
            bool searching = progress >= 0f;
            if (_searchFill.enabled != searching)
                _searchFill.enabled = searching;
            if (searching)
                _searchFill.fillAmount = progress;
        }

        void SetHidden(bool hidden)
        {
            _isHidden = hidden;
            if (_hidden != null && _hidden.activeSelf != hidden)
                _hidden.SetActive(hidden);
        }

        public void SetKey(string key)
        {
            key ??= string.Empty;
            if (_key == null || _shownKey == key)
                return;
            _shownKey = key;
            _key.enabled = key.Length > 0;
            _key.SetText(key);
        }

        public void SetCaption(string caption)
        {
            caption ??= string.Empty;
            if (_caption == null || _shownCaption == caption)
                return;
            _shownCaption = caption;
            _caption.enabled = caption.Length > 0;
            _caption.SetText(caption);
        }

        // The item is being dragged out of this slot: its icon fades.
        public void SetDragged(bool dragged)
        {
            _dragged = dragged;
            Refresh();
        }

        // A dragged item would fit here (a gun part over the gun's slots): the tile takes the style's fit colour.
        public void SetMarked(bool marked)
        {
            if (marked == _marked)
                return;
            _marked = marked;
            Refresh();
        }

        [Button, PropertyOrder(-1)]
        public void ApplyStyle()
        {
            if (_style == null)
                return;
            _background.sprite = _style.SlotSprite;
            _background.type = StyleUtils.ImageTypeFor(_style.SlotSprite);
            _highlight.sprite = _style.HighlightSprite;
            _highlight.type = StyleUtils.ImageTypeFor(_style.HighlightSprite);
            _highlight.color = _style.HighlightColor;
            if (_rarityBorder != null)
            {
                _rarityBorder.sprite = _style.HighlightSprite;
                _rarityBorder.type = StyleUtils.ImageTypeFor(_style.HighlightSprite);
            }
            _icon.preserveAspect = true;
            _emptyIcon.preserveAspect = true;
            _emptyIcon.color = _style.EmptyIconColor;
            Font(_name, _style.NameFontSize, _style.DimTextColor);
            Font(_count, _style.CountFontSize, _style.TextColor);
            Font(_key, _style.KeyFontSize, _style.DimTextColor);
            Font(_caption, _style.CaptionFontSize, _style.DimTextColor);
            if (_searchFill != null)
                _searchFill.color = _style.HighlightColor;
            Refresh();
        }

        void Font(TMP_Text text, float size, Color color)
        {
            if (text == null)
                return;
            if (_style.Font != null)
                text.font = _style.Font;
            text.fontSize = size;
            text.color = color;
        }

        void Refresh()
        {
            _icon.enabled = _hasItem && _icon.sprite != null;
            _emptyIcon.enabled = !_hasItem && !_isHidden && _emptyIcon.sprite != null;
            if (_name != null)
                _name.enabled = _hasItem;
            if (_count != null && !_hasItem)
                _count.enabled = false;
            _highlight.enabled = _hovered;
            if (_rarityBorder != null)
                _rarityBorder.enabled = _hasItem && _rarityBorder.color.a > 0f;
            if (_style == null)
                return;
            _background.color = _marked ? _style.FitColor : _hasItem || _isHidden ? _style.SlotColor : _style.EmptySlotColor;
            Color icon = _style.IconColor;
            icon.a *= _dragged ? _style.DraggedAlpha : 1f;
            _icon.color = icon;
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
