using System.Collections.Generic;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Entities;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MessagePipe;
using Opsive.BehaviorDesigner.Runtime;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // A robot at home: the bridge between the player (E, hold E to pat, the whistle, shots), its behaviour tasks (through
    // State, Call and its role's Work) and its body (legs, face, emotes, head). Advanced and thought by RobotManager only.
    [RequireComponent(typeof(BehaviorTree))]
    [DisallowMultipleComponent]
    public sealed class Robot : MonoBehaviour, IInteractable, IHoldInteractable, IWhistleTarget, INameTagged, IDamageable
    {
        static readonly int SpeedParameter = Animator.StringToHash("Speed");
        static readonly int TurnParameter = Animator.StringToHash("Turn");
        static readonly int EmoteParameter = Animator.StringToHash("Emote");
        static readonly int EmoteIndexParameter = Animator.StringToHash("EmoteIndex");
        static readonly int HitParameter = Animator.StringToHash("Hit");
        static readonly int HitIndexParameter = Animator.StringToHash("HitIndex");
        static readonly int KnockParameter = Animator.StringToHash("Knock");
        static readonly int DownParameter = Animator.StringToHash("Down");
        static readonly int StandbyParameter = Animator.StringToHash("Standby");
        static readonly int LocomotionState = Animator.StringToHash("Locomotion");
        static readonly HashSet<int> EmoteStates = Emotes();
        const float SpeedDamping = 0.15f;
        const float TurnDamping = 0.1f;
        // Called away mid-emote, once the emote has played this long (WalkOff).
        const float EmoteLeaveSpeed = 0.5f;
        const float EmoteSettle = 0.5f;
        // A fallback meeting point this much above or below the player is on another floor (the house's is 1 m up).
        const float MeetHeightSlack = 0.6f;

        [SerializeField, FormerlySerializedAs("_service"), Tooltip("Its job: a RobotService (menu) or a RobotJob (work)")] RobotRole _role;
        [SerializeField] RobotFace _face;
        [SerializeField, Tooltip("The Model's Animator (Rob15.controller's parameters)")] Animator _animator;
        [SerializeField, Tooltip("Turned toward what it looks at, on top of its animation")] Transform _head;
        [SerializeField, Tooltip("Where it lives (set in the scene); none for one that roams with the player (a guide)")] RobotSpot _spot;

        RobotManager _manager;
        IPublisher<RobotServiceRequested> _serviceRequests;
        readonly ActivationGate _gate = new();
        bool _active;
        bool _rolled;
        float _lastYaw;
        Vector3 _startPosition;
        Vector3 _startFacing = Vector3.forward;
        bool _standby;
        bool _down;
        bool _hasTaskMood;
        RobotExpression _taskMood;
        Quaternion _headTurn = Quaternion.identity;

        public RobotRoster Roster { get; private set; }
        public RobotSettings Settings => Roster?.Settings;
        public RobotState State { get; private set; }
        public RobotCall Call { get; } = new();
        public IRobotLegs Mover { get; private set; }
        public BehaviorTree Tree { get; private set; }
        public RobotRole Role => _role;
        public RobotService Service => _role as RobotService;
        // Its job's work (null for menu robots).
        public IRobotWork Work { get; private set; }
        public RobotFace Face => _face;
        public Animator Animator => _animator;
        public RobotSpot Spot => _spot;
        // Where it lives and faces there (its spot, else where it started), where it hides from a raid, how far it strolls.
        public Vector3 Home => _spot != null ? _spot.Center : _startPosition;
        public Vector3 HomeFacing => _spot != null ? _spot.Facing : _startFacing;
        public Vector3 Shelter => _spot != null ? _spot.Shelter : Home;
        public Vector3 ShelterFacing => _spot != null ? _spot.ShelterFacing : HomeFacing;
        public float HomeRadius => _spot != null ? _spot.Radius : 0f;
        // Within its home's radius and out of its NoGo boxes (anywhere, without a spot).
        public bool Allows(Vector3 point) => _spot == null || _spot.Allows(point);
        public bool InNoGo(Vector3 point) => _spot != null && _spot.InNoGo(point);
        public Transform Head => _head;
        public RobotPersonality Personality { get; private set; }
        public float NextThinkAt { get; set; }
        // Its number among the robots (the beep limit, staggered thinks), kept for its lifetime.
        public int Id { get; private set; } = -1;
        public bool Active => _active;
        public string DisplayName => _role != null ? _role.DisplayName.Text() : name;

        // Night, unless the player woke it for a moment.
        public bool Asleep => Roster != null && State.Asleep(Roster.Night, Time.time);

        // Where it looks with its head: the player, or nothing (the turn eases out).
        public Vector3 LookTarget { get; set; }
        public bool HasLookTarget { get; set; }

        [Inject]
        void Construct(RobotRoster roster, RobotManager manager, IPublisher<RobotServiceRequested> serviceRequests)
        {
            Roster = roster;
            _manager = manager;
            _serviceRequests = serviceRequests;
            State = new RobotState(roster.Settings);
            if (_gate.OnConstructed(isActiveAndEnabled))
                Activate();
        }

        void Start()
        {
            Mover = GetComponent<IRobotLegs>();
            Tree = GetComponent<BehaviorTree>();
            if (_gate.OnStarted())
                Activate();
            else
                Debug.LogError($"{name}: not injected; save the scene so its RobotInstaller lists it.", this);
        }

        void OnEnable()
        {
            if (_gate.OnEnabled())
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
            Call.End();
            State.Reset();
            _standby = false;
            _down = false;
        }

        void Activate()
        {
            if (_active)
                return;
            if (Mover == null)
            {
                Debug.LogError($"{name}: no legs (an IRobotLegs component, e.g. RobotGroundLegs).", this);
                return;
            }
            _active = true;
            RobotSettings settings = Settings;
            if (!_rolled)
            {
                _rolled = true;
                _startPosition = transform.position;
                _startFacing = transform.forward;
                Personality = RobotPersonality.Roll(settings.BaseTemper, settings.PersonalitySpread, Random.value, Random.value, Random.value, Random.value);
                Id = _manager.NextId();
                Work = _role is RobotJob job ? job.CreateWork(this) : null;
            }
            NextThinkAt = Time.time + settings.ThinkInterval * (Id % 4) / 4f;
            _lastYaw = transform.eulerAngles.y;
            Mover.Begin(settings);
            Tree.StartBehavior();
            Roster.Add(this);
            ClearMood();
        }

        public void Think() => Tree.Tick();

        bool Usable => _active && Roster.Enabled;

        // ---- E: its menu (for now a greeting); hold E: a pat ----

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!Usable || _role == null)
                return false;
            prompt = new InteractionPrompt(_role.Verb.Text(), _role.DisplayName.Text());
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (!Usable)
                return;
            float now = Time.time;
            State.Wake(now);
            State.Greet(now, fresh: true);
            if (Service != null)
                _serviceRequests?.Publish(new RobotServiceRequested(this, Service));
        }

        public bool TryGetHoldPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!Usable || _role == null || State.Down(Time.time))
                return false;
            prompt = new InteractionPrompt(Settings.PatVerb.Text(), _role.DisplayName.Text());
            return true;
        }

        public void InteractHold(GameObject actor)
        {
            if (!Usable)
                return;
            float now = Time.time;
            State.Wake(now);
            State.Pat(now);
        }

        // ---- Name tag: its name and job icon over its head while aimed at ----

        public bool TryGetNameTag(out string displayName, out Sprite icon, out Vector3 point)
        {
            displayName = null;
            icon = null;
            point = default;
            if (!Usable || _role == null)
                return false;
            displayName = _role.DisplayName.Text();
            icon = _role.Icon;
            point = (_head != null ? _head.position : transform.position + Vector3.up) + Vector3.up * Settings.NameTagHeight;
            return true;
        }

        // ---- The whistle ----

        public bool TryGetWhistlePrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!Usable || _role == null)
                return false;
            prompt = new InteractionPrompt(Settings.CallVerb.Text(), _role.DisplayName.Text());
            return true;
        }

        public bool TryWhistle(GameObject actor)
        {
            if (!Usable)
                return false;
            float now = Time.time;
            State.Wake(now);
            // Hiding from a raid: it won't come out.
            if (Roster.Raid)
            {
                Emote(RobotEmote.No);
                Hold(RobotExpression.Worried, 2.5f);
                Beep(RobotSound.Nervous);
                return true;
            }
            switch (State.Whistle(now))
            {
                // Whistled at too often: a grumpy head shake, and it stops coming.
                case RobotWhistle.Annoyed:
                    Call.End();
                    Emote(RobotEmote.No);
                    Hold(RobotExpression.Angry, 2.5f);
                    Beep(RobotSound.Shrug);
                    return true;
                case RobotWhistle.Ignored:
                    Hold(RobotExpression.Angry, 1.5f);
                    return true;
            }
            Vector3 player = actor.transform.position;
            // No way to the player: it waves from here.
            if (!TryPlanMeet(player, out Vector3 point))
            {
                State.Greet(now, fresh: true);
                return true;
            }
            bool already = Call.Active;
            Call.Begin(point, player, now, RobotCallReason.Whistle);
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
            // Nothing walkable there (the player against a wall, whistling through it): anywhere reachable near them, on
            // their floor (not the house floor behind the wall).
            float around = settings.ArriveDistance * 2f;
            for (int i = 0; i < 3; i++)
                if (Mover.TryFindDestination(player, around, 0f, settings.DestinationAttempts, out point) && (yard == null || yard.Contains(point, snap))
                    && Mathf.Abs(point.y - player.y) < MeetHeightSlack)
                    return true;
            point = default;
            return false;
        }

        // ---- IDamageable: never hurt, only startled (or knocked over) ----

        public bool IsDead => false;

        public DamageResult TakeDamage(in DamageInfo info)
        {
            if (!Usable)
                return default;
            float now = Time.time;
            State.Wake(now);
            RobotHit hit = State.TakeHit(now, SideOf(info.Point));
            if (hit == RobotHit.None)
                return default;
            Call.End();
            if (_animator != null && _animator.isActiveAndEnabled)
            {
                if (hit == RobotHit.Knockdown)
                {
                    // Shot from the front it falls on its back (Fall_0), from behind on its face (Fall_1).
                    _animator.SetInteger(HitIndexParameter, transform.InverseTransformPoint(info.Point).z >= 0f ? 0 : 1);
                    _animator.SetTrigger(KnockParameter);
                }
                else
                {
                    _animator.SetInteger(HitIndexParameter, State.HitIndex);
                    _animator.SetTrigger(HitParameter);
                }
            }
            if (hit == RobotHit.Knockdown)
                Hold(RobotExpression.Dizzy, Settings.KnockdownLie + 0.3f);
            else
                Hold(Random.value < 0.5f ? RobotExpression.Angry : RobotExpression.Sad, Settings.HitHold + 2f);
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

        // ---- Body, face, sound ----

        float _visitorReadyAt;

        // The dog (or another visitor) came close: a free, awake robot turns, waves and shows heart eyes, at most every 90 s.
        public bool TryGreetVisitor(Vector3 at, float now)
        {
            if (!Usable || Roster.Raid || now < _visitorReadyAt || !IsFree(now))
                return false;
            _visitorReadyAt = now + 90f;
            Mover.Face(at);
            Emote(RobotEmote.Hello);
            Hold(RobotExpression.Love, 2f);
            Beep(RobotSound.Happy);
            return true;
        }

        // Awake and doing nothing it was asked to: not called, hit, greeted, nervous, cheering, welcoming, seeing the player
        // off, stepping aside, working or already chatting.
        public bool IsFree(float now) =>
            !Asleep && !Call.Active && !State.Hit(now) && !State.Greeting(now)
            && !State.Nervous(now) && !State.Cheering(now) && !State.WelcomePending && !State.SeeingOff(now)
            && !State.Chatting(now) && !State.StepRequested(now) && (Work == null || !Work.WantsToWork(now));

        // Free, and its chat cooldown is over.
        public bool FreeToChat(float now) => State.CanChat(now) && IsFree(now);

        public void Emote(RobotEmote emote)
        {
            // Knocked over, nothing plays over the fall and the getting up.
            if (_animator == null || !_animator.isActiveAndEnabled || emote == RobotEmote.None || (State != null && State.Down(Time.time)))
                return;
            // Already playing it: a trigger left set would replay it once it's back in Locomotion.
            int state = Animator.StringToHash("Emote_" + (int)emote);
            if (_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == state || _animator.GetNextAnimatorStateInfo(0).shortNameHash == state)
                return;
            _animator.SetInteger(EmoteIndexParameter, (int)emote);
            _animator.SetTrigger(EmoteParameter);
        }

        public void Hold(RobotExpression expression, float seconds)
        {
            if (_face != null)
                _face.Hold(expression, seconds, Time.time);
        }

        // A task's mood while it runs (happy while greeting); ClearMood goes back to the resting one.
        public void SetMood(RobotExpression mood)
        {
            _hasTaskMood = true;
            _taskMood = mood;
        }

        public void ClearMood() => _hasTaskMood = false;

        // Sleepy on standby, angry while sulking, else neutral.
        RobotExpression RestingMood(float now) =>
            _standby ? RobotExpression.Sleepy : State.Sulking(now) ? RobotExpression.Angry : RobotExpression.Neutral;

        public void Beep(RobotSound sound)
        {
            if (_manager != null && _active)
                _manager.TryBeep(this, sound);
        }

        public void SetStandby(bool standby)
        {
            _standby = standby;
            if (_animator != null && _animator.isActiveAndEnabled)
                _animator.SetBool(StandbyParameter, standby);
        }

        // Every frame, from RobotManager: face, the legs following the agent (turning on the spot plays the turn clips), and
        // lying down while knocked over.
        public void Advance(float deltaTime, float now)
        {
            if (!_active)
                return;
            if (_face != null)
            {
                _face.SetMood(_hasTaskMood ? _taskMood : RestingMood(now));
                _face.Tick(now, deltaTime, Settings);
            }
            float yaw = transform.eulerAngles.y;
            float turnRate = deltaTime > 0f ? Mathf.DeltaAngle(_lastYaw, yaw) / deltaTime : 0f;
            _lastYaw = yaw;
            if (_animator == null || !_animator.isActiveAndEnabled)
                return;
            bool down = State.Lying(now);
            if (down != _down)
            {
                _down = down;
                _animator.SetBool(DownParameter, down);
            }
            Vector3 velocity = Mover.Velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            // A full turn clip turns 90° in a second; slower turns blend toward idle.
            float turn = speed < 0.1f ? Mathf.Clamp(turnRate / 90f, -1f, 1f) : 0f;
            _animator.SetFloat(SpeedParameter, speed, SpeedDamping, deltaTime);
            _animator.SetFloat(TurnParameter, turn, TurnDamping, deltaTime);
            WalkOff.Apply(_animator, EmoteStates, LocomotionState, speed, EmoteLeaveSpeed, EmoteSettle);
        }

        // Every emote state but the jump (the door hop plays it while moving).
        static HashSet<int> Emotes()
        {
            var states = new HashSet<int> { Animator.StringToHash("Emote_" + (int)RobotEmote.Brave + "_Punch") };
            foreach (RobotEmote emote in System.Enum.GetValues(typeof(RobotEmote)))
                if (emote != RobotEmote.None && emote != RobotEmote.Jump)
                    states.Add(Animator.StringToHash("Emote_" + (int)emote));
            return states;
        }

        // After the Animator, from RobotManager: turns the head toward LookTarget, clamped, eased in and out. The turn goes
        // on top of the animated pose, so the Animator must write the head every frame (the model animates even off screen).
        public void TurnHead(float deltaTime, float now)
        {
            if (!_active || _head == null || _animator == null)
                return;
            RobotSettings settings = Settings;
            Quaternion wanted = Quaternion.identity;
            if (HasLookTarget && !Asleep && !State.Down(now))
            {
                Vector3 local = transform.InverseTransformDirection(LookTarget - _head.position);
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -settings.HeadYawLimit, settings.HeadYawLimit);
                float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -settings.HeadPitchLimit, settings.HeadPitchLimit);
                wanted = Quaternion.Euler(pitch, yaw, 0f);
            }
            _headTurn = Quaternion.Slerp(_headTurn, wanted, 1f - Mathf.Exp(-settings.HeadTurnSpeed * deltaTime));
            // In the body's frame, whatever the bone's own axes.
            Quaternion body = transform.rotation;
            _head.rotation = body * _headTurn * Quaternion.Inverse(body) * _head.rotation;
        }

        public void Setup(RobotRole role, RobotFace face, Animator animator, Transform head)
        {
            _role = role;
            _face = face;
            _animator = animator;
            _head = head;
        }

        public void SetSpot(RobotSpot spot) => _spot = spot;
    }
}
