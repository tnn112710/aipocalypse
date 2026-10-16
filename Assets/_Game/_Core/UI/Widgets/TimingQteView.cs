using Aipocalypse.Core.QuickTimeSystem;
using UnityEngine;

namespace Aipocalypse.Core.UI
{
    public sealed class TimingQteView : QteView
    {
        [SerializeField] QteStyle _style;
        [SerializeField] bool _radial;
        [SerializeField] UnityEngine.UI.Image _track;
        [SerializeField] UnityEngine.UI.Image _fill;
        [SerializeField] UnityEngine.UI.Image _zone;
        [SerializeField] UnityEngine.UI.Image _marker;

        void Awake() => ApplyStyle();

        public void ApplyStyle()
        {
            if (_style == null)
                return;
            _track.sprite = _fill.sprite = _zone.sprite = _radial ? _style.Ring : _style.Bar;
            _track.color = _style.Track;
            _zone.color = _style.GreenZone;
            _fill.color = _style.Fill;
            _marker.color = _style.Text;
        }

        public override bool CanDisplay(IQteSession session) => session is TimingQteSession;

        public override void Display(IQteSession session)
        {
            if (session is not TimingQteSession timing)
                return;
            float progress = timing.Progress;
            _fill.fillAmount = progress;
            _fill.color = timing.AttemptAvailable ? _style.Fill : _style.Miss;
            if (_radial)
            {
                _zone.fillAmount = timing.ZoneWidth;
                _zone.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -360f * timing.ZoneStart);
                float angle = progress * Mathf.PI * 2f;
                float radius = _track.rectTransform.rect.width * 0.5f;
                _marker.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
                _marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -360f * progress);
            }
            else
            {
                RectTransform zone = _zone.rectTransform;
                zone.anchorMin = new Vector2(timing.ZoneStart, 0f);
                zone.anchorMax = new Vector2(timing.ZoneStart + timing.ZoneWidth, 1f);
                zone.offsetMin = zone.offsetMax = Vector2.zero;
                RectTransform marker = _marker.rectTransform;
                marker.anchorMin = marker.anchorMax = new Vector2(progress, 0.5f);
                marker.anchoredPosition = Vector2.zero;
            }
        }
    }
}
