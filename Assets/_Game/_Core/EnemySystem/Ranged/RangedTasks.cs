using MB13.Aipocalypse.Core.Entities;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.Shared.Utility;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // The target is within RangedSettings.MeleeRange: it fights hand to hand (the Melee tasks) instead of backing away. Also
    // the abort check of that branch: back to throwing once the target is further off.
    [Category("Aipocalypse/Ranged")]
    public sealed class TargetInMeleeReach : ConditionalNode
    {
        EnemyBrain _brain;

        public override void OnAwake() => _brain = GetComponent<EnemyBrain>();

        public override TaskStatus OnUpdate()
        {
            ITargetable target = _brain.Target;
            RangedSettings ranged = _brain.Definition.Ranged;
            if (target == null || ranged == null || ranged.MeleeRange <= 0f || _brain.Definition.Melee == null || target.Kind == TargetKind.Farm)
                return TaskStatus.Failure;
            float reach = ranged.MeleeRange;
            return MeleeHit.SqrFlatDistance(_brain.transform.position, target.PointFor(_brain.transform.position)) <= reach * reach
                ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    // Gets to where it throws from: runs at the target until it's inside its own stand-off distance (each life picks one in
    // RangedSettings.StandOff), backs away when the target comes inside MinRange, and settles for anywhere inside MaxRange
    // when it can't get closer (a shut fence: it throws over it).
    [Category("Aipocalypse/Ranged")]
    public sealed class TakeRangedPosition : ActionNode
    {
        // Slack on the stand-off distance once there, so it doesn't shuffle at the edge.
        const float Slack = 1.2f;
        // Seconds between two looks for a way on while stuck out of range.
        const float RetryInterval = 1.5f;

        EnemyBrain _brain;
        EnemyMover _mover;
        Vector3 _heading;
        float _nextRetry;
        int _interruptions;
        bool _moving;
        bool _retreating;

        public override void OnAwake()
        {
            _brain = GetComponent<EnemyBrain>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart()
        {
            _moving = false;
            _retreating = false;
            _nextRetry = 0f;
            MeleeSettings pace = _brain.Definition.Melee;
            if (pace != null)
                _brain.Stats.AddModifier(new EnemyStatModifier(EnemyStat.MoveSpeed, this, multiply: pace.ApproachSpeedMultiplier));
        }

        public override TaskStatus OnUpdate()
        {
            ITargetable target = _brain.Target;
            RangedSettings ranged = _brain.Definition.Ranged;
            if (target == null || ranged == null)
                return TaskStatus.Failure;

            Vector3 self = _brain.transform.position;
            Vector3 position = target.PointFor(self);
            float distance = Mathf.Sqrt(MeleeHit.SqrFlatDistance(self, position));
            float standOff = _brain.StandOff(ranged);
            _brain.Engaged = distance <= ranged.MaxRange;
            // Fighting back at the dog that bit it: it shoots from where it stands, never moves for it.
            if (_brain.Retaliating)
                return TaskStatus.Success;

            bool tooClose = ranged.MinRange > 0f && distance < ranged.MinRange;
            if (_retreating)
                tooClose = distance < standOff * 0.9f && !_mover.Arrived;
            if (!tooClose && distance <= standOff * (_moving ? 1f : Slack))
                return TaskStatus.Success;

            bool interrupted = _brain.Interruptions != _interruptions;
            if (tooClose)
            {
                // Straight away from the target, to its stand-off distance (the navmesh stops it at walls and water).
                if (!_retreating || interrupted)
                {
                    Vector3 away = self - position;
                    away.y = 0f;
                    away = away.sqrMagnitude > 0.01f ? away.normalized : -_brain.transform.forward;
                    _mover.MoveTo(position + away * standOff, float.PositiveInfinity);
                    _retreating = true;
                    _moving = true;
                    _interruptions = _brain.Interruptions;
                }
                return TaskStatus.Running;
            }
            _retreating = false;

            // As close as the navmesh lets it (a shut fence) and in range: good enough.
            if (_moving && _mover.Arrived)
            {
                if (distance <= ranged.MaxRange)
                    return TaskStatus.Success;
                if (Time.time < _nextRetry)
                    return TaskStatus.Running;
                _moving = false;
            }

            float repath = Mathf.Max(1f, 0.1f * distance);
            if (!_moving || interrupted || (position - _heading).sqrMagnitude > repath * repath)
            {
                _mover.MoveTo(position, float.PositiveInfinity);
                _heading = position;
                _interruptions = _brain.Interruptions;
                _moving = true;
                _nextRetry = Time.time + RetryInterval;
            }
            return TaskStatus.Running;
        }

        public override void OnEnd()
        {
            _mover.Halt();
            _brain.Stats.RemoveModifiers(this);
        }
    }

    // Faces the target and throws once: waits out its cooldown, plans the throw (RangedAim: straight with a clear line, a
    // lob over what's in the way), winds up (the attack clip, the wind-up sound), lets go at ReleaseTime aimed afresh, and
    // recovers. It never shoots through one of its own side (RangedSettings.Friends) standing in the straight line: it lobs
    // over them, holding fire while no lob reaches. Succeeds after the throw, or straight away when the target left its
    // range, came too close, it was staggered (the throw is cancelled) or no throw reaches from here (it then stands closer
    // next time).
    [Category("Aipocalypse/Ranged")]
    public sealed class RangedAttack : ActionNode
    {
        // Slack on MaxRange before it gives up its spot.
        const float RangeSlack = 1.15f;
        // Its stand-off distance shrinks by this share each time no throw reached.
        const float CloserShare = 0.8f;
        // Metres off the straight line within which one of its own side counts as standing in it; checked from its chest, not
        // the bow or the hand, which the wind-up moves.
        const float FriendRadius = 0.5f;

        EnemyBrain _brain;
        EnemyMover _mover;
        ITargetable _throwTarget;
        Vector3 _lastPosition;
        Vector3 _targetVelocity;
        float _lastSeen;
        float _releaseAt;
        float _endAt;
        int _interruptions;
        bool _released;

        public override void OnAwake()
        {
            _brain = GetComponent<EnemyBrain>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart()
        {
            _endAt = 0f;
            _released = false;
            _lastSeen = 0f;
            _targetVelocity = Vector3.zero;
            _interruptions = _brain.Interruptions;
        }

        public override TaskStatus OnUpdate()
        {
            ITargetable target = _brain.Target;
            RangedSettings ranged = _brain.Definition.Ranged;
            if (target == null || ranged == null)
                return TaskStatus.Failure;
            // Staggered (winding up or not): the throw is cancelled.
            if (_brain.Interruptions != _interruptions)
                return TaskStatus.Success;

            Vector3 self = _brain.transform.position;
            Vector3 position = target.PointFor(self);
            Track(position);
            _mover.Face(position);

            if (_endAt > 0f)
            {
                if (!_released && Time.time >= _releaseAt)
                {
                    _released = true;
                    if (ReferenceEquals(target, _throwTarget) && !target.IsDead)
                        Release(ranged, target, position);
                }
                return Time.time >= _endAt ? TaskStatus.Success : TaskStatus.Running;
            }

            float distance = Mathf.Sqrt(MeleeHit.SqrFlatDistance(self, position));
            if (distance > ranged.MaxRange * RangeSlack || (ranged.MinRange > 0f && distance < ranged.MinRange))
                return TaskStatus.Success;
            // Its objective (the farm) isn't thrown at: it stands there until ChooseTarget drops it or the player turns up.
            if (target.Kind == TargetKind.Farm)
                return TaskStatus.Running;
            if (Time.time < _brain.NextAttackTime)
                return TaskStatus.Running;
            Vector3 aimAt = position + Vector3.up * ranged.AimHeight;
            // One of its own in the straight line: it lobs over them, and holds fire while no lob reaches.
            bool friendInLine = ranged.Friends != 0
                && RangedAim.FriendInLine(self + Vector3.up * EnemySight.TargetHeight, aimAt, FriendRadius, ranged.Friends, _brain.transform);
            if (!RangedAim.TryPlan(ranged, _brain.LaunchPoint, aimAt, _targetVelocity, _brain.transform, target.Transform, out RangedThrow plan, friendInLine))
            {
                if (friendInLine)
                    return TaskStatus.Running;
                _brain.CloseIn(ranged, CloserShare);
                _brain.NextAttackTime = Time.time + 0.5f;
                return TaskStatus.Success;
            }

            _throwTarget = target;
            _releaseAt = Time.time + ranged.ReleaseTime;
            _endAt = Time.time + ranged.AttackDuration;
            _brain.NextAttackTime = _endAt + Random.Range(ranged.Cooldown.x, ranged.Cooldown.y);
            if (_brain.Animation != null)
                _brain.Animation.Attack();
            _brain.Voice.Play(ranged.WindUp);
            return TaskStatus.Running;
        }

        public override void OnEnd()
        {
            _mover.Halt();
            // Cut off before the release (a stagger, an abort): the spear is still in the hand.
            if (_brain.Animation != null)
            {
                _brain.Animation.ShowHeldProjectile(true);
                if (_endAt > 0f && Time.time < _endAt)
                    _brain.Animation.CancelAttack();
            }
        }

        // Aimed afresh at the moment of letting go: the wind-up doesn't lock the aim.
        void Release(RangedSettings ranged, ITargetable target, Vector3 position)
        {
            Vector3 from = _brain.LaunchPoint;
            if (!RangedAim.TryPlan(ranged, from, position + Vector3.up * ranged.AimHeight, _targetVelocity, _brain.transform, target.Transform,
                    out RangedThrow plan))
                return;
            Vector3 landing = plan.AimPoint;
            landing.y = position.y;
            _brain.Throw(new ProjectileShot
            {
                Definition = ranged.Projectile,
                Origin = from,
                Velocity = plan.Velocity,
                Damage = _brain.Stats.Get(EnemyStat.AttackDamage),
                Source = _brain.gameObject,
                Hurts = target.Kind,
                Lobbed = plan.Lobbed,
                LandingPoint = landing,
            });
            _brain.Voice.Play(ranged.Launch);
            if (_brain.Animation != null)
                _brain.Animation.ShowHeldProjectile(false);
        }

        // The target's speed over the ground, from where it was at the last think (thinks come about 15 times a second).
        void Track(Vector3 position)
        {
            float now = Time.time;
            if (_lastSeen > 0f && now > _lastSeen)
            {
                Vector3 velocity = (position - _lastPosition) / (now - _lastSeen);
                // A teleport or a respawn isn't movement.
                _targetVelocity = velocity.sqrMagnitude < 400f ? Vector3.Lerp(_targetVelocity, velocity, 0.5f) : Vector3.zero;
            }
            _lastPosition = position;
            _lastSeen = now;
        }
    }
}
