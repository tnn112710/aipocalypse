using DG.Tweening;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.LocalizationSystem;
using UnityEngine;
using UnityEngine.Localization;
using VContainer;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // The herd's bell: ringing it calls the released herd home (HerdOrders.Call). With no pen gate (WorldMap: it hangs by
    // the home gate) it also lets the herd out while it's home. It only rings where it hangs.
    [DisallowMultipleComponent]
    public sealed class AnimalBell : MonoBehaviour, IInteractable
    {
        static readonly LocalizedString Verb = Loc.Interaction("verb.ring");
        static readonly LocalizedString Name = Loc.Interaction("target.bell");
        static readonly LocalizedString LetOutVerb = Loc.Interaction("verb.let_out");
        static readonly LocalizedString HerdName = Loc.Interaction("target.herd");

        [SerializeField, Tooltip("Swings when rung; optional")] Transform _bell;
        [SerializeField, Min(0f), Tooltip("Seconds before it can ring again")] float _cooldown = 1.5f;
        [SerializeField, Range(0f, 45f), Tooltip("Degrees it swings")] float _swingAngle = 22f;

        AnimalHerd _herd;
        AudioService _audio;
        float _readyAt;
        Tween _swing;

        [Inject]
        void Construct(AnimalHerd herd, AudioService audio)
        {
            _herd = herd;
            _audio = audio;
        }

        void OnDestroy() => _swing?.Kill();

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (_herd == null || !_herd.Enabled || Time.time < _readyAt)
                return false;
            prompt = LetsOut()
                ? new InteractionPrompt(LetOutVerb.Text(), HerdName.Text())
                : new InteractionPrompt(Verb.Text(), Name.Text());
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (_herd == null || !_herd.Enabled || Time.time < _readyAt)
                return;
            _readyAt = Time.time + _cooldown;
            _audio?.PlayAt(_herd.Settings.BellSound, transform.position);
            if (_herd.Pen.Gate != null)
                _herd.Orders.Call();
            else
                _herd.Orders.Bell();
            if (_bell == null)
                return;
            _swing?.Kill(true);
            _swing = _bell.DOPunchRotation(new Vector3(0f, 0f, _swingAngle), _cooldown, 4, 0.6f).SetLink(gameObject);
        }

        public void Setup(Transform bell) => _bell = bell;

        bool LetsOut() => _herd.Pen.Gate == null && !_herd.Orders.Released;
    }
}
