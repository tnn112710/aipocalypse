using Aipocalypse.Core.Entities;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.Shared.Utility;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // Tasks of the shared Melee subtree. Tuning comes from the enemy's definition (EnemyDefinition.Melee); the hunted target
    // is EnemyBrain.Target.
    [Category("Aipocalypse/Melee")]
    public sealed class ChooseTarget : ConditionalNode
    {
        EnemyBrain _brain;

        public override void OnAwake() => _brain = GetComponent<EnemyBrain>();

        // Also the abort check: re-run every think while the hunt or the wander below it runs.
        public override TaskStatus OnUpdate() => _brain.ChooseTarget() != null ? TaskStatus.Success : TaskStatus.Failure;
    }

    // What it last hunted died (EnemyBrain.TargetDefeated).
    [Category("Aipocalypse/Melee")]
    public sealed class TargetDefeated : ConditionalNode
    {
        EnemyBrain _brain;

        public override void OnAwake() => _brain = GetComponent<EnemyBrain>();
        public override TaskStatus OnUpdate() => _brain.TargetDefeated ? TaskStatus.Success : TaskStatus.Failure;
    }

    // Stands where it is and celebrates (a random dance, repeated) until something aborts it (a new target).
    [Category("Aipocalypse/Melee")]
    public sealed class Celebrate : ActionNode
    {
        EnemyBrain _brain;
        EnemyMover _mover;

        public override void OnAwake()
        {
            _brain = GetComponent<EnemyBrain>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart()
        {
            _mover.Halt();
            if (_brain.Animation != null)
                _brain.Animation.Celebrate(true);
        }

        public override TaskStatus OnUpdate() => TaskStatus.Running;

        public override void OnEnd()
        {
            if (_brain.Animation != null)
                _brain.Animation.Celebrate(false);
        }
    }

    // Runs at the target (approach pace far away, sprint and fighting stance inside EngageRadius) until within attack range.
    // From afar it comes in from its own side (CrowdSpread.FlankPoint), straightening as it closes. Without a free attack
    // slot it waits at HoldDistance, facing the target.
    [Category("Aipocalypse/Melee")]
    public sealed class ChaseTarget : ActionNode
    {
        // Slack on EngageRadius and HoldDistance before letting go, so the stance and the slot don't flicker at the edge.
        const float Slack = 1.25f;
        // A flanking chase re-aims once its squared distance drops below this share of the last aim's. Must stay above
        // sin² of the widest FlankAngle (60°: 0.75), the closest its line to the aim passes the target.
        const float FlankReaim = 0.8f;

        EnemyBrain _brain;
        EnemyMover _mover;
        Vector3 _heading;
        float _side;
        float _reaimInside;
        int _interruptions;
        bool _moving;
        bool _sprinting;
        bool _waiting;

        public override void OnAwake()
        {
            _brain = GetComponent<EnemyBrain>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart()
        {
            _moving = false;
            _waiting = false;
            _side = Random.Range(-1f, 1f);
            _sprinting = _brain.Engaged;
            ApplyPace();
        }

        public override TaskStatus OnUpdate()
        {
            ITargetable target = _brain.Target;
            if (target == null)
                return TaskStatus.Failure;
            MeleeSettings melee = _brain.Definition.Melee;
            Vector3 position = target.PointFor(_brain.transform.position);
            float sqrDistance = MeleeHit.SqrFlatDistance(_brain.transform.position, position);

            float engage = _brain.Engaged ? melee.EngageRadius * Slack : melee.EngageRadius;
            _brain.Engaged = sqrDistance <= engage * engage;
            if (_brain.Engaged != _sprinting)
            {
                _sprinting = _brain.Engaged;
                ApplyPace();
            }

            float hold = _brain.HoldsSlot || _waiting ? melee.HoldDistance * Slack : melee.HoldDistance;
            _waiting = false;
            if (sqrDistance <= hold * hold)
            {
                if (!_brain.TryHoldSlot())
                {
                    _waiting = true;
                    _moving = false;
                    _mover.Face(position);
                    return TaskStatus.Running;
                }
            }
            else
                _brain.ReleaseSlot();

            float rise = Mathf.Abs(position.y - _brain.transform.position.y);
            if (sqrDistance <= melee.AttackRange * melee.AttackRange && rise <= MeleeHit.MaxHeightDifference && _brain.HoldsSlot)
                return TaskStatus.Success;
            float repath = Mathf.Max(melee.RepathDistance, 0.1f * Mathf.Sqrt(sqrDistance));
            bool reaim = _reaimInside > 0f && (sqrDistance < _reaimInside || _mover.Arrived);
            // A stagger halted the walk: set off again.
            bool interrupted = _brain.Interruptions != _interruptions;
            if (!_moving || reaim || interrupted || (position - _heading).sqrMagnitude > repath * repath)
            {
                Vector3 aim = CrowdSpread.FlankPoint(_brain.transform.position, position, _side, melee.FlankAngle,
                    melee.FlankFadeDistance, melee.EngageRadius);
                _mover.MoveTo(aim, float.PositiveInfinity);
                _heading = position;
                // The aim sits beside the target, so it must be renewed on the way in or the chase would stop there.
                _reaimInside = aim != position ? sqrDistance * FlankReaim : 0f;
                _interruptions = _brain.Interruptions;
                _moving = true;
            }
            return TaskStatus.Running;
        }

        public override void OnEnd()
        {
            _mover.Halt();
            _brain.Stats.RemoveModifiers(this);
        }

        void ApplyPace()
        {
            MeleeSettings melee = _brain.Definition.Melee;
            _brain.Stats.RemoveModifiers(this);
            _brain.Stats.AddModifier(new EnemyStatModifier(EnemyStat.MoveSpeed, this,
                multiply: _sprinting ? melee.ChaseSpeedMultiplier : melee.ApproachSpeedMultiplier));
        }
    }

    // Faces the target and swings once (after the cooldown since the last swing); a swing always finishes. The blow lands at
    // HitTime if the target is still within HitReach and HitArc. Succeeds after the swing, or straight away when the target
    // stepped out of range, the slot was lost or it was staggered (the swing is cancelled), so the hunt chases again.
    [Category("Aipocalypse/Melee")]
    public sealed class MeleeAttack : ActionNode
    {
        // Slack on the attack range before a waiting enemy gives up and chases again.
        const float RangeSlack = 1.2f;

        EnemyBrain _brain;
        EnemyMover _mover;
        ITargetable _swingTarget;
        int _interruptions;
        float _swingEnd;
        float _hitAt;
        bool _struck;

        public override void OnAwake()
        {
            _brain = GetComponent<EnemyBrain>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart()
        {
            _swingEnd = 0f;
            _interruptions = _brain.Interruptions;
            _struck = false;
        }

        public override TaskStatus OnUpdate()
        {
            ITargetable target = _brain.Target;
            if (target == null)
                return TaskStatus.Failure;
            MeleeSettings melee = _brain.Definition.Melee;
            // Staggered (mid-swing or not): the swing is cancelled, back to the chase.
            if (_brain.Interruptions != _interruptions)
                return TaskStatus.Success;
            Vector3 position = target.PointFor(_brain.transform.position);
            _mover.Face(position);
            if (_swingEnd > 0f)
            {
                if (!_struck && Time.time >= _hitAt)
                {
                    _struck = true;
                    // Only the target the swing was aimed at: a new one may not have given this enemy a slot.
                    if (ReferenceEquals(target, _swingTarget) && !target.IsDead && MeleeHit.Lands(_brain.transform.position, _brain.transform.forward, position, melee.HitReach, melee.HitArc))
                        _brain.Strike(target);
                }
                return Time.time >= _swingEnd ? TaskStatus.Success : TaskStatus.Running;
            }

            float reach = melee.AttackRange * RangeSlack;
            if (MeleeHit.SqrFlatDistance(_brain.transform.position, position) > reach * reach || !_brain.HoldsSlot)
                return TaskStatus.Success;
            if (Time.time < _brain.NextAttackTime)
                return TaskStatus.Running;
            _swingTarget = target;
            _swingEnd = Time.time + melee.AttackDuration;
            _hitAt = Time.time + melee.HitTime;
            _brain.NextAttackTime = _swingEnd + melee.AttackCooldown;
            if (_brain.Animation != null)
                _brain.Animation.Attack();
            _brain.Voice.Swing();
            return TaskStatus.Running;
        }

        public override void OnEnd() => _mover.Halt();
    }
}
