using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.Interaction;
using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // A farm animal: the bridge its behaviour tasks reach the herd through, its Tokens (AnimalEarnings), its animation and
    // its coin marker. Thought and advanced by AnimalManager only: no Update. The interact key collects its Tokens when it
    // holds some, else hurries it while it is called home.
    [RequireComponent(typeof(BehaviorTree), typeof(EnemyMover))]
    [DisallowMultipleComponent]
    public sealed class Animal : MonoBehaviour, IInteractable
    {
        const string HurryVerb = "Hurry";
        const string CollectVerb = "Collect";
        const string CollectTarget = "Tokens";
        const float SpeedDamping = 0.15f;
        const float MarkerSpin = 120f;
        static readonly int SpeedParameter = Animator.StringToHash("Speed");
        static readonly int EatParameter = Animator.StringToHash("Eat");
        static readonly int SleepParameter = Animator.StringToHash("Sleep");

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
        float _nextCall;

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
        public float NextThinkAt { get; set; }
        public bool IsHome => Herd != null && Herd.Pen != null && Herd.Pen.Contains(transform.position);
        public bool IsRoaming => Herd != null && Herd.Pasture != null && Herd.Pasture.Contains(transform.position);
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
                _marker.SetActive(false);
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
            Eating = false;
            Sleeping = false;
            _nextCall = Time.time + Random.Range(_definition.CallInterval.x, _definition.CallInterval.y);
            Mover.Initialize(_navigation, _definition.WalkSpeed, _definition.StopDistance);
            Tree.StartBehavior();
            Herd.Add(this);
            if (_grassBending != null && _definition.GrassBendRadius > 0f)
                _grassBending.Add(transform, _definition.GrassBendRadius);
        }

        public void Think() => Tree.Tick();

        // Runs the rest of the way home (until Calm).
        public void Hurry()
        {
            if (Hurrying || !_active)
                return;
            Hurrying = true;
            Mover.SetSpeed(_definition.HurrySpeed);
            _audio?.PlayAt(_definition.Hurried, transform.position);
        }

        public void Calm()
        {
            if (!Hurrying)
                return;
            Hurrying = false;
            Mover.SetSpeed(_definition.WalkSpeed);
        }

        // Every frame, from AnimalManager: `awakeHours` waking hours of a `dayLength`-hour waking day went by.
        public void Advance(float deltaTime, float awakeHours, float dayLength)
        {
            if (!_active)
                return;
            Earnings.Earn(awakeHours, dayLength, _definition.TokensPerDay(IsRoaming), _definition.MaxHeld);
            if (_animator != null)
                _animator.SetFloat(SpeedParameter, Mover.Velocity.magnitude, SpeedDamping, deltaTime);
            if (_marker != null)
            {
                bool show = HasTokens;
                if (_marker.activeSelf != show)
                    _marker.SetActive(show);
                if (show)
                    _marker.transform.Rotate(0f, MarkerSpin * deltaTime, 0f, Space.World);
            }
            if (_sleeping || Time.time < _nextCall)
                return;
            _nextCall = Time.time + Random.Range(_definition.CallInterval.x, _definition.CallInterval.y);
            _audio?.PlayAt(_definition.Call, transform.position);
        }

        // Puts it where a save left it.
        public void RestoreSaved(Vector3 position, float yaw, float held)
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (_active)
                Mover.Teleport(position);
            else
                transform.position = position;
            Earnings.Set(held);
        }

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!_active || !Herd.Enabled)
                return false;
            if (HasTokens)
                prompt = new InteractionPrompt(CollectVerb, CollectTarget);
            else if (CanHurry)
                prompt = new InteractionPrompt(HurryVerb, _definition.DisplayName);
            else
                return false;
            return true;
        }

        // Its Tokens first: a called animal holding some is hurried by the next press.
        public void Interact(GameObject actor)
        {
            if (!_active || !Herd.Enabled)
                return;
            if (HasTokens)
                _manager.HandOver(this, actor.transform.position, actor.GetComponent<Collider>());
            else if (CanHurry)
                Herd.Hurry(this);
        }

        bool CanHurry => Herd.Orders.Called && !Hurrying && !IsHome;

        public void Setup(AnimalDefinition definition, Animator animator, GameObject marker)
        {
            _definition = definition;
            _animator = animator;
            _marker = marker;
        }
    }
}
