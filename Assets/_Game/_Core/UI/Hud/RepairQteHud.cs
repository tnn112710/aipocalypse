using System;
using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.QuickTimeSystem;
using Aipocalypse.Core.TimeSystem;
using Aipocalypse.Core.WeaponSystem;
using Aipocalypse.Input;
using DG.Tweening;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Aipocalypse.Core.UI
{
    // The jam-repair QTE of the active player's held gun, from gun events only: shown on GunRepairQteStarted, shakes on a
    // Miss beat, plays the success burst or a fade on GunRepairCompleted. Hidden when the QTE ends or the gun is put away.
    public sealed class RepairQteHud : PlayerHudView
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
        ItemInstance _displayedItem;
        RepairFeedbackSettings _feedback;
        QteView _selected;
        bool _finishing;
        bool _successful;
        // Nothing shown and nothing to undo, so idle frames skip the reset.
        bool _clean;
        float _flash;
        Color _flashColor;
        Vector3 _restScale;
        Vector3 _restPosition;

        ItemInstance HeldInstance
        {
            get
            {
                HeldItem held = Player != null ? Player.HeldItem : null;
                return held != null ? held.Item : null;
            }
        }

        [Inject]
        void Construct(QuickTimeService qte, TimeService time, InputService input, ISubscriber<GunRepairQteStarted> started,
            ISubscriber<GunJamBeat> beats, ISubscriber<GunRepairCompleted> repairs)
        {
            _qte = qte;
            _time = time;
            _input = input;
            _subscriptions = DisposableBag.Create(
                started.Subscribe(OnQteStarted, e => IsMine(e.Owner)),
                beats.Subscribe(OnMissed, e => e.Kind == GunJamBeatKind.Miss && IsMine(e.Owner)),
                repairs.Subscribe(OnRepairCompleted, e => IsMine(e.Owner)));
        }

        void Awake()
        {
            _restScale = _content.transform.localScale;
            _restPosition = _content.transform.localPosition;
        }

        protected override void Start()
        {
            ApplyStyle();
            _key.SetText(_input?.GetReloadKeyLabel() ?? "R");
            ResetFeedback();
            base.Start();
        }

        public void ApplyStyle()
        {
            if (_style == null)
                return;
            _keyCap.sprite = _style.KeyCap;
            _key.font = _style.Font;
            _key.fontSize = _style.FontSize;
            _key.color = Color.black;
        }

        protected override void Bind(Player player) { }
        protected override void Unbind(Player player) => ResetFeedback();

        protected override void OnDestroy()
        {
            _subscriptions?.Dispose();
            ResetFeedback();
            base.OnDestroy();
        }

        void OnDisable() => ResetFeedback();

        bool IsMine(IEquipmentOwner owner) => Player != null && !Player.IsDead && ReferenceEquals(owner, Player);

        void OnQteStarted(GunRepairQteStarted started)
        {
            ResetFeedback();
            _clean = false;
            _displayedSession = started.Session;
            _displayedItem = started.Item;
            _feedback = started.Feedback;
            _selected = null;
            foreach (QteView view in _views)
                if (view.CanDisplay(_displayedSession) && (_selected == null || view.PresentationId == _qte.Presentation))
                    _selected = view;
            foreach (QteView view in _views)
                view.gameObject.SetActive(view == _selected);
        }

        void OnMissed(GunJamBeat beat)
        {
            if (_feedback == null || _displayedSession == null || !ReferenceEquals(beat.Item, _displayedItem))
                return;
            _feedbackTween?.Kill();
            _content.transform.localPosition = _restPosition;
            _content.transform.localScale = _restScale;
            _flash = 1f;
            _flashColor = _feedback.MissColor;
            float duration = Mathf.Max(0.05f, _feedback.MissDuration);
            _feedbackTween = DOTween.Sequence()
                .Append(_content.transform.DOPunchPosition(Vector3.right * _feedback.MissShakePixels, duration, 10, 0.7f))
                .Join(_content.transform.DOPunchScale(-_restScale * _feedback.MissSquash, duration * 0.6f, 4, 0.5f))
                .Join(DOTween.To(() => _flash, value => _flash = value, 0f, duration).SetEase(Ease.InQuad))
                .SetUpdate(true);
        }

        void OnRepairCompleted(GunRepairCompleted repair)
        {
            RepairFeedbackSettings feedback = _feedback;
            if (feedback == null || _displayedSession == null || !ReferenceEquals(_displayedItem, repair.Item))
                return;
            _feedbackTween?.Kill();
            _content.transform.localPosition = _restPosition;
            _content.transform.localScale = _restScale;
            _group.alpha = 1f;
            _finishing = true;
            _successful = repair.QteSuccess;
            _keyCap.gameObject.SetActive(!repair.QteSuccess);
            _flash = repair.QteSuccess ? 1f : 0f;
            _flashColor = feedback.SuccessColor;
            Sequence sequence = DOTween.Sequence();
            if (repair.QteSuccess)
            {
                float duration = Mathf.Max(0.05f, feedback.SuccessDuration);
                float hold = Mathf.Clamp(feedback.SuccessHold, 0f, duration * 0.65f);
                float fade = duration * 0.76f - hold;
                sequence.Append(_content.transform.DOScale(_restScale * feedback.SuccessScale, duration * 0.12f).SetEase(Ease.OutCubic))
                    .Append(_content.transform.DOScale(_restScale * 1.08f, duration * 0.12f).SetEase(Ease.OutQuad))
                    .AppendInterval(hold)
                    .Append(_group.DOFade(0f, fade))
                    .Join(_content.transform.DOScale(_restScale * 0.9f, fade).SetEase(Ease.InCubic));
                if (_burst != null)
                {
                    _burst.gameObject.SetActive(true);
                    _burst.sprite = _style.Ring;
                    _burst.color = feedback.SuccessColor;
                    _burst.transform.localScale = Vector3.one;
                    sequence.Insert(0f, _burst.transform.DOScale(feedback.BurstScale, duration * 0.65f).SetEase(Ease.OutCubic))
                        .Insert(0f, _burst.DOFade(0f, duration * 0.65f));
                }
            }
            else
                sequence.Append(_group.DOFade(0f, Mathf.Max(0.05f, feedback.NaturalFadeDuration)));
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
            _displayedItem = null;
            _feedback = null;
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
            bool allowed = _input != null && _input.GameplayEnabled && Player != null && !Player.IsDead &&
                           ReferenceEquals(_displayedItem, HeldInstance);
            if (!allowed || (!live && !_finishing))
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
            _keyCap.rectTransform.anchoredPosition = new Vector2(0f, -((RectTransform)_selected.transform).rect.height * 0.5f - 22f);
        }
    }
}
