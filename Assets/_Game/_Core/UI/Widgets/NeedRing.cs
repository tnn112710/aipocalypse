using DG.Tweening;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // A need's HUD gauge: an icon inside a ring that empties clockwise, coloured by how full it is (the style's bands), dim
    // while green and bright for a moment after a change. A presenter feeds it; its look comes from a NeedRingStyle. Real time throughout.
    public sealed class NeedRing : MonoBehaviour
    {
        [SerializeField, InlineEditor] NeedRingStyle _style;
        [SerializeField] CanvasGroup _group;
        [SerializeField, Tooltip("Scaled by the pulses")] RectTransform _body;
        [SerializeField] Image _track;
        [SerializeField] Image _fill;
        [SerializeField] Image _icon;

        float _normalized = -1f;
        NeedSeverity _severity = NeedSeverity.Normal;
        float _brightLeft;
        float _alpha = 1f;
        float _flicker = 1f;
        Tween _punch;

        public NeedRingStyle Style => _style;
        public NeedSeverity Severity => _severity;

        void Awake() => ApplyStyle();

        void OnDestroy() => _punch?.Kill();

        // Skips unchanged values; a rise (a refill) brightens the ring.
        public void SetValue(float normalized)
        {
            normalized = Mathf.Clamp01(normalized);
            if (normalized == _normalized)
                return;
            if (_normalized >= 0f && normalized > _normalized + 0.001f)
                Brighten();
            _normalized = normalized;
            if (_fill == null)
                return;
            _fill.fillAmount = normalized;
            if (_style != null)
                _fill.color = _style.ColorAt(normalized);
        }

        public void SetSeverity(NeedSeverity severity, bool pulse)
        {
            _severity = severity;
            if (!pulse)
                return;
            Brighten();
            if (_body != null && _style != null)
            {
                _punch?.Kill(true);
                _body.localScale = Vector3.one;
                _punch = _body.DOPunchScale(Vector3.one * (_style.PulseScale - 1f), _style.PulseTime, 1, 0f).SetUpdate(true);
            }
        }

        public void Brighten() => _brightLeft = _style != null ? _style.BrightTime : 0f;

        // Sanity's glitch: an alpha multiplier on top of everything else (1 = none). Never changes the value shown.
        public void SetFlicker(float alpha) => _flicker = Mathf.Clamp01(alpha);

        void Update()
        {
            if (_group == null || _style == null)
                return;
            float dt = Time.unscaledDeltaTime;
            _brightLeft -= dt;
            float target = _style.Dims(_normalized) && _brightLeft <= 0f ? _style.DimAlpha : 1f;
            if (_severity == NeedSeverity.Empty && _style.EmptyPulseRate > 0f)
            {
                float wave = 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * _style.EmptyPulseRate * Mathf.PI * 2f);
                target = Mathf.Lerp(_style.EmptyPulseAlpha, 1f, wave);
            }
            _alpha = Mathf.MoveTowards(_alpha, target, _style.FadeSpeed * dt);
            _group.alpha = _alpha * _flicker;
        }

        [Button("Apply style")]
        public void ApplyStyle()
        {
            if (_style == null)
                return;
            if (_icon != null)
            {
                _icon.sprite = _style.Icon;
                _icon.color = _style.IconColor;
                _icon.preserveAspect = true;
            }
            if (_track != null)
            {
                _track.sprite = _style.TrackSprite != null ? _style.TrackSprite : _style.RingSprite;
                _track.color = _style.TrackColor;
            }
            if (_fill != null)
            {
                _fill.sprite = _style.RingSprite;
                _fill.type = Image.Type.Filled;
                _fill.fillMethod = Image.FillMethod.Radial360;
                _fill.fillOrigin = (int)Image.Origin360.Top;
                _fill.fillClockwise = true;
                _fill.color = _style.ColorAt(_normalized >= 0f ? _normalized : 1f);
            }
            _alpha = _style.DimAlpha;
            if (_group != null)
                _group.alpha = _alpha * _flicker;
        }
    }
}
