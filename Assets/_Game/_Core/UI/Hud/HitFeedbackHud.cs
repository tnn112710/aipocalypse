using System;
using Aipocalypse.Core.AudioSystem;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.PlayerSystem;
using Ami.BroAudio;
using DamageNumbersPro;
using DG.Tweening;
using MessagePipe;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Aipocalypse.Core.UI
{
    // Hits the active player lands (DamageDealt from the player): a hit marker round the crosshair, bigger and red on a
    // kill, a hit or kill sound, and a pooled damage number at the hit point. Shotgun pellets in one frame mark once.
    public sealed class HitFeedbackHud : PlayerHudView
    {
        [Header("Marker")]
        [SerializeField, Tooltip("The X round the crosshair; scaled and faded")] RectTransform _marker;
        [SerializeField] CanvasGroup _markerGroup;
        [SerializeField, Tooltip("Tinted hit / kill")] Graphic[] _markerParts = Array.Empty<Graphic>();
        [SerializeField, InlineEditor, Tooltip("Colors, timing and pop of the marker and kill numbers")] HitFeedbackStyle _style;

        [Header("Sound")]
        [SerializeField] SoundID _hitSound;
        [SerializeField] SoundID _killSound;

        [Header("Damage numbers")]
        [SerializeField, Tooltip("Damage Numbers Pro prefab with pooling on; empty = no numbers")] DamageNumber _damageNumber;

        AudioService _audio;
        IDisposable _subscription;
        Tween _tween;
        Vector3 _markerScale;
        int _markedFrame = -1;
        bool _markedKill;

        [Inject]
        void Construct(AudioService audio, ISubscriber<DamageDealt> dealt)
        {
            _audio = audio;
            _subscription = dealt.Subscribe(OnDamageDealt);
        }

        void Awake()
        {
            if (_style == null)
            {
                Debug.LogWarning($"{name}: no HitFeedbackStyle assigned; using defaults.", this);
                _style = ScriptableObject.CreateInstance<HitFeedbackStyle>();
            }
            _markerScale = _marker.localScale;
            _markerGroup.alpha = 0f;
        }

        protected override void Start()
        {
            if (_damageNumber != null)
                _damageNumber.PrewarmPool();
            base.Start();
        }

        protected override void Unbind(IPlayer player) => HideMarker();

        protected override void OnDestroy()
        {
            _subscription?.Dispose();
            _tween?.Kill();
            base.OnDestroy();
        }

        void OnDamageDealt(DamageDealt hit)
        {
            IPlayer player = Player;
            if (player == null || hit.Info.Source != player.Transform.gameObject || ReferenceEquals(hit.Target, player))
                return;

            bool kill = hit.Result.Killed;
            if (_damageNumber != null)
            {
                DamageNumber number = _damageNumber.Spawn(hit.Info.Point, hit.Result.Applied);
                if (kill)
                    number.SetColor(_style.KillNumberColor);
            }

            // One marker and sound per frame; a kill among them wins.
            if (_markedFrame == Time.frameCount && (_markedKill || !kill))
                return;
            _markedFrame = Time.frameCount;
            _markedKill = kill;
            _audio.Play(kill ? _killSound : _hitSound);
            ShowMarker(kill);
        }

        void ShowMarker(bool kill)
        {
            _tween?.Kill();
            HitFeedbackStyle style = _style;
            Color color = kill ? style.KillColor : style.HitColor;
            foreach (Graphic part in _markerParts)
                part.color = color;
            float duration = kill ? style.KillDuration : style.HitDuration;
            _markerGroup.alpha = 1f;
            _marker.localScale = _markerScale * (kill ? style.KillPop : style.HitPop);
            // Real time, so a kill's hit stop doesn't freeze the marker.
            _tween = DOTween.Sequence()
                .Append(_marker.DOScale(_markerScale, duration * style.PopShare).SetEase(Ease.OutCubic))
                .Join(_markerGroup.DOFade(0f, duration).SetEase(Ease.InQuad))
                .SetUpdate(true);
        }

        void HideMarker()
        {
            _tween?.Kill();
            _tween = null;
            if (_markerGroup != null)
                _markerGroup.alpha = 0f;
        }
    }
}
