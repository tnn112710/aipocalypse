using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Interaction;
using DG.Tweening;
using MB13.Aipocalypse.Core.LocalizationSystem;
using UnityEngine;
using UnityEngine.Localization;
using VContainer;

namespace MB13.Aipocalypse.Core.DefenseSystem
{
    // A gate: a fence segment (BarrierSegment beside it, as wide as one panel slot or two for a double gate, so it can replace
    // any segments) whose leaves swing open and shut on the interact key, away from whoever opens it. It blocks only while closed, and won't close
    // on someone standing in the doorway. A broken gate is repaired like any segment and stands again closed.
    [RequireComponent(typeof(BarrierSegment))]
    [DisallowMultipleComponent]
    public sealed class FenceGate : MonoBehaviour
    {
        static readonly LocalizedString OpenVerb = Loc.Interaction("verb.open");
        static readonly LocalizedString CloseVerb = Loc.Interaction("verb.close");
        static readonly LocalizedString BlockedSuffix = Loc.Interaction("suffix.blocked");
        static readonly LocalizedString FallbackName = Loc.Interaction("target.gate");

        [SerializeField, Range(30f, 170f), Tooltip("Degrees the leaves swing open")] float _openAngle = 100f;
        [SerializeField, Min(0.05f), Tooltip("Seconds a swing takes")] float _swingTime = 0.6f;
        [SerializeField] bool _startsOpen;

        BarrierSegment _segment;
        AudioService _audio;
        Transform[] _hinges;
        Quaternion[] _closedRotations;
        Tween[] _swings;
        BarrierDefinition _namedFor;
        string _name;
        string _blockedName;
        Locale _nameLocale;

        public bool IsOpen { get; private set; }

        [Inject]
        void Construct(AudioService audio) => _audio = audio;

        void Awake()
        {
            _segment = GetComponent<BarrierSegment>();
            CacheHinge();
            if (_startsOpen)
                SetOpen(true, 1f, instant: true);
        }

        void OnDestroy() => KillSwings();

        public bool TryGetPrompt(out InteractionPrompt prompt)
        {
            if (_name == null || _namedFor != _segment.Definition || _nameLocale != Loc.Current)
            {
                _namedFor = _segment.Definition;
                _nameLocale = Loc.Current;
                _name = (_namedFor != null ? _namedFor.DisplayName : FallbackName).Text();
                _blockedName = _name + BlockedSuffix.Text();
            }
            if (!IsOpen)
            {
                prompt = new InteractionPrompt(OpenVerb.Text(), _name);
                return true;
            }
            // Checked only while open: one overlap query a frame while the player looks at an open gate.
            prompt = new InteractionPrompt(CloseVerb.Text(), _segment.IsOccupied() ? _blockedName : _name);
            return true;
        }

        // Opens away from `actorPosition`, or closes unless someone stands in the doorway.
        public void Toggle(Vector3 actorPosition)
        {
            if (!IsOpen)
            {
                // A positive turn round a left hinge's Y swings its leaf (along local X) towards -Z: away from someone in front.
                float side = Vector3.Dot(actorPosition - transform.position, transform.forward) >= 0f ? 1f : -1f;
                SetOpen(true, side);
            }
            else if (!_segment.IsOccupied())
                SetOpen(false, 0f);
        }

        // Shut at once, without a sound (it stands again after a repair, or was restored).
        public void ResetClosed()
        {
            CacheHinge();
            KillSwings();
            IsOpen = false;
            for (int i = 0; i < _hinges.Length; i++)
                if (_hinges[i] != null)
                    _hinges[i].localRotation = _closedRotations[i];
        }

        // Open at once, without a sound (loading a save).
        public void OpenInstantly() => SetOpen(true, 1f, instant: true);

        void SetOpen(bool open, float side, bool instant = false)
        {
            CacheHinge();
            IsOpen = open;
            BarrierDefinition definition = _segment.Definition;
            if (!instant && definition != null)
                _audio?.PlayAt(open ? definition.OpenSound : definition.CloseSound, transform.position + Vector3.up);

            // Opening frees the way at once; closing blocks it once the leaf is home.
            if (open)
                _segment.Refresh();
            KillSwings();
            if (_hinges.Length == 0)
            {
                _segment.Refresh();
                return;
            }
            Tween last = null;
            for (int i = 0; i < _hinges.Length; i++)
            {
                Transform hinge = _hinges[i];
                if (hinge == null)
                    continue;
                // A right hinge's leaf runs along -X, so the same swing direction is the opposite turn.
                float mirror = hinge.localPosition.x <= 0f ? 1f : -1f;
                Quaternion target = open ? _closedRotations[i] * Quaternion.Euler(0f, _openAngle * side * mirror, 0f) : _closedRotations[i];
                if (instant)
                    hinge.localRotation = target;
                else
                    last = _swings[i] = hinge.DOLocalRotateQuaternion(target, _swingTime).SetEase(open ? Ease.OutCubic : Ease.InCubic).SetLink(gameObject);
            }
            if (instant)
                _segment.Refresh(instant: true);
            else if (!open && last != null)
                last.OnComplete(() => _segment.Refresh());
        }

        void KillSwings()
        {
            if (_swings == null)
                return;
            foreach (Tween swing in _swings)
                swing?.Kill();
        }

        void CacheHinge()
        {
            Transform[] hinges = _segment != null && _segment.Look != null ? _segment.Look.Hinges : System.Array.Empty<Transform>();
            if (ReferenceEquals(hinges, _hinges))
                return;
            KillSwings();
            _hinges = hinges;
            _closedRotations = new Quaternion[hinges.Length];
            _swings = new Tween[hinges.Length];
            for (int i = 0; i < hinges.Length; i++)
                if (hinges[i] != null)
                    _closedRotations[i] = hinges[i].localRotation;
        }
    }
}
