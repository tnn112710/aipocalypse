using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.LocalizationSystem;
using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // A service robot at home: the bridge its behaviour tasks reach the roster through, what it was asked to do (greet,
    // answer a whistle, step aside), what upset it (a hit, a gun pointed at it), its face, body animation and head.
    // E opens its service (for now: it greets); the whistle calls it; a hit makes it flinch but never hurts it.
    // Thought and advanced by RobotManager only: no Update.
    [RequireComponent(typeof(BehaviorTree), typeof(EnemyMover))]
    [DisallowMultipleComponent]
    public sealed class Robot : MonoBehaviour, IInteractable, IWhistleTarget, IDamageable
    {
        static readonly int SpeedParameter = Animator.StringToHash("Speed");
        static readonly int TurnParameter = Animator.StringToHash("Turn");
        static readonly int EmoteParameter = Animator.StringToHash("Emote");
        static readonly int EmoteIndexParameter = Animator.StringToHash("EmoteIndex");
        static readonly int HitParameter = Animator.StringToHash("Hit");
        static readonly int HitIndexParameter = Animator.StringToHash("HitIndex");
        static readonly int StandbyParameter = Animator.StringToHash("Standby");
        const float SpeedDamping = 0.15f;
        const float TurnDamping = 0.1f;
        // Used before the robot is injected (a hit in a test or a scene without the system).
        const float DefaultHitHold = 1.2f;

        [SerializeField] RobotService _service;
        [SerializeField] RobotFace _face;
        [SerializeField, Tooltip("The Model's Animator (Rob15.controller's parameters)")] Animator _animator;
        [SerializeField, Tooltip("Turned toward what it looks at, on top of its animation")] Transform _head;
        [SerializeField, Tooltip("Where it lives (set in the scene)")] RobotSpot _spot;

        RobotManager _manager;
        AstarPath _navigation;
        bool _started;
        bool _active;
        float _lastYaw;
        float _greetUntil = float.NegativeInfinity;
        float _hitUntil = float.NegativeInfinity;
        float _nervousUntil = float.NegativeInfinity;
        float _wakeUntil = float.NegativeInfinity;
        bool _standby;
        Quaternion _headTurn = Quaternion.identity;

        public RobotRoster Roster { get; private set; }
        public RobotSettings Settings => Roster?.Settings;
        public EnemyMover Mover { get; private set; }
        public BehaviorTree Tree { get; private set; }
        public RobotService Service => _service;
        public RobotFace Face => _face;
        public RobotSpot Spot => _spot;
        public Transform Head => _head;
        public Animator Animator => _animator;
        public RobotCall Call { get; } = new();
        public RobotPersonality Personality { get; private set; }
        // Hand-off between its tasks: where it walks next.
        public Vector3 Destination { get; set; }
        public float NextThinkAt { get; set; }
        // Its number among the robots, for the beep limit.
        public int Id { get; private set; }
        public bool Active => _active;

        public bool Greeting => Time.time < _greetUntil;
        public bool Hit => Time.time < _hitUntil;
        public int HitIndex { get; private set; }
        public bool Nervous => Time.time < _nervousUntil;
        public bool LookedAt { get; set; }
        // Night, unless the player woke it for a moment.
        public bool Asleep => Roster != null && Roster.Night && Time.time >= _wakeUntil;
        // Someone walked into it: where from (StepAside reads and clears it).
        public bool StepRequested { get; private set; }
        public Vector3 StepFrom { get; private set; }
        RobotCooldown _waveCooldown;
        RobotCooldown _nervousCooldown;

        public bool CanWave(float now) => _waveCooldown.Ready(now);
        public void UseWave(float now) => _waveCooldown.Use(now, Settings.WaveCooldown);
        public bool CanGetNervous(float now) => _nervousCooldown.Ready(now);

        // Where it looks with its head: the player, or nothing (weight fades out).
        public Vector3 LookTarget { get; set; }
        public bool HasLookTarget { get; set; }

        [Inject]
        void Construct(RobotRoster roster, RobotManager manager, AstarPath navigation)
        {
            Roster = roster;
            _manager = manager;
            _navigation = navigation;
            if (_started && isActiveAndEnabled)
                Activate();
        }

        void Start()
        {
            Mover = GetComponent<EnemyMover>();
            Tree = GetComponent<BehaviorTree>();
            _started = true;
            if (Roster != null)
                Activate();
            else
                Debug.LogError($"{name}: not injected; save the scene so its RobotInstaller lists it.", this);
        }

        void OnEnable()
        {
            if (_started && Roster != null)
                Activate();
        }

        void OnDisable()
        {
            if (!_active)
                return;
            _active = false;
            Roster.Remove(this);
            Tree.StopBehavior(false);
            Mover.Halt();
        }

        void Activate()
        {
            if (_active)
                return;
            if (_spot == null)
            {
                Debug.LogError($"{name}: no RobotSpot assigned.", this);
                return;
            }
            _active = true;
            RobotSettings settings = Settings;
            Personality = RobotPersonality.Roll(settings.BaseTemper, settings.PersonalitySpread, Random.value, Random.value, Random.value, Random.value);
            Id = _manager.NextId();
            _lastYaw = transform.eulerAngles.y;
            Mover.Initialize(_navigation, settings.WalkSpeed, 0.2f, holdGround: true);
            Tree.StartBehavior();
            Roster.Add(this);
            if (_face != null)
                _face.SetMood(RobotExpression.Neutral);
        }

        public void Think() => Tree.Tick();

        // ---- Asked by the player ----

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!_active || !Roster.Enabled || _service == null)
                return false;
            prompt = new InteractionPrompt(_service.Verb.Text(), _service.DisplayName.Text());
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (!_active || !Roster.Enabled)
                return;
            // Later: open its service's menu.
            Greet(Time.time, fresh: true);
        }

        public bool TryWhistle(GameObject actor)
        {
            if (!_active || !Roster.Enabled)
                return false;
            Vector3 player = actor.transform.position;
            float now = Time.time;
            // No way to the player (the house robot: the doorway is narrower than the navmesh allows): it waves from here.
            if (!TryPlanMeet(player, out Vector3 point))
            {
                Greet(now, fresh: true);
                return true;
            }
            Wake(now);
            bool already = Call.Active;
            Call.Begin(point, player, now);
            if (!already)
            {
                Hold(RobotExpression.Surprised, 0.5f);
                Emote(Random.value < 0.5f ? RobotEmote.ThumbsUp : RobotEmote.Jump);
                Beep(RobotSound.Called);
            }
            return true;
        }

        // Where to meet the player: ArriveDistance from them on this side, YardMargin inside the yard, on the navmesh and
        // reachable; the snapped point is checked again so it never lands past the fence.
        public bool TryPlanMeet(Vector3 player, out Vector3 point)
        {
            RobotSettings settings = Settings;
            RobotYard yard = Roster.Yard;
            Matrix4x4 worldToLocal = yard != null ? yard.WorldToLocal : Matrix4x4.identity;
            Vector3 halfSize = yard != null ? yard.HalfSize : Vector3.one * 1e6f;
            Vector3 target = RobotCall.PlanMeetPoint(transform.position, player, settings.ArriveDistance, worldToLocal, halfSize, settings.YardMargin);
            float snap = settings.YardMargin * 0.5f;
            for (int i = 0; i < 3; i++)
                if (Mover.TryFindDestination(target, snap, 0f, settings.DestinationAttempts, out point) && (yard == null || yard.Contains(point, snap)))
                    return true;
            point = default;
            return false;
        }

        // ---- IDamageable: never hurt, only startled ----

        public bool IsDead => false;

        public DamageResult TakeDamage(in DamageInfo info)
        {
            float now = Time.time;
            _hitUntil = now + (Settings != null ? Settings.HitHold : DefaultHitHold);
            HitIndex = SideOf(info.Point);
            Wake(now);
            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetInteger(HitIndexParameter, HitIndex);
                _animator.SetTrigger(HitParameter);
            }
            Hold(Random.value < 0.5f ? RobotExpression.Angry : RobotExpression.Sad, _hitUntil - now + 2f);
            Beep(RobotSound.Hit);
            return default;
        }

        // 0 front, 1 left, 2 right (the controller's Hit_ states); hits from behind play the front one.
        int SideOf(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            if (Mathf.Abs(local.x) <= Mathf.Abs(local.z))
                return 0;
            return local.x < 0f ? 1 : 2;
        }

        // ---- Set by RobotManager ----

        // Starts or keeps a greeting going; `fresh` (E, or the first look) lets it wave or talk again.
        public void Greet(float now, bool fresh)
        {
            if (!_active)
                return;
            Wake(now);
            bool started = !Greeting || fresh;
            _greetUntil = now + Settings.GreetHold;
            if (started)
                GreetingVersion++;
        }

        // Changes each time a greeting starts, so the greet task reacts again.
        public int GreetingVersion { get; private set; }

        public void MakeNervous(float now)
        {
            _nervousUntil = now + 2.2f;
            _nervousCooldown.Use(now, Settings.NervousCooldown);
        }

        public void RequestStep(Vector3 from)
        {
            StepRequested = true;
            StepFrom = from;
        }

        public void ClearStep() => StepRequested = false;

        // Awake for a while at night after the player calls or greets it.
        void Wake(float now)
        {
            if (Roster != null && Roster.Night)
                _wakeUntil = now + 12f;
        }

        // ---- Body, face, sound ----

        public void Emote(RobotEmote emote)
        {
            if (_animator == null || !_animator.isActiveAndEnabled || emote == RobotEmote.None)
                return;
            _animator.SetInteger(EmoteIndexParameter, (int)emote);
            _animator.SetTrigger(EmoteParameter);
        }

        public void Hold(RobotExpression expression, float seconds)
        {
            if (_face != null)
                _face.Hold(expression, seconds, Time.time);
        }

        public void SetMood(RobotExpression mood)
        {
            if (_face != null)
                _face.SetMood(mood);
        }

        public void Beep(RobotSound sound)
        {
            if (_manager != null && _active)
                _manager.TryBeep(this, sound);
        }

        public void SetStandby(bool standby)
        {
            if (_standby == standby)
                return;
            _standby = standby;
            if (_animator != null && _animator.isActiveAndEnabled)
                _animator.SetBool(StandbyParameter, standby);
            SetMood(standby ? RobotExpression.Sleepy : RobotExpression.Neutral);
        }

        // Every frame, from RobotManager: the legs follow the agent (walking speed; turning on the spot plays the turn clips).
        public void Advance(float deltaTime, float now)
        {
            if (!_active)
                return;
            if (_face != null)
                _face.Tick(now, Settings);
            float yaw = transform.eulerAngles.y;
            float turnRate = deltaTime > 0f ? Mathf.DeltaAngle(_lastYaw, yaw) / deltaTime : 0f;
            _lastYaw = yaw;
            if (_animator == null)
                return;
            Vector3 velocity = Mover.Velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            // A full turn clip turns 90° in a second; slower turns blend toward idle.
            float turn = speed < 0.1f ? Mathf.Clamp(turnRate / 90f, -1f, 1f) : 0f;
            _animator.SetFloat(SpeedParameter, speed, SpeedDamping, deltaTime);
            _animator.SetFloat(TurnParameter, turn, TurnDamping, deltaTime);
        }

        // After the Animator, from RobotManager: turns the head toward LookTarget, clamped, eased in and out.
        public void TurnHead(float deltaTime)
        {
            if (!_active || _head == null)
                return;
            RobotSettings settings = Settings;
            Quaternion wanted = Quaternion.identity;
            if (HasLookTarget && !Asleep)
            {
                Vector3 to = LookTarget - _head.position;
                Vector3 local = transform.InverseTransformDirection(to);
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -settings.HeadYawLimit, settings.HeadYawLimit);
                float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -settings.HeadPitchLimit, settings.HeadPitchLimit);
                wanted = Quaternion.Euler(pitch, yaw, 0f);
            }
            _headTurn = Quaternion.Slerp(_headTurn, wanted, 1f - Mathf.Exp(-settings.HeadTurnSpeed * deltaTime));
            // The turn is in the body's frame, applied on top of the animated pose whatever the bone's own axes.
            Quaternion body = transform.rotation;
            _head.rotation = body * _headTurn * Quaternion.Inverse(body) * _head.rotation;
        }

        public void Setup(RobotService service, RobotFace face, Animator animator, Transform head)
        {
            _service = service;
            _face = face;
            _animator = animator;
            _head = head;
        }

        public void SetSpot(RobotSpot spot) => _spot = spot;
    }
}
