using DG.Tweening;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.LocalizationSystem;
using Pathfinding;
using UnityEngine;
using UnityEngine.Localization;
using VContainer;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // The pen's gate: opening it releases the herd to the pasture (HerdOrders), closing it keeps them in. It cuts the
    // animals' navmesh while shut and won't close on an animal in the doorway. Not a barrier: raiders ignore it.
    [DisallowMultipleComponent]
    public sealed class PenGate : MonoBehaviour, IInteractable
    {
        static readonly LocalizedString OpenVerb = Loc.Interaction("verb.open");
        static readonly LocalizedString CloseVerb = Loc.Interaction("verb.close");
        static readonly LocalizedString Name = Loc.Interaction("target.pen");
        static readonly LocalizedString BlockedName = Loc.Interaction("target.pen_blocked");

        [SerializeField, Tooltip("The leaf turns round this")] Transform _hinge;
        [SerializeField, Tooltip("Cuts the navmesh while the gate is shut")] NavmeshCut _cut;
        [SerializeField, Range(30f, 170f), Tooltip("Degrees the leaf swings open")] float _openAngle = 100f;
        [SerializeField, Min(0.05f), Tooltip("Seconds a swing takes")] float _swingTime = 0.6f;

        AnimalHerd _herd;
        AudioService _audio;
        AstarPath _navigation;
        Quaternion _closedRotation;
        bool _cached;
        Tween _swing;

        public bool IsOpen { get; private set; }

        [Inject]
        void Construct(AnimalHerd herd, AudioService audio, AstarPath navigation)
        {
            _herd = herd;
            _audio = audio;
            _navigation = navigation;
        }

        void Awake() => CacheHinge();

        void OnDestroy() => _swing?.Kill();

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (_herd == null || !_herd.Enabled)
                return false;
            prompt = !IsOpen
                ? new InteractionPrompt(OpenVerb.Text(), Name.Text())
                : new InteractionPrompt(CloseVerb.Text(), (IsBlocked() ? BlockedName : Name).Text());
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (_herd == null || !_herd.Enabled)
                return;
            if (!IsOpen)
                Open(actor.transform.position);
            else
                Close();
        }

        // Swings away from `actorPosition` and releases the herd.
        public void Open(Vector3 actorPosition)
        {
            float side = Vector3.Dot(actorPosition - transform.position, transform.forward) >= 0f ? 1f : -1f;
            Set(true, side, instant: false);
            _herd.Orders.OpenGate();
        }

        // False while an animal stands in the doorway.
        public bool Close()
        {
            if (!IsOpen || IsBlocked())
                return false;
            Set(false, 0f, instant: false);
            _herd.Orders.CloseGate();
            return true;
        }

        // Open or shut at once, without a sound or new orders (loading a save).
        public void Restore(bool open) => Set(open, 1f, instant: true);

        public void Setup(Transform hinge, NavmeshCut cut)
        {
            _hinge = hinge;
            _cut = cut;
        }

        bool IsBlocked() => _herd.AnyNear(transform.position, _herd.Settings.DoorwayRadius);

        void Set(bool open, float side, bool instant)
        {
            CacheHinge();
            IsOpen = open;
            _swing?.Kill();
            if (!instant && _herd != null)
                _audio?.PlayAt(open ? _herd.Settings.GateOpenSound : _herd.Settings.GateCloseSound, transform.position + Vector3.up);
            // Opening frees the way at once; closing blocks it once the leaf is home.
            if (open || instant || _hinge == null)
                ApplyCut(!open);
            if (_hinge == null)
                return;
            Quaternion target = open ? _closedRotation * Quaternion.Euler(0f, _openAngle * side, 0f) : _closedRotation;
            if (instant)
                _hinge.localRotation = target;
            else
            {
                _swing = _hinge.DOLocalRotateQuaternion(target, _swingTime).SetEase(open ? Ease.OutCubic : Ease.InCubic).SetLink(gameObject);
                if (!open)
                    _swing.OnComplete(() => ApplyCut(true));
            }
        }

        // The cut applied now (A* would on its next update), so an animal choosing its way sees the gate as it is.
        void ApplyCut(bool blocking)
        {
            if (_cut == null || _cut.enabled == blocking)
                return;
            _cut.enabled = blocking;
            if (_navigation == null || !Application.isPlaying)
                return;
            _navigation.navmeshUpdates.ForceUpdate();
            _navigation.FlushWorkItems();
        }

        void CacheHinge()
        {
            if (_cached || _hinge == null)
                return;
            _cached = true;
            _closedRotation = _hinge.localRotation;
        }
    }
}
