using DG.Tweening;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.LocalizationSystem;
using UnityEngine;
using UnityEngine.Localization;
using VContainer;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // The bell by the pen: ringing it calls the released herd home (HerdOrders.Call). It only rings where it hangs.
    [DisallowMultipleComponent]
    public sealed class AnimalBell : MonoBehaviour, IInteractable
    {
        static readonly LocalizedString Verb = Loc.Interaction("verb.ring");
        static readonly LocalizedString Name = Loc.Interaction("target.bell");

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
            prompt = new InteractionPrompt(Verb.Text(), Name.Text());
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (_herd == null || !_herd.Enabled || Time.time < _readyAt)
                return;
            _readyAt = Time.time + _cooldown;
            _audio?.PlayAt(_herd.Settings.BellSound, transform.position);
            _herd.Orders.Call();
            if (_bell == null)
                return;
            _swing?.Kill(true);
            _swing = _bell.DOPunchRotation(new Vector3(0f, 0f, _swingAngle), _cooldown, 4, 0.6f).SetLink(gameObject);
        }

        public void Setup(Transform bell) => _bell = bell;
    }
}
