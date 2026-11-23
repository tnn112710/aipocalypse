using System.Collections.Generic;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Entities;
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
    public sealed class Dog : MonoBehaviour, IInteractable, IHoldInteractable, IWhistleTarget, INameTagged, ITargetable
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
        static readonly int AttackParameter = Animator.StringToHash("Attack");
        static readonly int AttackIndexParameter = Animator.StringToHash("AttackIndex");
        static readonly int DodgeParameter = Animator.StringToHash("Dodge");
        static readonly int DownParameter = Animator.StringToHash("Down");
        static readonly int RiseParameter = Animator.StringToHash("Rise");
        static readonly int PinParameter = Animator.StringToHash("Pin");
        static readonly int CombatState = Animator.StringToHash("Combat");
        static readonly int InjuredState = Animator.StringToHash("Injured");
        static readonly int MoveState = Animator.StringToHash("Move");
        static readonly int SniffState = Animator.StringToHash("Sniff");
        static readonly int SwimState = Animator.StringToHash("Swim");
        static readonly int ShakeState = Animator.StringToHash("Act ShakeWater");
        static readonly HashSet<int> ActStates = Acts();
        const float SpeedDamping = 0.12f;
        // Sent off mid-act faster than a walk, once the act has played this long (WalkOff).
        const float ActLeaveSpeed = 1.5f;
        const float ActSettle = 0.5f;
        const float NameTagHeight = 2f;
        const float LiftSpeed = 1.5f;

        [SerializeField, Tooltip("The Model's Animator (BorderCollie.controller's parameters)")] Animator _animator;
        [SerializeField, Tooltip("The nested model: lifted to float at the surface while swimming")] Transform _model;

        DogManager _manager;
        bool _active;
        bool _started;
        float _lift;
        bool _sniffing, _combat, _injured;
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

        // The shake-dry act is playing (or blending in).
        public bool Shaking
        {
            get
            {
                if (_animator == null || !_animator.isActiveAndEnabled)
                    return false;
                return _animator.GetCurrentAnimatorStateInfo(0).shortNameHash == ShakeState
                    || (_animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).shortNameHash == ShakeState);
            }
        }
        float _lastYaw;
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
            State.ResetToughness(_manager.Stats);
            Legs.Begin(Roster.Settings);
            Tree.StartBehavior();
            Roster.Add(this);
        }

        public void Think() => Tree.Tick();

        // The model moved off the agent (world offset and facing): into its dog house, where the navmesh is cut out, while
        // the agent waits at the door. Never switch the agent off instead: its entity data can be lost to another agent.
        public bool InHouse => _bodyRotation.HasValue;
        // Mid-scramble over the fence (the body on its arc, the agent waiting on the near side).
        public bool Scrambling { get; set; }
        // Getting out after a bite: a swing at it may miss (DodgeChance).
        public bool Escaping { get; set; }
        // The side the last hit came from (it falls that way when knocked down).
        public bool LastHitLeft { get; private set; }

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
        public void Restore(Vector3 position, float yaw, DogOrder order, Vector3 stayPoint, float herdFor, int howledNight)
        {
            SpeedOverride = -1f;
            ResetBody();
            if (Legs != null && _active)
                Legs.Warp(position);
            else
                transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            State.Reset();
            State.Restore(order, stayPoint, herdFor, howledNight, Time.time);
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
            float yaw = transform.eulerAngles.y;
            float yawRate = dt > 0f ? Mathf.DeltaAngle(_lastYaw, yaw) / dt : 0f;
            _lastYaw = yaw;
            float speed = SpeedOverride >= 0f
                ? SpeedOverride
                : DogTurn.AnimatorSpeed(new Vector2(velocity.x, velocity.z).magnitude, yawRate, Pose == DogPose.Stand && !Swimming && !InHouse, Settings);
            _animator.SetFloat(SpeedParameter, speed, SpeedDamping, dt);
            WalkOff.Apply(_animator, ActStates, MoveState, new Vector2(velocity.x, velocity.z).magnitude, ActLeaveSpeed, ActSettle);
            _lift = Mathf.MoveTowards(_lift, Swimming ? TargetLift : 0f, LiftSpeed * dt);
            if (_model == null)
                return;
            if (_bodyRotation.HasValue)
                _model.SetPositionAndRotation(transform.position + _bodyOffset, _bodyRotation.Value);
            else
                _model.localPosition = new Vector3(0f, _lift, 0f);
        }

        static HashSet<int> Acts()
        {
            var states = new HashSet<int>();
            foreach (DogAct act in System.Enum.GetValues(typeof(DogAct)))
                states.Add(Animator.StringToHash("Act " + act));
            return states;
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
                return state == MoveState || state == SniffState || state == SwimState || state == CombatState || state == InjuredState;
            }
        }

        public void SetPose(DogPose pose)
        {
            Pose = pose;
            if (_animator == null)
                return;
            _animator.SetInteger(PoseParameter, (int)pose);
            if (pose != DogPose.Stand)
                _animator.ResetTrigger(ActParameter);
        }

        public void SetSniffing(bool sniffing)
        {
            _sniffing = sniffing;
            UpdateStance();
        }

        // The fighting stance (growling, low) while it skirmishes.
        public void SetCombat(bool combat)
        {
            _combat = combat;
            UpdateStance();
        }

        // Limping after being helped up.
        public void SetInjured(bool injured)
        {
            _injured = injured;
            UpdateStance();
        }

        void UpdateStance()
        {
            if (_animator != null)
                _animator.SetInteger(StanceParameter, _injured ? 3 : _sniffing ? 2 : _combat ? 1 : 0);
        }

        // 0 bite forward, 1 front left, 2 front right, 3 feint.
        public void Attack(int index)
        {
            if (_animator == null)
                return;
            _animator.SetInteger(AttackIndexParameter, index);
            _animator.SetTrigger(AttackParameter);
        }

        // Side 0 left, 1 right.
        public void Dodge(int side)
        {
            if (_animator == null)
                return;
            _animator.SetInteger(SideParameter, side);
            _animator.SetTrigger(DodgeParameter);
        }

        public void KnockDown(int side)
        {
            if (_animator == null)
                return;
            _animator.SetInteger(SideParameter, side);
            _animator.SetTrigger(DownParameter);
        }

        public void Rise()
        {
            if (_animator != null)
                _animator.SetTrigger(RiseParameter);
        }

        // A robot that just greeted it (DogHomeLife goes over and sniffs it), cleared when done. Robots greet it only while
        // it's in home life (`InHomeLife`, set by DogHomeLife).
        public Transform GreetedBy { get; set; }
        public bool InHomeLife { get; set; }

        // The last requested bite: 0 not dealt yet, 1 a bite, 2 a takedown (DogManager.RequestBite).
        public int BiteLanded { get; set; }

        // Holding a dragged-down enemy (the fight branch stays on while it does, the enemy already gone).
        public bool Pinning { get; private set; }

        public void SetPin(bool pinning)
        {
            Pinning = pinning;
            if (_animator != null)
                _animator.SetBool(PinParameter, pinning);
        }

        // Acts only play on its feet: one asked for while it sits or lies would wait in the animator and go off seconds later,
        // once it's up (a happy wag holding it back from following), so it's dropped.
        public void Act(DogAct act)
        {
            if (_animator == null || Pose != DogPose.Stand)
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

        public bool Usable => _active && _manager != null && _manager.Enabled;
        string DisplayName => Settings.Name.Text();

        // ---- E: the command wheel; hold E: pet ----

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!Usable || State.Down)
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
            prompt = new InteractionPrompt((State.Down ? Settings.HelpUpVerb : Settings.PetVerb).Text(), DisplayName);
            return true;
        }

        // Down: helped up. Else: a pet.
        public void InteractHold(GameObject actor)
        {
            if (!Usable)
                return;
            if (State.Down)
                _manager.HelpUp(this);
            else
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

        // ---- ITargetable: enemies only go for it when it provoked them (EnemyBrain.Retaliate) ----

        public Transform Transform => transform;
        public TargetKind Kind => TargetKind.Dog;
        public bool IsTargetable => _active && State != null && !State.Down;

        // It's never killed, only knocked down.
        public bool IsDead => false;

        // The player's shots: a yelp and a flinch. Others wear its toughness down (a flinch, or knocked down at 0); while
        // escaping a swing may miss (a dodge). Down, nothing reaches it.
        public DamageResult TakeDamage(in DamageInfo info)
        {
            if (!Usable)
                return default;
            DogHit hit = State.Hit(info.Amount, _manager.IsPlayer(info.Source), Escaping, _manager.Roll(), Time.time, _manager.Stats);
            bool left = transform.InverseTransformPoint(info.Point).x < 0f;
            LastHitLeft = left;
            switch (hit)
            {
                case DogHit.Yelped:
                    Flinch(SideOf(info.Point));
                    _manager.TryPlay(this, DogSound.Yelp);
                    return default;
                case DogHit.Dodged:
                    Dodge(left ? 1 : 0);
                    return default;
                case DogHit.Flinched:
                    Flinch(SideOf(info.Point));
                    _manager.TryPlay(this, DogSound.Yelp);
                    return new DamageResult(info.Amount, false);
                case DogHit.KnockedDown:
                    // DogManager plays the fall when it sees it down (whatever knocked it down).
                    _manager.TryPlay(this, DogSound.Yelp);
                    return new DamageResult(info.Amount, false);
                default:
                    return default;
            }
        }
    }
}
