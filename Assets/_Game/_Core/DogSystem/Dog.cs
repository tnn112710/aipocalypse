using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.LocalizationSystem;
using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // The dog entity: the bridge between the player (E: the command wheel, hold E: pet, the whistle, shots), its behaviour
    // tasks (through State and Follow) and its body (legs, animator). Advanced and thought by DogManager only.
    [RequireComponent(typeof(BehaviorTree))]
    [DisallowMultipleComponent]
    public sealed class Dog : MonoBehaviour, IInteractable, IHoldInteractable, IWhistleTarget, INameTagged, IDamageable
    {
        static readonly int SpeedParameter = Animator.StringToHash("Speed");
        static readonly int StanceParameter = Animator.StringToHash("Stance");
        static readonly int PoseParameter = Animator.StringToHash("Pose");
        static readonly int ActParameter = Animator.StringToHash("Act");
        static readonly int ActIndexParameter = Animator.StringToHash("ActIndex");
        static readonly int HitParameter = Animator.StringToHash("Hit");
        static readonly int SideParameter = Animator.StringToHash("Side");
        static readonly int JumpParameter = Animator.StringToHash("Jump");
        static readonly int JumpKindParameter = Animator.StringToHash("JumpKind");
        static readonly int SwimParameter = Animator.StringToHash("Swim");
        static readonly int MoveState = Animator.StringToHash("Move");
        static readonly int SniffState = Animator.StringToHash("Sniff");
        static readonly int SwimState = Animator.StringToHash("Swim");
        const float SpeedDamping = 0.12f;
        const float NameTagHeight = 1.05f;
        const float LiftSpeed = 1.5f;

        [SerializeField, Tooltip("The Model's Animator (BorderCollie.controller's parameters)")] Animator _animator;
        [SerializeField, Tooltip("The nested model: lifted to float at the surface while swimming")] Transform _model;

        DogManager _manager;
        bool _active;
        bool _started;
        float _lift;
        Vector3 _bodyOffset;
        Quaternion? _bodyRotation;

        public DogSettings Settings => _manager?.Settings;
        public DogState State { get; private set; }
        public DogFollow Follow { get; private set; }
        public DogLegs Legs { get; private set; }
        public BehaviorTree Tree { get; private set; }
        public float NextThinkAt { get; set; }
        public DogPose Pose { get; private set; }
        public bool Swimming { get; private set; }
        // Out of deep water: it shakes itself dry at the next chance (stopped, on its feet).
        public bool ShakePending { get; set; }
        // Metres the model floats above the agent (the agent walks the stream bed).
        public float TargetLift { get; set; }
        public bool Active => _active;
        public DogManager Manager => _manager;
        public DogRoster Roster { get; private set; }
        public Vector3 HomePosition { get; private set; }
        // Where DogFollow wants it, planned each think while it follows (DogManager).
        public FollowPlan Plan { get; set; }
        // A scripted move (into its house) drives the walk clip at this pace; negative: the legs' own speed.
        public float SpeedOverride { get; set; } = -1f;

        public void Setup(Animator animator, Transform model)
        {
            _animator = animator;
            _model = model;
        }

        [Inject]
        void Construct(DogRoster roster, DogManager manager)
        {
            Roster = roster;
            _manager = manager;
            State = new DogState(roster.Settings);
            Follow = new DogFollow(roster.Settings, name.GetHashCode());
            if (_started && isActiveAndEnabled)
                Activate();
        }

        void Start()
        {
            Legs = GetComponent<DogLegs>();
            Tree = GetComponent<BehaviorTree>();
            HomePosition = transform.position;
            _started = true;
            if (Roster != null)
                Activate();
            else
                Debug.LogError($"{name}: not injected; save the scene so its DogInstaller lists it.", this);
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
            Legs.Halt();
        }

        void Activate()
        {
            if (_active || !Roster.Enabled)
                return;
            _active = true;
            Follow.Reset(Time.time);
            Legs.Begin(Roster.Settings);
            Tree.StartBehavior();
            Roster.Add(this);
        }

        public void Think() => Tree.Tick();

        // The model moved off the agent (world offset and facing): into its dog house, where the navmesh is cut out, while
        // the agent waits at the door. Never switch the agent off instead: its entity data can be lost to another agent.
        public bool InHouse => _bodyRotation.HasValue;

        public void SetBody(Vector3 offset, Quaternion rotation)
        {
            _bodyOffset = offset;
            _bodyRotation = rotation;
        }

        public void ResetBody()
        {
            _bodyOffset = Vector3.zero;
            _bodyRotation = null;
            if (_model != null)
                _model.SetLocalPositionAndRotation(new Vector3(0f, _lift, 0f), Quaternion.identity);
        }

        // From a save: where it stood, its order; on its feet, nothing pending.
        public void Restore(Vector3 position, float yaw, DogOrder order, Vector3 stayPoint)
        {
            SpeedOverride = -1f;
            ResetBody();
            if (Legs != null && _active)
                Legs.Warp(position);
            else
                transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            State.Reset();
            State.Restore(order, stayPoint);
            SetPose(DogPose.Stand);
            SetSniffing(false);
            Follow.Reset(Time.time);
        }

        // Every frame: the animator's speed from the legs, and the swim float.
        public void Advance(float dt)
        {
            if (!_active || _animator == null)
                return;
            Vector3 velocity = Legs.Velocity;
            float speed = SpeedOverride >= 0f ? SpeedOverride : new Vector2(velocity.x, velocity.z).magnitude;
            _animator.SetFloat(SpeedParameter, speed, SpeedDamping, dt);
            _lift = Mathf.MoveTowards(_lift, Swimming ? TargetLift : 0f, LiftSpeed * dt);
            if (_model == null)
                return;
            if (_bodyRotation.HasValue)
                _model.SetPositionAndRotation(transform.position + _bodyOffset, _bodyRotation.Value);
            else
                _model.localPosition = new Vector3(0f, _lift, 0f);
        }

        // ---- Body ----

        // On its feet in the idle / walking / sniffing blend (not sitting, lying, mid-act or mid-jump): free to walk off.
        public bool Standing
        {
            get
            {
                if (_animator == null || !_animator.isActiveAndEnabled)
                    return true;
                if (_animator.IsInTransition(0))
                    return false;
                int state = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
                return state == MoveState || state == SniffState || state == SwimState;
            }
        }

        public void SetPose(DogPose pose)
        {
            Pose = pose;
            if (_animator != null)
                _animator.SetInteger(PoseParameter, (int)pose);
        }

        public void SetSniffing(bool sniffing)
        {
            if (_animator != null)
                _animator.SetInteger(StanceParameter, sniffing ? 2 : 0);
        }

        public void Act(DogAct act)
        {
            if (_animator == null)
                return;
            _animator.SetInteger(ActIndexParameter, (int)act);
            _animator.SetTrigger(ActParameter);
        }

        public void SetSwimming(bool swimming)
        {
            Swimming = swimming;
            if (_animator != null)
                _animator.SetBool(SwimParameter, swimming);
        }

        public void PlayJump(int animatorKind)
        {
            if (_animator == null)
                return;
            _animator.SetInteger(JumpKindParameter, animatorKind);
            _animator.SetTrigger(JumpParameter);
        }

        public void Flinch(int side)
        {
            if (_animator == null)
                return;
            _animator.SetInteger(SideParameter, side);
            _animator.SetTrigger(HitParameter);
        }

        // 0 front-left, 1 front-right, 2 left, 3 right, 4 back-left, 5 back-right (the Hit states' order).
        public int SideOf(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            bool left = local.x < 0f;
            if (Mathf.Abs(angle) < 45f)
                return left ? 0 : 1;
            if (Mathf.Abs(angle) > 135f)
                return left ? 4 : 5;
            return left ? 2 : 3;
        }

        bool Usable => _active && _manager != null && _manager.Enabled;
        string DisplayName => Settings.Name.Text();

        // ---- E: the command wheel; hold E: pet ----

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!Usable)
                return false;
            prompt = new InteractionPrompt(Settings.OrdersVerb.Text(), DisplayName);
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (Usable)
                _manager.OpenWheel(this);
        }

        public bool TryGetHoldPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!Usable)
                return false;
            prompt = new InteractionPrompt(Settings.PetVerb.Text(), DisplayName);
            return true;
        }

        public void InteractHold(GameObject actor)
        {
            if (Usable)
                _manager.Pet(this);
        }

        // ---- Name tag ----

        public bool TryGetNameTag(out string displayName, out Sprite icon, out Vector3 point)
        {
            displayName = null;
            icon = null;
            point = default;
            if (!Usable)
                return false;
            displayName = DisplayName;
            icon = Settings.Icon;
            point = transform.position + Vector3.up * (NameTagHeight + _lift);
            return true;
        }

        // ---- The whistle (aimed at it, or the plain whistle through DogManager's fallback) ----

        public bool TryGetWhistlePrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!Usable)
                return false;
            prompt = new InteractionPrompt(Settings.CallVerb.Text(), DisplayName);
            return true;
        }

        public bool TryWhistle(GameObject actor) => Usable && _manager.Whistled(this, actor);

        // ---- IDamageable: the player's shots only make it yelp and flinch ----

        public bool IsDead => false;

        public DamageResult TakeDamage(in DamageInfo info)
        {
            if (!Usable)
                return default;
            Flinch(SideOf(info.Point));
            _manager.Hurt(this, info);
            return default;
        }
    }
}
