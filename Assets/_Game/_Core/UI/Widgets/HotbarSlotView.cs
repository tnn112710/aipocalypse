using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aipocalypse.Core.UI
{
    // One hotbar slot: item icon on a tile, a selection frame, an ammo count in the corner, and the key cap below.
    // Knows nothing about items or input.
    public sealed class HotbarSlotView : MonoBehaviour
    {
        [SerializeField, InlineEditor] HotbarSlotStyle _style;
        [SerializeField, Tooltip("Scaled while selected")] RectTransform _tile;
        [SerializeField] Image _background;
        [SerializeField] Image _icon;
        [SerializeField] Image _frame;
        [SerializeField] Image _keyCap;
        [SerializeField] TMP_Text _keyLabel;
        [SerializeField, Tooltip("Magazine / reserve, bottom-right of the tile")] TMP_Text _ammoLabel;

        Sprite _iconSprite;
        bool _selected;
        // Last ammo shown, so a refresh of every slot rebuilds only the labels that changed. Rounds -1 = hidden.
        int _shownRounds = -1;
        int _shownReserve = -1;
        bool _shownInfinite;
        bool _shownWarning;

        public HotbarSlotStyle Style => _style;

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
            _background.type = TypeFor(_style.BackgroundSprite);
            _frame.sprite = _style.FrameSprite;
            _frame.type = TypeFor(_style.FrameSprite);
            _frame.color = _style.FrameColor;
            _icon.color = _style.IconColor;
            _icon.preserveAspect = true;

            _keyCap.sprite = _style.KeySprite;
            _keyCap.type = TypeFor(_style.KeySprite);
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
            _frame.enabled = _selected;
            _keyCap.color = _selected ? _style.SelectedKeyColor : _style.KeyColor;
            _tile.localScale = Vector3.one * (_selected ? _style.SelectedScale : 1f);
        }

        static Image.Type TypeFor(Sprite sprite) =>
            sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;

#if UNITY_EDITOR
        void OnValidate() => UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
                ApplyStyle();
        };
#endif
    }
}
