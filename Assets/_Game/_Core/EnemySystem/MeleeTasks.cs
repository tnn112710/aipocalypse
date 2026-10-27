using Aipocalypse.Core.Entities;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using Opsive.Shared.Utility;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // Tasks of the shared Melee subtree. Tuning comes from the enemy's definition (EnemyDefinition.Melee); the engaged
    // player is EnemyBrain.Target.
    [Category("Aipocalypse/Melee")]
    public sealed class PlayerInReach : ConditionalNode
    {
        EnemyBrain _brain;

        public override void OnAwake() => _brain = GetComponent<EnemyBrain>();

        // Also the abort check: re-run every think while the fight or the wander below it runs.
        public override TaskStatus OnUpdate()
        {
            MeleeSettings melee = _brain.Definition.Melee;
            IPlayerTarget player = _brain.Player;
            bool inReach = melee != null && player != null && !player.IsDead
                && melee.InReach(_brain.Target != null, MeleeMath.SqrFlatDistance(_brain.transform.position, player.Transform.position));
            _brain.Target = inReach ? player : null;
            return inReach ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    // Runs at the target, faster than it walks, until within attack range.
    [Category("Aipocalypse/Melee")]
    public sealed class ChaseTarget : ActionNode
    {
        EnemyBrain _brain;
        EnemyMover _mover;
        Vector3 _heading;
        bool _moving;

        public override void OnAwake()
        {
            _brain = GetComponent<EnemyBrain>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart()
        {
            _moving = false;
            _brain.Stats.AddModifier(new EnemyStatModifier(EnemyStat.MoveSpeed, this, multiply: _brain.Definition.Melee.ChaseSpeedMultiplier));
        }

        public override TaskStatus OnUpdate()
        {
            IPlayerTarget target = _brain.Target;
            if (target == null)
                return TaskStatus.Failure;
            MeleeSettings melee = _brain.Definition.Melee;
            Vector3 position = target.Transform.position;
            if (MeleeMath.SqrFlatDistance(_brain.transform.position, position) <= melee.AttackRange * melee.AttackRange)
                return TaskStatus.Success;
            if (!_moving || (position - _heading).sqrMagnitude > melee.RepathDistance * melee.RepathDistance)
            {
                _mover.MoveTo(position, float.PositiveInfinity);
                _heading = position;
                _moving = true;
            }
            return TaskStatus.Running;
        }

        public override void OnEnd()
        {
            _mover.Halt();
            _brain.Stats.RemoveModifiers(this);
        }
    }

    // Faces the target and swings once (after the cooldown since the last swing); a swing always finishes. Succeeds after
    // the swing, or straight away when the target stepped out of range so the fight chases again.
    [Category("Aipocalypse/Melee")]
    public sealed class MeleeAttack : ActionNode
    {
        // Slack on the attack range before a waiting enemy gives up and chases again.
        const float RangeSlack = 1.2f;

        EnemyBrain _brain;
        EnemyMover _mover;
        float _swingEnd;

        public override void OnAwake()
        {
            _brain = GetComponent<EnemyBrain>();
            _mover = GetComponent<EnemyMover>();
        }

        public override void OnStart() => _swingEnd = 0f;

        public override TaskStatus OnUpdate()
        {
            IPlayerTarget target = _brain.Target;
            if (target == null)
                return TaskStatus.Failure;
            Vector3 position = target.Transform.position;
            _mover.Face(position);
            if (_swingEnd > 0f)
                return Time.time >= _swingEnd ? TaskStatus.Success : TaskStatus.Running;

            MeleeSettings melee = _brain.Definition.Melee;
            float reach = melee.AttackRange * RangeSlack;
            if (MeleeMath.SqrFlatDistance(_brain.transform.position, position) > reach * reach)
                return TaskStatus.Success;
            if (Time.time < _brain.NextAttackTime)
                return TaskStatus.Running;
            _swingEnd = Time.time + melee.AttackDuration;
            _brain.NextAttackTime = _swingEnd + melee.AttackCooldown;
            if (_brain.Animation != null)
                _brain.Animation.Attack();
            return TaskStatus.Running;
        }

        public override void OnEnd() => _mover.Halt();
    }

    static class MeleeMath
    {
        public static float SqrFlatDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return x * x + z * z;
        }
    }
}
