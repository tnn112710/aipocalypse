using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.Shared.Utility;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // The dog's tree (DogTasks.cs): following the player, catching up, over the fence.

    // ---- Following ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogMustCatchUp : DogCondition
    {
        protected override bool Check() => _dog.State.Order == DogOrder.Follow && _dog.Roster.HasPlayer && _dog.Plan.Mode == FollowMode.CatchUp;
    }

    // Fallen far behind or cut off: appears near the player out of their sight (tried every RetryTime until a spot is
    // found), meanwhile running toward them.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogCatchUp : ActionNode
    {
        const float RetryTime = 0.5f;

        Dog _dog;
        float _nextTry;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _nextTry = 0f;
            _dog.ResetBody();
            _dog.SpeedOverride = -1f;
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            if (now < _nextTry)
                return TaskStatus.Running;
            _nextTry = now + RetryTime;
            if (_dog.Manager.TryFindReappearPoint(_dog, _dog.Plan.Goal, out Vector3 point))
            {
                _dog.SetPose(DogPose.Stand);
                _dog.SetSniffing(false);
                _dog.Legs.Warp(point);
                _dog.Follow.Reset(now);
                return TaskStatus.Success;
            }
            DogWalk.Go(_dog, _dog.Roster.PlayerPosition, DogGait.Sprint);
            return TaskStatus.Running;
        }
    }

    // ---- Over the fence ----

    [Category("Aipocalypse/Dogs")]
    public sealed class DogMustScramble : DogCondition
    {
        protected override bool Check()
        {
            bool wantsPlayer = _dog.State.Order == DogOrder.Follow || _dog.State.Called(Time.time);
            return _dog.Scrambling || (wantsPlayer && _dog.Manager.WantsScramble);
        }
    }

    // Cut off by the shut fence: trots to the best crossing (the shortest way to the player over it), stands squared up a
    // moment, then leaps over (a fence up to LeapHeight; taller: scrambles over) by hand (the body on the arc, the agent
    // waiting) and lands on the far side's navmesh.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogScramble : ActionNode
    {
        const float SquareUp = 12f;

        Dog _dog;
        DogLinkInfo _crossing;
        JumpKind _kind;
        Vector3 _from, _to;
        float _startedAt;
        float _readyAt;
        float _nextGo;
        bool _over;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _over = false;
            _nextGo = 0f;
            _readyAt = -1f;
            DogRoster roster = _dog.Roster;
            Vector3 at = _dog.transform.position;
            _crossing = roster.ScrambleCrossing(at, roster.PlayerPosition, _dog.Manager.HasPathToPlayer);
            if (_crossing == null)
                return;
            bool inside = roster.InYard(at);
            _from = inside ? _crossing.Inside : _crossing.Outside;
            _to = inside ? _crossing.Outside : _crossing.Inside;
            _kind = DogJumpArc.Pick(_to.y - _from.y, _crossing.ObstacleHeight, roster.Settings.LeapHeight);
        }

        public override TaskStatus OnUpdate()
        {
            float now = Time.time;
            if (!_over)
            {
                // Its segment fell or the gate opened on the way there: no fence to climb, it walks through.
                if (_crossing == null || !_crossing.Standing)
                    return TaskStatus.Failure;
                if (!DogWalk.Near(_dog, _from, 0.5f))
                {
                    if (now >= _nextGo)
                    {
                        _nextGo = now + 0.5f;
                        DogWalk.Go(_dog, _from, DogGait.Canter);
                    }
                    return TaskStatus.Running;
                }
                if (!_dog.Standing)
                    return TaskStatus.Running;
                // Square up to the fence first: the leap is drawn facing straight across.
                Vector3 ahead = _to - _dog.transform.position;
                ahead.y = 0f;
                if (ahead.sqrMagnitude > 0.01f && Vector3.Angle(_dog.transform.forward, ahead) > SquareUp)
                {
                    _dog.Legs.FaceInPlace(_to);
                    return TaskStatus.Running;
                }
                _dog.Legs.Halt();
                // Sizing it up: a moment squared up before a leap.
                if (_readyAt < 0f)
                    _readyAt = now + (_kind == JumpKind.Leap ? _dog.Roster.Settings.LeapPause : 0f);
                if (now < _readyAt)
                    return TaskStatus.Running;
                _over = true;
                _dog.Scrambling = true;
                _startedAt = now;
                _dog.PlayJump(DogJumpArc.AnimatorKind(_kind, _to.y - _from.y, _dog.ModelScale));
            }
            Vector3 start = _dog.transform.position;
            float peak = DogJumpArc.Peak(_kind, start, _to, _crossing.ObstacleHeight);
            float duration = DogJumpArc.Duration(_kind, FlatMath.Length(_to - start));
            float k = (now - _startedAt) / duration;
            Vector3 across = _to - start;
            across.y = 0f;
            Quaternion facing = across.sqrMagnitude > 0.001f ? Quaternion.LookRotation(across) : _dog.transform.rotation;
            if (k < 1f)
            {
                _dog.SetBody(DogJumpArc.Position(_kind, start, _to, peak, k) - start, facing);
                return TaskStatus.Running;
            }
            _dog.Legs.Warp(_to);
            _dog.transform.rotation = facing;
            _dog.ResetBody();
            _dog.Scrambling = false;
            return TaskStatus.Success;
        }

        public override void OnEnd()
        {
            if (_dog == null || !_dog.Scrambling)
                return;
            // Cut short mid-air: it lands on the far side at once (never stuck halfway over the fence).
            _dog.Legs.Warp(_to);
            _dog.ResetBody();
            _dog.Scrambling = false;
        }
    }

    [Category("Aipocalypse/Dogs")]
    public sealed class DogIsFollowing : DogCondition
    {
        protected override bool Check() => _dog.State.Order == DogOrder.Follow && _dog.Roster.HasPlayer;
    }

    // Goes where its Plan says (back to the player, ahead, alongside, out of the aim); inside the comfort zone it does dog
    // things: sniffs about, digs or marks a spot, looks round, checks in on the player now and then, and settles (sit,
    // lie, nap) while they stand still.
    [Category("Aipocalypse/Dogs")]
    public sealed class DogFollowPlayer : ActionNode
    {
        enum Free { None, Sniff, Look, CheckIn, Settled }

        Dog _dog;
        bool _started;
        Vector3 _goal;
        float _nextGo;
        Free _free;
        float _freeUntil;
        float _nextCheckIn;
        float _sitAt, _lieAt;
        bool _settleRolled;

        public override void OnAwake() => _dog = GetComponent<Dog>();

        public override void OnStart()
        {
            _started = true;
            _free = Free.None;
            _nextGo = 0f;
            _settleRolled = false;
            DogSettings settings = _dog.Settings;
            _nextCheckIn = Time.time + Random.Range(settings.CheckIn.x, settings.CheckIn.y);
        }

        public override TaskStatus OnUpdate()
        {
            FollowPlan plan = _dog.Plan;
            if (plan.Mode == FollowMode.Free)
                FreeTime();
            else
                Move(plan);
            return TaskStatus.Running;
        }

        void Move(FollowPlan plan)
        {
            float now = Time.time;
            _free = Free.None;
            _settleRolled = false;
            _dog.SetSniffing(false);
            if (now < _nextGo && FlatMath.Length(plan.Goal - _goal) < 0.75f)
                return;
            _nextGo = now + 0.4f;
            _goal = plan.Goal;
            DogWalk.Go(_dog, plan.Goal, plan.Gait == DogGait.Still ? DogGait.Trot : plan.Gait);
        }

        void FreeTime()
        {
            float now = Time.time;
            DogSettings settings = _dog.Settings;
            DogRoster roster = _dog.Roster;
            Vector3 player = roster.PlayerPosition;
            float still = roster.PlayerStillFor;
            // Just out of the water: stop and shake dry before anything else (DogSenses plays it once it stands still).
            if (_dog.ShakePending)
            {
                if (_dog.Pose != DogPose.Stand)
                    _dog.SetPose(DogPose.Stand);
                _dog.Legs.Halt();
                return;
            }
            if (still > 0f)
            {
                if (!_settleRolled)
                {
                    _settleRolled = true;
                    _sitAt = Random.Range(settings.SitAfter.x, settings.SitAfter.y);
                    _lieAt = Random.Range(settings.LieAfter.x, settings.LieAfter.y);
                }
                DogPose pose = DogState.SettlePose(still, settings.NapAfter, _sitAt, _lieAt);
                if (pose != DogPose.Stand)
                {
                    if (_free != Free.Settled || _dog.Pose != pose)
                    {
                        if (_dog.Pose == DogPose.Stand && _dog.Standing && pose == DogPose.Sleep)
                            _dog.Act(DogAct.Yawn);
                        DogWalk.Settle(_dog, pose, player);
                    }
                    _free = Free.Settled;
                    return;
                }
            }
            else
                _settleRolled = false;
            if (_free == Free.Settled)
            {
                _free = Free.None;
                _dog.SetPose(DogPose.Stand);
            }
            if (!_dog.Standing)
                return;

            switch (_free)
            {
                case Free.Sniff when !_dog.Legs.Settled && now < _freeUntil:
                case Free.CheckIn when !_dog.Legs.Settled && now < _freeUntil:
                case Free.Look when now < _freeUntil:
                    return;
                case Free.Sniff:
                    _dog.SetSniffing(false);
                    float roll = Random.value;
                    if (roll < 0.15f)
                        _dog.Act(DogAct.Dig);
                    else if (roll < 0.22f && !roster.InYard(_dog.transform.position))
                        _dog.Act(DogAct.Pee);
                    _free = Free.Look;
                    _freeUntil = now + Random.Range(1.5f, 3f);
                    return;
                case Free.CheckIn:
                    _dog.Legs.Face(player);
                    _dog.Act(DogAct.Happy);
                    _dog.Manager.TryPlay(_dog, DogSound.Pant);
                    _free = Free.Look;
                    _freeUntil = now + 3f;
                    return;
            }

            if (now >= _nextCheckIn && !roster.PlayerAiming)
            {
                _nextCheckIn = now + Random.Range(settings.CheckIn.x, settings.CheckIn.y);
                Vector3 forward = roster.PlayerForward;
                forward.y = 0f;
                if (DogWalk.Go(_dog, player + forward.normalized * settings.CheckInDistance, DogGait.Trot))
                {
                    _free = Free.CheckIn;
                    _freeUntil = now + 6f;
                }
                return;
            }
            if (Random.value < 0.55f && SniffPoint(player, out Vector3 point))
            {
                _dog.SetSniffing(true);
                if (DogWalk.Go(_dog, point, DogGait.Walk))
                {
                    _free = Free.Sniff;
                    _freeUntil = now + 8f;
                    if (Random.value < 0.3f)
                        _dog.Manager.TryPlay(_dog, DogSound.Sniff);
                }
                return;
            }
            _dog.Legs.Halt();
            _dog.Legs.Face(_dog.transform.position + Quaternion.Euler(0f, Random.Range(-120f, 120f), 0f) * _dog.transform.forward * 3f);
            _free = Free.Look;
            _freeUntil = now + Random.Range(2f, 5f);
        }

        // A spot within SniffRadius of the dog that stays inside the comfort zone round the player.
        bool SniffPoint(Vector3 player, out Vector3 point)
        {
            DogSettings settings = _dog.Settings;
            for (int i = 0; i < 4; i++)
            {
                if (!_dog.Legs.TryFindDestination(_dog.transform.position, settings.SniffRadius, 1.5f, 2, out point))
                    continue;
                if (FlatMath.Length(point - player) < settings.OuterRadius - 1f)
                    return true;
            }
            point = default;
            return false;
        }

        // Once per run: a second call would halt whatever moves it next.
        public override void OnEnd()
        {
            if (!_started || _dog == null)
                return;
            _started = false;
            _dog.SetSniffing(false);
            _dog.Legs?.Halt();
        }
    }
}
