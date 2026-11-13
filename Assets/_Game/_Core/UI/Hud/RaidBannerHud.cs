using System;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Events;
using Ami.BroAudio;
using DG.Tweening;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // A banner across the screen when a raid starts, when every wave is killed, and when dawn ends it first; each with its
    // sound. A defeat shows nothing (the death screen covers it), nor does a raid stopped from the console.
    public sealed class RaidBannerHud : MonoBehaviour
    {
        [SerializeField] RaidBannerStyle _style;
        [SerializeField] CanvasGroup _group;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _subtitle;

        AudioService _audio;
        IDisposable _subscriptions;
        Sequence _show;

        [Inject]
        void Construct(AudioService audio, ISubscriber<RaidStarted> started, ISubscriber<RaidEnded> ended)
        {
            _audio = audio;
            _subscriptions = DisposableBag.Create(started.Subscribe(OnStarted), ended.Subscribe(OnEnded));
        }

        void Awake() => _group.alpha = 0f;

        void OnStarted(RaidStarted raid) =>
            Show(_style.StartTitle, _style.StartSubtitle, raid.Night, _style.StartColor, _style.StartSound);

        void OnEnded(RaidEnded raid)
        {
            if (raid.Reason == RaidEndReason.Cleared)
                Show(_style.VictoryTitle, _style.VictorySubtitle, raid.Night, _style.VictoryColor, _style.VictorySound);
            else if (raid.Reason == RaidEndReason.Dawn)
                Show(_style.DawnTitle, _style.DawnSubtitle, raid.Night, _style.DawnColor, _style.DawnSound);
        }

        void Show(LocalizedString title, LocalizedString subtitle, int night, Color color, SoundID sound)
        {
            _title.text = title.Text();
            _title.color = color;
            _subtitle.text = subtitle.Format(night);
            _audio.Play(sound);

            _show?.Kill();
            _group.alpha = 0f;
            _title.transform.localScale = Vector3.one * _style.PunchScale;
            _show = DOTween.Sequence()
                .Append(_group.DOFade(1f, _style.FadeIn))
                .Join(_title.transform.DOScale(1f, _style.FadeIn * 1.5f).SetEase(Ease.OutBack))
                .AppendInterval(_style.Hold)
                .Append(_group.DOFade(0f, _style.FadeOut))
                .SetLink(gameObject);
        }

        void OnDestroy()
        {
            _subscriptions?.Dispose();
            _show?.Kill();
        }
    }
}
