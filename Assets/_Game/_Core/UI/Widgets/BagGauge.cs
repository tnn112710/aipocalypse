using MB13.Aipocalypse.Core.PlayerSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // The carried-weight gauge: a bag that fills from the bottom (carried / Max) in the load's colour, a notch at Comfortable
    // and "kg / max" under it; dim while light. A presenter feeds it; its look comes from a BagGaugeStyle. Real time.
    public sealed class BagGauge : MonoBehaviour
    {
        [SerializeField, InlineEditor] BagGaugeStyle _style;
        [SerializeField] CanvasGroup _group;
        [SerializeField, Tooltip("The bag, dim: the empty shape")] Image _empty;
        [SerializeField, Tooltip("The bag again, Filled vertically from the bottom")] Image _fill;
        [SerializeField, Tooltip("A thin line across the bag; its anchors follow the Comfortable share")] RectTransform _notch;
        [SerializeField] TMP_Text _label;

        float _fillShown = -1f;
        float _notchShown = -1f;
        LoadState _state = (LoadState)(-1);
        float _alpha = 1f;

        public BagGaugeStyle Style => _style;

        void Awake() => ApplyStyle();

        // Skips unchanged parts; the label is formatted by the caller only when the numbers change.
        public void SetLoad(float fill, float notch, LoadState state)
        {
            fill = Mathf.Clamp01(fill);
            if (fill != _fillShown && _fill != null)
            {
                _fillShown = fill;
                _fill.fillAmount = fill;
            }
            notch = Mathf.Clamp01(notch);
            if (notch != _notchShown && _notch != null)
            {
                _notchShown = notch;
                _notch.anchorMin = new Vector2(_notch.anchorMin.x, notch);
                _notch.anchorMax = new Vector2(_notch.anchorMax.x, notch);
                _notch.anchoredPosition = new Vector2(_notch.anchoredPosition.x, 0f);
            }
            if (state == _state || _style == null)
                return;
            _state = state;
            Color color = _style.ColorFor(state);
            if (_fill != null)
                _fill.color = color;
            if (_label != null)
                _label.color = color;
        }

        public TMP_Text Label => _label;

        void Update()
        {
            if (_group == null || _style == null)
                return;
            float target = _state == LoadState.Light ? _style.DimAlpha : 1f;
            _alpha = Mathf.MoveTowards(_alpha, target, _style.FadeSpeed * Time.unscaledDeltaTime);
            _group.alpha = _alpha;
        }

        [Button("Apply style")]
        public void ApplyStyle()
        {
            if (_style == null)
                return;
            if (_empty != null)
            {
                _empty.sprite = _style.Icon;
                _empty.color = _style.EmptyColor;
                _empty.preserveAspect = true;
            }
            if (_fill != null)
            {
                _fill.sprite = _style.Icon;
                _fill.preserveAspect = true;
                _fill.type = Image.Type.Filled;
                _fill.fillMethod = Image.FillMethod.Vertical;
                _fill.fillOrigin = (int)Image.OriginVertical.Bottom;
                _fill.color = _style.ColorFor(_state >= 0 ? _state : LoadState.Light);
            }
            if (_notch != null && _notch.TryGetComponent(out Image notch))
                notch.color = _style.NotchColor;
            if (_label != null)
            {
                if (_style.Font != null)
                    _label.font = _style.Font;
                _label.fontSize = _style.FontSize;
            }
        }
    }
}
