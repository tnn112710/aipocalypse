using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    public enum FollowMode
    {
        // Inside the comfort zone: it does its own thing (sniff, settle, check in).
        Free,
        Return,
        Scout,
        Sprint,
        Alongside,
        ClearAim,
        CatchUp,
    }

    public readonly struct FollowInput
    {
        public readonly Vector3 Player, PlayerVelocity, PlayerForward;
        public readonly bool PlayerSprinting, PlayerAiming;
        public readonly Vector3 CameraPosition, CameraForward, Dog;
        public readonly bool HasPath;
        public readonly float Now;
        // A fence (or a wall) between them: one is in the yard, the other not. Close or not, it comes back.
        public readonly bool Separated;

        public FollowInput(Vector3 player, Vector3 playerVelocity, Vector3 playerForward, bool playerSprinting,
            bool playerAiming, Vector3 cameraPosition, Vector3 cameraForward, Vector3 dog, bool hasPath, float now,
            bool separated = false)
        {
            Separated = separated;
            Player = player;
            PlayerVelocity = playerVelocity;
            PlayerForward = playerForward;
            PlayerSprinting = playerSprinting;
            PlayerAiming = playerAiming;
            CameraPosition = cameraPosition;
            CameraForward = cameraForward;
            Dog = dog;
            HasPath = hasPath;
            Now = now;
        }
    }

    public readonly struct FollowPlan
    {
        public readonly FollowMode Mode;
        public readonly Vector3 Goal;
        public readonly DogGait Gait;

        public FollowPlan(FollowMode mode, Vector3 goal, DogGait gait)
        {
            Mode = mode;
            Goal = goal;
            Gait = gait;
        }
    }

    // Where the dog wants to be relative to the player, like a dog: a comfort zone it's free inside, a lag before it
    // reacts to the player starting or stopping, scouting ahead on a steady walk, a side it keeps, out of the aim.
    public sealed class DogFollow
    {
        const float SteadySpeed = 0.8f;
        const float SteadyTurn = 25f;
        const float ReturnSlack = 1f;
        const float ScoutSideOffset = 1f;
        const float AlongsideOffset = 1.5f;
        const float AimSightRange = 40f;
        // With the player walking and its goal farther than this, a canter barely gains on them: it sprints.
        const float CatchUpGap = 8f;

        readonly DogSettings _settings;
        readonly System.Random _random;
        FollowMode _mode;
        FollowMode _pending;
        float _pendingAt = float.NaN;
        float _steadySince = float.NaN;
        Vector3 _steadyHeading;
        float _noPathSince = float.NaN;
        float _sideSwapAt;
        float _lead;
        float _reappear;
        bool _sideChosen;

        // +1: the player's right, −1: their left.
        public int Side { get; private set; } = 1;
        public FollowMode Mode => _mode;

        public DogFollow(DogSettings settings, int seed)
        {
            _settings = settings;
            _random = new System.Random(seed);
        }

        public void Reset(float now)
        {
            _mode = FollowMode.Free;
            _pendingAt = float.NaN;
            _steadySince = float.NaN;
            _noPathSince = float.NaN;
            _sideChosen = false;
            _sideSwapAt = now + Roll(_settings.SideSwap);
        }

        public FollowPlan Plan(in FollowInput input)
        {
            Vector3 forward = Flat(input.PlayerForward);
            if (forward == Vector3.zero)
                forward = Vector3.forward;
            Vector3 right = new(forward.z, 0f, -forward.x);
            if (!_sideChosen)
            {
                Side = Vector3.Dot(Flat(input.Dog - input.Player), right) < 0f ? -1 : 1;
                _sideChosen = true;
            }
            if (input.Now >= _sideSwapAt)
            {
                Side = -Side;
                _sideSwapAt = input.Now + Roll(_settings.SideSwap);
            }

            UpdateSteady(input);
            UpdatePath(input);
            FollowMode raw = Raw(input);
            Advance(raw, input.Now);
            Vector3 goal = Goal(input, forward, right);
            return new FollowPlan(_mode, goal, Gait(_mode, Flat(goal - input.Dog).magnitude, Flat(input.PlayerVelocity).magnitude));
        }

        // The nearest point outside the cone round the camera's (flat) forward, `clearance` further out.
        public static Vector3 ClearOfAim(Vector3 dog, Vector3 cameraPosition, Vector3 cameraForward, float coneDegrees,
            float clearance, int side = 1)
        {
            Vector3 forward = Flat(cameraForward).normalized;
            Vector3 offset = Flat(dog - cameraPosition);
            float along = Vector3.Dot(offset, forward);
            Vector3 lateral = offset - forward * along;
            Vector3 lateralDir = lateral.sqrMagnitude > 1e-6f ? lateral.normalized : new Vector3(forward.z, 0f, -forward.x) * side;
            float needed = Mathf.Tan(coneDegrees * Mathf.Deg2Rad) * Mathf.Max(0f, along) + clearance;
            Vector3 flat = new Vector3(cameraPosition.x, 0f, cameraPosition.z) + forward * along + lateralDir * Mathf.Max(needed, lateral.magnitude);
            return new Vector3(flat.x, dog.y, flat.z);
        }

        FollowMode Raw(in FollowInput input)
        {
            float distance = Flat(input.Dog - input.Player).magnitude;
            if (distance > _settings.CatchUpRadius || NoPath(input.Now))
                return FollowMode.CatchUp;
            if (input.PlayerSprinting)
                return FollowMode.Alongside;
            if (input.PlayerAiming && InAim(input))
                return FollowMode.ClearAim;
            if (distance > _settings.SprintRadius)
                return FollowMode.Sprint;
            bool returning = _mode is FollowMode.Return or FollowMode.Sprint or FollowMode.CatchUp;
            if (distance > _settings.OuterRadius || input.Separated || (returning && distance > _settings.InnerRadius + ReturnSlack))
                return FollowMode.Return;
            if (Steady(input.Now))
                return FollowMode.Scout;
            return FollowMode.Free;
        }

        // Changes of mode wait a reaction delay (never the same frame the player starts or stops); catching up and getting
        // out of the line of fire don't.
        void Advance(FollowMode raw, float now)
        {
            if (raw is FollowMode.CatchUp or FollowMode.ClearAim && _mode != raw)
            {
                Enter(raw);
                return;
            }
            if (raw == _mode)
            {
                _pendingAt = float.NaN;
                return;
            }
            if (float.IsNaN(_pendingAt))
                _pendingAt = now + Roll(_settings.ReactionDelay);
            _pending = raw;
            if (now >= _pendingAt)
                Enter(_pending);
        }

        void Enter(FollowMode mode)
        {
            _mode = mode;
            _pendingAt = float.NaN;
            if (mode == FollowMode.Scout)
                _lead = Roll(_settings.Lead);
            else if (mode == FollowMode.CatchUp)
                _reappear = Roll(new Vector2(_settings.ReappearMin, _settings.ReappearMax));
        }

        Vector3 Goal(in FollowInput input, Vector3 forward, Vector3 right)
        {
            Vector3 player = input.Player;
            return _mode switch
            {
                FollowMode.Return or FollowMode.Sprint => player + right * (Side * _settings.InnerRadius),
                FollowMode.Scout => player + _steadyHeading * _lead + right * (Side * ScoutSideOffset),
                FollowMode.Alongside => player + right * (Side * AlongsideOffset) + forward,
                FollowMode.ClearAim => ClearOfAim(input.Dog, input.CameraPosition, input.CameraForward,
                    _settings.AimCone, _settings.AimClearance, Side),
                FollowMode.CatchUp => player - forward * _reappear,
                _ => input.Dog,
            };
        }

        // Moving with the player, it never goes slower than they walk (a trot is slower than the player's walk).
        DogGait Gait(FollowMode mode, float distance, float playerSpeed)
        {
            if (mode is FollowMode.Sprint or FollowMode.Alongside or FollowMode.CatchUp)
                return DogGait.Sprint;
            if (mode == FollowMode.Free)
                return DogGait.Still;
            if (mode == FollowMode.ClearAim)
                return distance > CatchUpGap ? DogGait.Sprint : DogGait.Canter;
            if (playerSpeed > _settings.TrotSpeed * 0.9f)
                return distance > CatchUpGap ? DogGait.Sprint : DogGait.Canter;
            if (distance < 2f)
                return DogGait.Walk;
            return distance < 10f ? DogGait.Trot : DogGait.Canter;
        }

        bool InAim(in FollowInput input)
        {
            Vector3 forward = Flat(input.CameraForward).normalized;
            Vector3 offset = Flat(input.Dog - input.CameraPosition);
            float along = Vector3.Dot(offset, forward);
            if (along <= 0f || along > AimSightRange)
                return false;
            float lateral = (offset - forward * along).magnitude;
            return lateral < Mathf.Tan(_settings.AimCone * Mathf.Deg2Rad) * along + _settings.AimClearance;
        }

        void UpdateSteady(in FollowInput input)
        {
            Vector3 velocity = Flat(input.PlayerVelocity);
            if (velocity.magnitude < SteadySpeed)
            {
                _steadySince = float.NaN;
                return;
            }
            Vector3 heading = velocity.normalized;
            if (float.IsNaN(_steadySince) || Vector3.Angle(heading, _steadyHeading) > SteadyTurn)
            {
                _steadySince = input.Now;
                _steadyHeading = heading;
            }
        }

        bool Steady(float now) => !float.IsNaN(_steadySince) && now - _steadySince >= _settings.SteadyTime;

        void UpdatePath(in FollowInput input)
        {
            if (input.HasPath)
                _noPathSince = float.NaN;
            else if (float.IsNaN(_noPathSince))
                _noPathSince = input.Now;
        }

        bool NoPath(float now) => !float.IsNaN(_noPathSince) && now - _noPathSince >= _settings.NoPathTime;

        float Roll(Vector2 range) => range.x + (range.y - range.x) * (float)_random.NextDouble();

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
