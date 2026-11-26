using Ami.BroAudio;
using DG.Tweening;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.LocalizationSystem;
using UnityEngine;
using UnityEngine.Localization;
using VContainer;

namespace MB13.Aipocalypse.Core.ForecastSystem
{
    // The pull rope on the forecast board: "[E] Spin Forecast" while today's forecast is '?'. The handle is yanked down
    // and springs back, and the reveal fires as it bottoms out. One way to reveal: remove this component and call
    // ForecastReveal.Reveal() from another trigger to change the mechanic.
    [DisallowMultipleComponent]
    public sealed class ForecastRope : MonoBehaviour, IInteractable
    {
        static readonly LocalizedString Verb = Loc.Interaction("verb.spin");
        static readonly LocalizedString Name = Loc.Interaction("target.forecast");

        [SerializeField, Tooltip("The handle at the rope's end, its pivot where the rope ties on")] Transform _handle;
        [SerializeField, Tooltip("The rope down to the handle: a cylinder along local Y, stretched to follow it")] Transform _rope;
        [SerializeField, Tooltip("Where the rope hangs from, in the handle's parent space")] Vector3 _top;
        [SerializeField, Min(0f), Tooltip("Metres the handle is pulled down")] float _depth = 0.22f;
        [SerializeField, Min(0.01f), Tooltip("Seconds of the pull")] float _pullTime = 0.15f;
        [SerializeField, Min(0.01f), Tooltip("Seconds it springs back")] float _returnTime = 0.6f;
        [SerializeField] SoundID _pullSound;

        ForecastReveal _reveal;
        AudioService _audio;
        Vector3 _rest;
        Sequence _pull;

        [Inject]
        void Construct(ForecastReveal reveal, AudioService audio)
        {
            _reveal = reveal;
            _audio = audio;
        }

        void Awake()
        {
            if (_handle != null)
                _rest = _handle.localPosition;
        }

        void OnDestroy() => _pull?.Kill();

        bool Pulling => _pull != null && _pull.IsActive();

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (_reveal == null || _reveal.IsRevealed || Pulling)
                return false;
            prompt = new InteractionPrompt(Verb.Text(), Name.Text());
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (_reveal == null || _reveal.IsRevealed || Pulling)
                return;
            _audio?.PlayAt(_pullSound, transform.position);
            if (_handle == null)
            {
                _reveal.Reveal();
                return;
            }
            _pull = DOTween.Sequence().SetLink(gameObject)
                .Append(_handle.DOLocalMove(_rest + Vector3.down * _depth, _pullTime).SetEase(Ease.OutQuad))
                .AppendCallback(_reveal.Reveal)
                .Append(_handle.DOLocalMove(_rest, _returnTime).SetEase(Ease.OutElastic, 1.1f, 0.35f))
                .Join(_handle.DOPunchRotation(new Vector3(0f, 0f, 14f), _returnTime, 5, 0.6f))
                .OnUpdate(Stretch)
                .OnKill(() =>
                {
                    _handle.localPosition = _rest;
                    Stretch();
                });
        }

        // The rope runs from its top to the handle.
        void Stretch()
        {
            if (_rope == null || _handle == null)
                return;
            Vector3 bottom = _handle.localPosition;
            _rope.localPosition = (_top + bottom) * 0.5f;
            Vector3 scale = _rope.localScale;
            scale.y = Vector3.Distance(_top, bottom) * 0.5f;
            _rope.localScale = scale;
        }
    }
}
