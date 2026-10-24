using System;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.QuickTimeSystem;
using Aipocalypse.Core.TimeSystem;
using Aipocalypse.Input;
using DG.Tweening;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Aipocalypse.Core.UI
{
    // Shows the running QTE (whoever started it) from its own events: view by presentation, key label, shake on a miss, success
    // burst or fade at the end. Hidden while paused or in a menu; look and animations in QteStyle.
    public sealed class QteHud : MonoBehaviour
    {
        [SerializeField] QteStyle _style;
        [SerializeField] GameObject _content;
        [SerializeField] Image _keyCap;
        [SerializeField] TMP_Text _key;
        [SerializeField] QteView[] _views;
        [SerializeField] CanvasGroup _group;
        [SerializeField] Image _burst;

        QuickTimeService _qte;
        TimeService _time;
        InputService _input;
        IDisposable _subscriptions;
        Tween _feedbackTween;
        IQteSession _displayedSession;
        QteView _selected;
        bool _finishing;
        bool _successful;
        // Nothing shown and nothing to undo, so idle frames skip the reset.
        bool _clean;
        float _flash;
        Color _flashColor;
        Vector3 _restScale;
        Vector3 _restPosition;

        [Inject]
        void Construct(QuickTimeService qte, TimeService time, InputService input, ISubscriber<QteStarted> started,
            ISubscriber<QteAttempted> attempted, ISubscriber<QteEnded> ended)
        {
            _qte = qte;
            _time = time;
            _input = input;
            _subscriptions = DisposableBag.Create(
                started.Subscribe(OnStarted),
                attempted.Subscribe(OnMissed, e => e.Result == QteAttemptResult.Miss),
                ended.Subscribe(OnEnded));
        }

        void Awake()
        {
            _restScale = _content.transform.localScale;
            _restPosition = _content.transform.localPosition;
        }

        void Start()
        {
            ApplyStyle();
            ResetFeedback();
        }

        void ApplyStyle()
        {
            if (_style == null)
                return;
            _keyCap.sprite = _style.KeyCap;
            _key.font = _style.KeyFont;
            _key.fontSize = _style.KeyFontSize;
            _key.color = _style.KeyLabelColor;
            if (_burst != null)
                _burst.sprite = _style.Ring;
        }

        void OnDestroy()
        {
            _subscriptions?.Dispose();
            ResetFeedback();
        }

        void OnDisable() => ResetFeedback();

        void OnStarted(QteStarted started)
        {
            ResetFeedback();
            _clean = false;
            _displayedSession = started.Session;
            _key.SetText(_input?.GetKeyLabel(started.Button) ?? started.Button.ToString());
            _selected = null;
            foreach (QteView view in _views)
                if (view.CanDisplay(_displayedSession) && (_selected == null || view.PresentationId == started.Presentation))
                    _selected = view;
            foreach (QteView view in _views)
                view.gameObject.SetActive(view == _selected);
            if (_selected != null)
            {
                float gap = _style != null ? _style.KeyCapGap : 0f;
                _keyCap.rectTransform.anchoredPosition = new Vector2(0f, -((RectTransform)_selected.transform).rect.height * 0.5f - gap);
            }
        }

        void OnMissed(QteAttempted attempt)
        {
            if (_style == null || !ReferenceEquals(attempt.Session, _displayedSession))
                return;
            QteStyle s = _style;
            _feedbackTween?.Kill();
            _content.transform.localPosition = _restPosition;
            _content.transform.localScale = _restScale;
            _flash = 1f;
            _flashColor = s.MissColor;
            float duration = s.MissDuration;
            _feedbackTween = DOTween.Sequence()
                .Append(_content.transform.DOPunchPosition(Vector3.right * s.MissShakePixels, duration, s.MissShakeVibrato, s.MissShakeElasticity))
                .Join(_content.transform.DOPunchScale(-_restScale * s.MissSquash, duration * s.MissSquashShare, s.MissSquashVibrato, s.MissSquashElasticity))
                .Join(DOTween.To(() => _flash, value => _flash = value, 0f, duration).SetEase(Ease.InQuad))
                .SetUpdate(true);
        }

        void OnEnded(QteEnded ended)
        {
            if (_style == null || !ReferenceEquals(ended.Session, _displayedSession))
                return;
            QteStyle s = _style;
            bool success = ended.Result == QteResult.Success;
            _feedbackTween?.Kill();
            _content.transform.localPosition = _restPosition;
            _content.transform.localScale = _restScale;
            _group.alpha = 1f;
            _finishing = true;
            _successful = success;
            _keyCap.gameObject.SetActive(!success);
            _flash = success ? 1f : 0f;
            _flashColor = s.SuccessColor;
            Sequence sequence = DOTween.Sequence();
            if (success)
            {
                float duration = s.SuccessDuration;
                float pop = duration * s.SuccessPopShare;
                float burst = duration * s.BurstShare;
                float hold = Mathf.Clamp(s.SuccessHold, 0f, burst);
                float fade = Mathf.Max(0f, duration * s.SuccessFadeEndShare - hold);
                sequence.Append(_content.transform.DOScale(_restScale * s.SuccessScale, pop).SetEase(Ease.OutCubic))
                    .Append(_content.transform.DOScale(_restScale * s.SuccessSettleScale, pop).SetEase(Ease.OutQuad))
                    .AppendInterval(hold)
                    .Append(_group.DOFade(0f, fade))
                    .Join(_content.transform.DOScale(_restScale * s.SuccessEndScale, fade).SetEase(Ease.InCubic));
                if (_burst != null)
                {
                    _burst.gameObject.SetActive(true);
                    _burst.color = s.SuccessColor;
                    _burst.transform.localScale = Vector3.one;
                    sequence.Insert(0f, _burst.transform.DOScale(s.BurstScale, burst).SetEase(Ease.OutCubic))
                        .Insert(0f, _burst.DOFade(0f, burst));
                }
            }
            else
                sequence.Append(_group.DOFade(0f, s.EndFadeDuration));
            // Real time, so the success hit stop doesn't freeze the widget.
            _feedbackTween = sequence.SetUpdate(true).OnComplete(ResetFeedback);
        }

        void ResetFeedback()
        {
            if (_clean)
                return;
            _clean = true;
            _feedbackTween?.Kill();
            _feedbackTween = null;
            _finishing = false;
            _successful = false;
            _flash = 0f;
            _displayedSession = null;
            if (_content == null)
                return;
            _content.transform.localScale = _restScale;
            _content.transform.localPosition = _restPosition;
            _content.SetActive(false);
            _keyCap.gameObject.SetActive(true);
            if (_group != null)
                _group.alpha = 1f;
            if (_burst != null)
                _burst.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (_clean)
                return;
            bool live = _qte != null && ReferenceEquals(_qte.Session, _displayedSession);
            if (_input == null || !_input.GameplayEnabled || (!live && !_finishing))
            {
                ResetFeedback();
                return;
            }
            bool visible = !_time.IsPaused;
            _content.SetActive(visible);
            if (!visible || _selected == null)
                return;
            if (_finishing && _successful)
                _selected.DisplaySuccess(_displayedSession);
            else
                _selected.Display(_displayedSession);
            _selected.Flash(_flashColor, _flash);
        }
    }
}
