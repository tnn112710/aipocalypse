using MB13.Aipocalypse.Core.QuickTimeSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    public sealed class TimingQteView : QteView
    {
        [SerializeField] QteStyle _style;
        [SerializeField] bool _radial;
        [SerializeField] UnityEngine.UI.Image _track;
        [SerializeField] UnityEngine.UI.Image _fill;
        [SerializeField] UnityEngine.UI.Image _zone;
        [SerializeField] UnityEngine.UI.Image _marker;

        void Awake()
        {
            if (_style != null)
                _track.sprite = _fill.sprite = _zone.sprite = _radial ? _style.Ring : _style.Bar;
        }

        public override bool CanDisplay(IQteSession session) => session is TimingQteSession;

        public override void DisplaySuccess(IQteSession session)
        {
            Display(session);
            _fill.fillAmount = 1f;
            _marker.enabled = false;
        }

        public override void Flash(Color color, float amount)
        {
            if (amount <= 0f)
                return;
            _fill.color = Color.Lerp(_fill.color, color, amount);
            _zone.color = Color.Lerp(_zone.color, color, amount);
            _marker.color = Color.Lerp(_marker.color, color, amount);
            float trackStrength = _style != null ? _style.TrackFlashStrength : 1f;
            _track.color = Color.Lerp(_track.color, color, amount * trackStrength);
        }

        // Every frame: each colour is set once to its final base value (Graphic skips unchanged colours), and Flash tints on top.
        public override void Display(IQteSession session)
        {
            if (session is not TimingQteSession timing)
                return;
            if (_style != null)
            {
                _track.color = _style.Track;
                _zone.color = _style.GreenZone;
                _marker.color = _style.Marker;
                _fill.color = timing.AttemptAvailable ? _style.Fill : _style.NoAttempt;
            }
            _marker.enabled = true;
            float progress = timing.Progress;
            _fill.fillAmount = progress;
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
