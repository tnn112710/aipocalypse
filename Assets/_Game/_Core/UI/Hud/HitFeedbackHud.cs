using System;
using Aipocalypse.Core.AudioSystem;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.PlayerSystem;
using Ami.BroAudio;
using DamageNumbersPro;
using DG.Tweening;
using MessagePipe;
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
        [SerializeField] Color _hitColor = Color.white;
        [SerializeField] Color _killColor = new(1f, 0.25f, 0.2f, 1f);
        [SerializeField, Min(0.01f), Tooltip("Seconds to fade (real time)")] float _hitDuration = 0.18f;
        [SerializeField, Min(0.01f)] float _killDuration = 0.4f;
        [SerializeField, Min(1f), Tooltip("Pop size at the start of a hit marker")] float _hitPop = 1.3f;
        [SerializeField, Min(1f)] float _killPop = 1.8f;
        [SerializeField, Range(0.05f, 1f), Tooltip("Share of the fade the marker takes to shrink back from its pop")] float _popShare = 0.4f;

        [Header("Sound")]
        [SerializeField] SoundID _hitSound;
        [SerializeField] SoundID _killSound;

        [Header("Damage numbers")]
        [SerializeField, Tooltip("Damage Numbers Pro prefab with pooling on; empty = no numbers")] DamageNumber _damageNumber;
        [SerializeField] Color _killNumberColor = new(1f, 0.3f, 0.25f, 1f);

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
            _markerScale = _marker.localScale;
            _markerGroup.alpha = 0f;
        }

        protected override void Start()
        {
            if (_damageNumber != null)
                _damageNumber.PrewarmPool();
            base.Start();
        }

        protected override void Unbind(Player player) => HideMarker();

        protected override void OnDestroy()
        {
            _subscription?.Dispose();
            _tween?.Kill();
            base.OnDestroy();
        }

        void OnDamageDealt(DamageDealt hit)
        {
            Player player = Player;
            if (player == null || hit.Info.Source != player.gameObject || ReferenceEquals(hit.Target, player))
                return;

            bool kill = hit.Result.Killed;
            if (_damageNumber != null)
            {
                DamageNumber number = _damageNumber.Spawn(hit.Info.Point, hit.Result.Applied);
                if (kill)
                    number.SetColor(_killNumberColor);
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
            Color color = kill ? _killColor : _hitColor;
            foreach (Graphic part in _markerParts)
                part.color = color;
            float duration = kill ? _killDuration : _hitDuration;
            _markerGroup.alpha = 1f;
            _marker.localScale = _markerScale * (kill ? _killPop : _hitPop);
            // Real time, so a kill's hit stop doesn't freeze the marker.
            _tween = DOTween.Sequence()
                .Append(_marker.DOScale(_markerScale, duration * _popShare).SetEase(Ease.OutCubic))
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
