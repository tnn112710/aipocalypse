using System;
using MB13.Aipocalypse.Core.Events;
using DG.Tweening;
using MessagePipe;
using TMPro;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // A short line when a fence segment or gate breaks, saying where from the player's view ("The fence is down behind you").
    // The break's own sound plays at the barrier, so this is silent. A new break replaces the line.
    public sealed class BreachAlertHud : PlayerHudView
    {
        [SerializeField] BreachAlertStyle _style;
        [SerializeField] CanvasGroup _group;
        [SerializeField] TMP_Text _text;

        IDisposable _subscription;
        Sequence _show;

        [Inject]
        void Construct(ISubscriber<BarrierBroken> broken) => _subscription = broken.Subscribe(OnBroken);

        void Awake() => _group.alpha = 0f;

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _subscription?.Dispose();
            _show?.Kill();
        }

        void OnBroken(BarrierBroken broken)
        {
            if (_style == null)
                return;
            _text.text = (broken.IsGate ? _style.GateText : _style.FenceText) + Where(broken.Position);
            _text.color = _style.Color;

            _show?.Kill();
            _group.alpha = 0f;
            _text.transform.localScale = Vector3.one * _style.PunchScale;
            _show = DOTween.Sequence()
                .Append(_group.DOFade(1f, _style.FadeIn))
                .Join(_text.transform.DOScale(1f, _style.FadeIn * 2f).SetEase(Ease.OutBack))
                .AppendInterval(_style.Hold)
                .Append(_group.DOFade(0f, _style.FadeOut))
                .SetLink(gameObject);
        }

        // Ahead, behind or to a side, from the active player's view (nothing without a player).
        string Where(Vector3 position)
        {
            Transform view = Player?.View;
            if (view == null)
                return string.Empty;
            Vector3 to = Vector3.ProjectOnPlane(position - view.position, Vector3.up);
            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up);
            float angle = Vector3.SignedAngle(forward, to, Vector3.up);
            return Mathf.Abs(angle) <= 45f ? _style.AheadSuffix
                : Mathf.Abs(angle) >= 135f ? _style.BehindSuffix
                : angle < 0f ? _style.LeftSuffix
                : _style.RightSuffix;
        }
    }
}
