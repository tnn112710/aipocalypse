using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.LocalizationSystem;
using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;
using UnityEngine.Localization;
using VContainer;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // A farm animal: the bridge its behaviour tasks reach the herd through, its Tokens (AnimalEarnings), its own order (sent
    // home), what startled it, its own pace and patience (rolled when it wakes), its animation and its coin marker. Thought and advanced by AnimalManager only: no Update.
    // The interact key sends it home or lets it out again (after the bell: speeds it up); the Collect key takes its Tokens.
    [RequireComponent(typeof(BehaviorTree), typeof(EnemyMover))]
    [DisallowMultipleComponent]
    public sealed class Animal : MonoBehaviour, IInteractable, ICollectInteractable
    {
        static readonly LocalizedString SendHomeVerb = Loc.Interaction("verb.send_home");
        static readonly LocalizedString LetOutVerb = Loc.Interaction("verb.let_out");
        static readonly LocalizedString HurryVerb = Loc.Interaction("verb.hurry");
        static readonly LocalizedString CollectVerb = Loc.Interaction("verb.collect");
        static readonly LocalizedString CollectTarget = Loc.Items("item.tokens");
        const float SpeedDamping = 0.15f;
        const float MarkerSpin = 120f;
        const float MarkerDropSpeed = 1.5f;
        static readonly int SpeedParameter = Animator.StringToHash("Speed");
        static readonly int EatParameter = Animator.StringToHash("Eat");
        static readonly int SleepParameter = Animator.StringToHash("Sleep");
        static readonly int IdleParameter = Animator.StringToHash("Idle");

        [SerializeField] AnimalDefinition _definition;
        [SerializeField, Tooltip("The Model's Animator (parameters Speed, Eat, Sleep)")] Animator _animator;
        [SerializeField, Tooltip("Shown while it holds Tokens to collect")] GameObject _marker;

        AstarPath _navigation;
        AudioService _audio;
        AnimalManager _manager;
        GrassBendField _grassBending;
        bool _started;
        bool _active;
        bool _eating;
        bool _sleeping;
        int _fidget;
        float _lastYaw;
        bool _wasCalled;
        float _nextCall;
        float _answerAt = float.PositiveInfinity;
        float _markerHeight;
        float _markerDrop;
        int _ownVersion;

        public AnimalDefinition Definition => _definition;
        public AnimalHerd Herd { get; private set; }
        public EnemyMover Mover { get; private set; }
        public BehaviorTree Tree { get; private set; }
        // Its body collider: what the player looks at, and what a burst of Tokens flies through.
        public Collider Body { get; private set; }
        public AnimalEarnings Earnings { get; } = new();
        // Hand-off between its tasks: where it walks next.
        public Vector3 Destination { get; set; }
        public bool Hurrying { get; private set; }
        // This animal's share of its kind: speeds times Pace, meals, idles and calls times Patience.
        public float Pace { get; private set; } = 1f;
        public float Patience { get; private set; } = 1f;
        public float WalkSpeed => _definition.WalkSpeed * Pace;
        public float HurrySpeed => _definition.HurrySpeed * Pace;
        public float FleeSpeed => _definition.FleeSpeed * Pace;
        public float NextThinkAt { get; set; }
        // Its next walk is at hurry pace: a chicken's dart, or catching up with its flock (ChooseGrazeSpot sets it, WalkToGrazeSpot runs it).
        public bool Darting { get; set; }
        // Told by the player to go into the pen and stay there, whatever the herd does, until let out (or the bell rings).
        public bool SentHome { get; private set; }
        // Something it moves away from (Startle): where from, how far, running or stepping.
        public bool Startled { get; private set; }
        public Vector3 StartledFrom { get; private set; }
        public float StartleDistance { get; private set; }
        public bool StartleRuns { get; private set; }
        // Changes whenever the herd's orders, its own, or dusk change, so an animal busy eating looks again at once.
        public int OrdersVersion => Herd.Orders.Version + _ownVersion + (Herd.Dusk ? 1 << 20 : 0);
        public bool IsHome => Herd != null && Herd.Pen != null && Herd.Pen.Contains(transform.position);
        public bool IsRoaming => Herd != null && Herd.Pasture != null && Herd.Pasture.Contains(transform.position);
        // On its way into the pen: the bell rang, or the player sent it.
        public bool GoingHome => (Herd.Orders.Called || SentHome) && !IsHome;
        public bool HasTokens => _definition != null && Earnings.Whole >= _definition.CollectAt;
        // Where its Tokens leave it: the coin over its back.
        public Vector3 TokenPoint => _marker != null ? _marker.transform.position : transform.position + Vector3.up * 1.5f;

        public bool Eating
        {
            get => _eating;
            set
            {
                if (_eating == value)
                    return;
                _eating = value;
                if (_animator != null)
                    _animator.SetBool(EatParameter, value);
            }
        }

        // Which spare idle clip it plays while standing about (0: none).
        public int Fidget
        {
            get => _fidget;
            set
            {
                if (_fidget == value)
                    return;
                _fidget = value;
                if (_animator != null)
                    _animator.SetInteger(IdleParameter, value);
            }
        }

        public bool Sleeping
        {
            get => _sleeping;
            set
            {
                if (_sleeping == value)
                    return;
                _sleeping = value;
                if (_animator != null)
                    _animator.SetBool(SleepParameter, value);
            }
        }

        [Inject]
        void Construct(AnimalHerd herd, AnimalManager manager, AstarPath navigation, AudioService audio, IObjectResolver resolver)
        {
            Herd = herd;
            _manager = manager;
            _navigation = navigation;
            _audio = audio;
            resolver.TryResolve(out _grassBending);
            if (_started && isActiveAndEnabled)
                Activate();
        }

        // First activation waits for Start, so the FollowerEntity and tree have woken up.
        void Start()
        {
            Mover = GetComponent<EnemyMover>();
            Tree = GetComponent<BehaviorTree>();
            Body = GetComponent<Collider>();
            _started = true;
            if (_marker != null)
            {
                _markerHeight = _marker.transform.localPosition.y;
                _marker.SetActive(false);
            }
            if (_definition == null)
                Debug.LogError($"{name}: no AnimalDefinition assigned.", this);
            else if (Herd != null)
                Activate();
            else
                Debug.LogError($"{name}: not injected; save the scene so its AnimalInstaller lists it.", this);
        }

        void OnEnable()
        {
            if (_started && Herd != null && _definition != null)
                Activate();
        }

        void OnDisable()
        {
            if (!_active)
                return;
            _active = false;
            Herd.Remove(this);
            if (_grassBending != null)
                _grassBending.Remove(transform);
            Tree.StopBehavior(false);
            Mover.Halt();
        }

        void Activate()
        {
            if (_active)
                return;
            _active = true;
            Hurrying = false;
            Startled = false;
            Eating = false;
            Sleeping = false;
            Fidget = 0;
            float pace = Herd.Settings.PaceVariation, time = Herd.Settings.TimeVariation;
            Pace = Random.Range(1f - pace, 1f + pace);
            Patience = Random.Range(1f - time, 1f + time);
            // Its legs keep up with its own pace and its size (a smaller one steps quicker); Speed is fed per kind.
            if (_animator != null)
                _animator.speed = Pace / Mathf.Max(0.1f, transform.lossyScale.y);
            _lastYaw = transform.eulerAngles.y;
            _wasCalled = Herd.Orders.Called;
            _nextCall = Time.time + NextCallDelay();
            Mover.Initialize(_navigation, WalkSpeed, _definition.StopDistance, holdGround: true, faceTurnSpeed: Herd.Settings.FaceTurnSpeed);
            Tree.StartBehavior();
            Herd.Add(this);
            if (_grassBending != null && _definition.GrassBendRadius > 0f)
                _grassBending.Add(transform, _definition.GrassBendRadius);
        }

        public void Think() => Tree.Tick();

        // Trots the rest of the way home (until Calm).
        public void Hurry()
        {
            if (Hurrying || !_active)
                return;
            Hurrying = true;
            Mover.SetSpeed(HurrySpeed);
            _audio?.PlayAt(_definition.Hurried, transform.position);
        }

        public void Calm()
        {
            if (!Hurrying)
                return;
            Hurrying = false;
            Mover.SetSpeed(WalkSpeed);
        }

        // Back to its own walking pace after a dart or a run (a hurried one keeps trotting).
        public void ResumePace() => Mover.SetSpeed(Hurrying ? HurrySpeed : WalkSpeed);

        public void SetSentHome(bool sent)
        {
            if (SentHome == sent)
                return;
            SentHome = sent;
            _ownVersion++;
        }

        // Moves `distance` away from `from`: running (a gunshot, with its startled call) or stepping aside (the player).
        // Ignored while it's already moving away from something, unless a gunshot turns a step aside into a run.
        public void Startle(Vector3 from, float distance, bool run)
        {
            if (!_active || (Startled && (StartleRuns || !run)))
                return;
            Startled = true;
            StartledFrom = from;
            StartleDistance = distance;
            StartleRuns = run;
            if (run)
                _audio?.PlayAt(_definition.Startled, transform.position);
        }

        public void Settle() => Startled = false;

        // Every frame, from AnimalManager: `awakeHours` waking hours of a `dayLength`-hour waking day went by.
        public void Advance(float deltaTime, float awakeHours, float dayLength)
        {
            if (!_active)
                return;
            Earnings.Earn(awakeHours, dayLength, _definition.TokensPerDay(IsRoaming), _definition.MaxHeld);
            UpdateAnimator(deltaTime);
            UpdateMarker(deltaTime);

            // The bell: each answers with its call, a moment apart; a sent animal's own order gives way to it.
            bool called = Herd.Orders.Called;
            if (called && !_wasCalled)
            {
                Vector2 delay = Herd.Settings.AnswerDelay;
                _answerAt = Time.time + Random.Range(delay.x, delay.y);
                SetSentHome(false);
            }
            _wasCalled = called;
            if (Time.time >= _answerAt)
            {
                _answerAt = float.PositiveInfinity;
                PlayCall();
                return;
            }

            if (_sleeping || Time.time < _nextCall)
                return;
            PlayCall();
        }

        void PlayCall()
        {
            _nextCall = Time.time + NextCallDelay();
            _audio?.PlayAt(_definition.Call, transform.position);
        }

        float NextCallDelay() => Random.Range(_definition.CallInterval.x, _definition.CallInterval.y) * Patience;

        // Speed in its kind's terms (the controller's thresholds are the definition's speeds); turning on the spot counts as a
        // slow walk, so its legs step round instead of the body skating.
        void UpdateAnimator(float deltaTime)
        {
            float yaw = transform.eulerAngles.y;
            float turn = deltaTime > 0f ? Mathf.Abs(Mathf.DeltaAngle(_lastYaw, yaw)) * Mathf.Deg2Rad / deltaTime : 0f;
            _lastYaw = yaw;
            if (_animator == null)
                return;
            Vector3 velocity = Mover.Velocity;
            velocity.y = 0f;
            float stepping = _sleeping ? 0f : Mathf.Min(turn * Herd.Settings.TurnStep, 1f) * _definition.WalkSpeed;
            float speed = Mathf.Max(velocity.magnitude / Pace, stepping);
            _animator.SetFloat(SpeedParameter, speed, SpeedDamping, deltaTime);
        }

        // Shown while it holds Tokens, spinning; lower while it lies asleep.
        void UpdateMarker(float deltaTime)
        {
            if (_marker == null)
                return;
            bool show = HasTokens;
            if (_marker.activeSelf != show)
                _marker.SetActive(show);
            if (!show)
                return;
            Transform marker = _marker.transform;
            marker.Rotate(0f, MarkerSpin * deltaTime, 0f, Space.World);
            _markerDrop = Mathf.MoveTowards(_markerDrop, _sleeping ? _definition.MarkerDropAsleep : 0f, MarkerDropSpeed * deltaTime);
            Vector3 local = marker.localPosition;
            local.y = _markerHeight - _markerDrop;
            marker.localPosition = local;
        }

        // Puts it where a save left it.
        public void RestoreSaved(Vector3 position, float yaw, float held, bool sentHome)
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (_active)
                Mover.Teleport(position);
            else
                transform.position = position;
            Earnings.Set(held);
            SetSentHome(sentHome);
        }

        bool CanSpeedUp => Herd.Orders.Called && !Hurrying && !IsHome;
        // Not after the bell; anywhere while the herd is out, or outside the pen while it's shut in.
        bool CanSendHome => !Herd.Orders.Called && !SentHome && (Herd.Orders.Released || !IsHome);
        bool CanLetOut => !Herd.Orders.Called && SentHome;

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!_active || !Herd.Enabled)
                return false;
            if (CanSpeedUp)
                prompt = new InteractionPrompt(HurryVerb.Text(), _definition.DisplayName.Text());
            else if (CanLetOut)
                prompt = new InteractionPrompt(LetOutVerb.Text(), _definition.DisplayName.Text());
            else if (CanSendHome)
                prompt = new InteractionPrompt(SendHomeVerb.Text(), _definition.DisplayName.Text());
            else
                return false;
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (!_active || !Herd.Enabled)
                return;
            if (CanSpeedUp)
                Hurry();
            else if (CanLetOut)
                SetSentHome(false);
            else if (CanSendHome)
                SetSentHome(true);
        }

        public bool TryGetCollectPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!_active || !Herd.Enabled || !HasTokens)
                return false;
            prompt = new InteractionPrompt(CollectVerb.Text(), CollectTarget.Text());
            return true;
        }

        public void Collect(GameObject actor)
        {
            if (_active && Herd.Enabled && HasTokens)
                _manager.HandOver(this, actor.transform.position, actor.GetComponent<Collider>());
        }

        public void Setup(AnimalDefinition definition, Animator animator, GameObject marker)
        {
            _definition = definition;
            _animator = animator;
            _marker = marker;
        }
    }
}
