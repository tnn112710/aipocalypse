using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aipocalypse.Core.UI
{
    // Reusable HUD meter: icon, filled bar, optional trail and a value label. A presenter feeds it values; its look comes from a StatBarStyle.
    public sealed class StatBar : MonoBehaviour
    {
        [SerializeField, InlineEditor] StatBarStyle _style;
        [SerializeField] Image _background;
        [SerializeField] Image _iconFrame;
        [SerializeField] Image _icon;
        [SerializeField] Image _trail;
        [SerializeField] Image _fill;
        [SerializeField] TMP_Text _label;

        float _normalized = -1f;
        int _shownValue = int.MinValue;
        bool _warning;
        float _trailHoldLeft;

        public StatBarStyle Style => _style;
        bool TrailActive => _trail != null && _style != null && _style.UseTrail;

        void Awake()
        {
            ApplyStyle();
            // Update only runs while the trail is catching up.
            enabled = false;
        }

        // Skips unchanged values, so calling it every frame costs almost nothing.
        public void SetValue(float current, float max)
        {
            float normalized = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (normalized != _normalized)
            {
                float drop = _normalized - normalized;
                _normalized = normalized;
                _fill.fillAmount = normalized;
                UpdateTrail(drop);
            }

            // Rounded up so a sliver of health never reads as 0.
            int shown = Mathf.CeilToInt(current);
            if (shown != _shownValue && _label != null)
            {
                _shownValue = shown;
                _label.SetText("{0}", shown);
            }
        }

        public void SetWarning(bool warning)
        {
            if (warning == _warning)
                return;

            _warning = warning;
            if (_style != null)
                _fill.color = warning ? _style.WarningColor : _style.FillColor;
        }

        [Button, PropertyOrder(-1)]
        public void ApplyStyle()
        {
            if (_style == null)
                return;

            Paint(_background, _style.BackgroundSprite, _style.BackgroundColor);
            Paint(_fill, _style.FillSprite, _warning ? _style.WarningColor : _style.FillColor);
            MakeHorizontalFill(_fill);

            bool hasIcon = _style.IconSprite != null;
            SetActive(_iconFrame, hasIcon);
            SetActive(_icon, hasIcon);
            Paint(_iconFrame, _style.IconFrameSprite, _style.IconFrameColor);
            Paint(_icon, _style.IconSprite, _style.IconColor);

            if (_trail != null)
            {
                _trail.gameObject.SetActive(_style.UseTrail);
                Paint(_trail, _style.FillSprite, _style.TrailColor);
                MakeHorizontalFill(_trail);
            }

            if (_label != null)
            {
                _label.gameObject.SetActive(_style.ShowLabel);
                if (_style.Font != null)
                    _label.font = _style.Font;
                _label.color = _style.LabelColor;
                _label.fontSize = _style.FontSize;
            }
        }

        void UpdateTrail(float drop)
        {
            if (!TrailActive)
                return;

            if (drop > 0f)
            {
                if (drop >= _style.TrailHoldMinDrop)
                    _trailHoldLeft = _style.TrailDelay;
                enabled = true;
            }
            else if (_trail.fillAmount < _normalized)
            {
                _trail.fillAmount = _normalized;
            }
        }

        // Hand-rolled instead of DOTween: a continuous drain (bleeding) would restart a tween every frame.
        void Update()
        {
            if (!TrailActive)
            {
                enabled = false;
                return;
            }

            if (_trailHoldLeft > 0f)
            {
                _trailHoldLeft -= Time.deltaTime;
                return;
            }

            _trail.fillAmount = Mathf.MoveTowards(_trail.fillAmount, _normalized, _style.TrailSpeed * Time.deltaTime);
            if (_trail.fillAmount <= _normalized)
                enabled = false;
        }

        static void Paint(Image image, Sprite sprite, Color color)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.color = color;
        }

        static void MakeHorizontalFill(Image image)
        {
            if (image == null)
                return;

            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        static void SetActive(Component component, bool active)
        {
            if (component != null)
                component.gameObject.SetActive(active);
        }

#if UNITY_EDITOR
        void OnValidate() => UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
                ApplyStyle();
        };
#endif
    }
}
