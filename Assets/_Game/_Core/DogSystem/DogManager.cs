using System;
using System.Collections.Generic;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.SpawnSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using Pathfinding;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // Runs the dog on scaled time: reads the player (where they are and look, how fast, aiming, sprinting, standing still)
    // into the roster, plans where a following dog wants to be, swims it in deep water, tilts its head when looked at,
    // opens its command wheel, answers whistles (also as the plain-whistle fallback), limits its noise, thinks its tree.
    public sealed class DogManager : ITickable, IDisposable, IWhistleFallback
    {
        const float PathCheckInterval = 0.5f;
        const float LookAngle = 6f;
        // How far up the body the water may come before it swims (the model floats with its back at the surface).
        const float SwimBodyDepth = 0.55f;
        const float StillSpeed = 0.2f;
        const float ShakeWindow = 20f;
        const float FloorStep = 0.6f;

        readonly DogRoster _roster;
        readonly DogSettings _settings;
        readonly WorldClock _clock;
        readonly PlayerRegistry _players;
        readonly AudioService _audio;
        readonly CommandWheelPresenter _wheel;
        readonly IWaterLevel _water;
        readonly DogVoice _voice;
        readonly IDisposable _subscriptions;
        readonly List<WheelSlice> _slices = new();
        Vector3 _lastPlayerPosition;
        bool _hadPlayer;
        float _lookedFor;
        bool _hasPath = true;
        float _nextPathCheck;
        float _shakeUntil = float.NegativeInfinity;

        public DogManager(DogRoster roster, DogSettings settings, WorldClock clock, PlayerRegistry players, AudioService audio,
            CommandWheelPresenter wheel, IObjectResolver resolver, ISubscriber<PlayerRevived> revived, ISubscriber<TimeSkipEnded> skipEnded,
            ISubscriber<RaidStarted> raidStarted, ISubscriber<RaidEnded> raidEnded)
        {
            _roster = roster;
            _settings = settings;
            _clock = clock;
            _players = players;
            _audio = audio;
            _wheel = wheel;
            _voice = new DogVoice(settings);
            resolver.TryResolve(out _water);
            _subscriptions = DisposableBag.Create(
                revived.Subscribe(_ => SendHome(DogOrder.Follow)),
                skipEnded.Subscribe(_ => AfterSleep()),
                raidStarted.Subscribe(_ => _roster.Raid = true),
                raidEnded.Subscribe(_ => _roster.Raid = false));
        }

        public DogSettings Settings => _settings;
        public bool Enabled => _settings != null && _settings.Enabled;

        public void Dispose()
        {
            _subscriptions.Dispose();
            if (_wheel.Owner == this)
                _wheel.Close();
        }

        public void Tick()
        {
            Dog dog = _roster.Dog;
            if (!Enabled || dog == null || !dog.Active)
                return;
            float now = Time.time;
            float dt = Time.deltaTime;
            _roster.Night = !_settings.Awake.Contains(_clock.Now.Hour);
            IPlayer player = _players.Current;
            ReadPlayer(player, dt);
            if (dog.State.Order == DogOrder.Stay && !_roster.InYard(dog.State.StayPoint) && _roster.House != null
                && (_roster.Night || (_roster.HasPlayer && Flat(dog.State.StayPoint - _roster.PlayerPosition) > _settings.StayGoHomeDistance)))
                dog.State.SetOrder(DogOrder.Stay, _roster.House.Door);
            Swim(dog, now);
            LookedAt(dog, player, now, dt);
            dog.Advance(dt);
            if (now < dog.NextThinkAt)
                return;
            dog.NextThinkAt = now + _settings.ThinkInterval;
            if (_roster.HasPlayer && dog.State.Order == DogOrder.Follow)
                dog.Plan = dog.Follow.Plan(FollowInput(dog, player, now));
            dog.Think();
        }

        void ReadPlayer(IPlayer player, float dt)
        {
            bool has = player != null && !player.IsDead && !player.InSequence;
            _roster.HasPlayer = has;
            if (!has)
            {
                _hadPlayer = false;
                return;
            }
            Vector3 position = player.Transform.position;
            Vector3 velocity = _hadPlayer && dt > 0f ? (position - _lastPlayerPosition) / dt : Vector3.zero;
            // A teleport (respawn, a cheat) isn't a run.
            if (velocity.sqrMagnitude > 400f)
                velocity = Vector3.zero;
            _lastPlayerPosition = position;
            _hadPlayer = true;
            _roster.PlayerPosition = position;
            _roster.PlayerForward = player.View.forward;
            _roster.PlayerVelocity = Vector3.Lerp(_roster.PlayerVelocity, velocity, Mathf.Clamp01(dt * 8f));
            _roster.PlayerStillFor = Flat(_roster.PlayerVelocity) < StillSpeed ? _roster.PlayerStillFor + dt : 0f;
        }

        FollowInput FollowInput(Dog dog, IPlayer player, float now)
        {
            if (now >= _nextPathCheck)
            {
                _nextPathCheck = now + PathCheckInterval;
                _hasPath = HasPath(dog.transform.position, _roster.PlayerPosition);
            }
            bool aiming = player.HeldItem != null && player.HeldItem.IsAiming;
            return new FollowInput(_roster.PlayerPosition, _roster.PlayerVelocity, _roster.PlayerForward, player.IsSprinting, aiming,
                player.View.position, player.View.forward, dog.transform.position, _hasPath, now, Separated(dog.transform.position));
        }

        // A wall between them: one in the yard and the other not (the fence), or on another floor (the house is 1 m up).
        bool Separated(Vector3 dog)
        {
            Vector3 player = _roster.PlayerPosition;
            return Mathf.Abs(dog.y - player.y) > FloorStep || (_roster.Yard != null && _roster.InYard(dog) != _roster.InYard(player));
        }

        static bool HasPath(Vector3 from, Vector3 to)
        {
            AstarPath navigation = AstarPath.active;
            if (navigation == null)
                return true;
            GraphNode a = navigation.GetNearest(from, NNConstraint.Walkable).node;
            GraphNode b = navigation.GetNearest(to, NNConstraint.Walkable).node;
            return a == null || b == null || PathUtilities.IsPathPossible(a, b);
        }

        // Deep water: it swims, the model floating with its back at the surface; out again, it shakes itself dry soon after.
        void Swim(Dog dog, float now)
        {
            Vector3 at = dog.transform.position;
            bool deep = _water != null && _water.TryGetLevel(at, out float level) && level - at.y > _settings.SwimDepth;
            if (deep)
            {
                _water.TryGetLevel(at, out level);
                dog.TargetLift = Mathf.Max(0f, level - at.y - SwimBodyDepth);
            }
            if (deep != dog.Swimming)
            {
                dog.SetSwimming(deep);
                dog.ShakePending = !deep;
                if (!deep)
                    _shakeUntil = now + ShakeWindow;
            }
            if (!dog.ShakePending)
                return;
            if (now >= _shakeUntil)
                dog.ShakePending = false;
            else if (dog.Standing && dog.Pose == DogPose.Stand && Flat(dog.Legs.Velocity) < 0.5f)
            {
                dog.ShakePending = false;
                dog.Act(DogAct.ShakeWater);
            }
        }

        // Looked at for LookTime within LookRange: a head tilt (at most every HeadTiltGap), when it's standing about.
        void LookedAt(Dog dog, IPlayer player, float now, float dt)
        {
            if (!_roster.HasPlayer)
            {
                _lookedFor = 0f;
                return;
            }
            Vector3 eye = player.View.position;
            Vector3 toDog = dog.transform.position + Vector3.up * 0.5f - eye;
            bool looking = toDog.sqrMagnitude <= _settings.LookRange * _settings.LookRange
                && (ReferenceEquals(player.Interactor?.Focus, dog) || Vector3.Angle(player.View.forward, toDog) < LookAngle);
            _lookedFor = looking ? _lookedFor + dt : 0f;
            if (_lookedFor < _settings.LookTime || !dog.Standing || Flat(dog.Legs.Velocity) > 0.3f || !dog.State.TryHeadTilt(now))
                return;
            dog.Legs.Face(_roster.PlayerPosition);
            dog.Act(DogAct.HeadTilt);
        }

        // ---- Orders ----

        public void OpenWheel(Dog dog)
        {
            DogState state = dog.State;
            _slices.Clear();
            _slices.Add(new WheelSlice(_settings.FollowLabel.Text(), null, true, state.Order == DogOrder.Follow, () => Order(dog, DogOrder.Follow)));
            _slices.Add(new WheelSlice(_settings.StayLabel.Text(), null, true, state.Order == DogOrder.Stay, () => Order(dog, DogOrder.Stay)));
            _slices.Add(new WheelSlice(_settings.PetLabel.Text(), null, true, false, () => Pet(dog)));
            _wheel.TryOpen(this, _slices, _settings.WheelTimeScale);
        }

        public void Order(Dog dog, DogOrder order)
        {
            float now = Time.time;
            dog.State.EndCall();
            dog.State.SetOrder(order, dog.transform.position, now);
            if (order == DogOrder.Follow)
            {
                dog.Follow.Reset(now);
                if (TryPlay(dog, DogSound.Bark))
                    dog.Act(DogAct.Happy);
            }
            else
                TryPlay(dog, DogSound.Whine);
        }

        public void Pet(Dog dog)
        {
            dog.State.Pet(Time.time);
            TryPlay(dog, DogSound.Pant);
        }

        // Answered a whistle (aimed at it or the plain fallback): it comes to heel, following again; annoyed, it comes
        // grumbling; still annoyed, it only shakes its head.
        public bool Whistled(Dog dog, GameObject actor)
        {
            float now = Time.time;
            switch (dog.State.Whistle(now, inFight: false))
            {
                case DogWhistle.Ignored:
                    if (dog.Standing)
                        dog.Act(DogAct.No);
                    return true;
                case DogWhistle.Annoyed:
                    dog.State.Call(now, annoyed: true);
                    break;
                default:
                    dog.State.Call(now, annoyed: false);
                    TryPlay(dog, DogSound.Bark);
                    break;
            }
            if (dog.State.Order != DogOrder.Follow)
                dog.State.SetOrder(DogOrder.Follow, dog.transform.position);
            dog.Follow.Reset(now);
            return true;
        }

        public bool TryAnswer(GameObject actor, Vector3 eye, Vector3 aim, out IWhistleTarget target)
        {
            target = null;
            Dog dog = _roster.Dog;
            if (!Enabled || dog == null || !dog.Active || actor == null)
                return false;
            if (Flat(dog.transform.position - actor.transform.position) > _settings.HearWhistleRange)
                return false;
            target = dog;
            return true;
        }

        // Shot or hit: the player's own shots only make it yelp (it never takes damage in Phase 1).
        public void Hurt(Dog dog, in DamageInfo info)
        {
            IPlayer player = _players.Current;
            if (info.Source != null && player != null && info.Source.transform.IsChildOf(player.Transform))
                TryPlay(dog, DogSound.Yelp);
        }

        public bool TryPlay(Dog dog, DogSound sound)
        {
            if (!_voice.TryPlay(sound, Time.time))
                return false;
            if (_audio != null && _settings.Sounds.TryGetValue(sound, out SoundID id) && id.IsValid())
                _audio.PlayAt(id, dog.transform.position + Vector3.up * 0.6f);
            return true;
        }

        // ---- Coming home ----

        // After a respawn or a night's sleep: back at its house with the given order.
        public void SendHome(DogOrder order)
        {
            Dog dog = _roster.Dog;
            if (dog == null || !dog.Active)
                return;
            dog.SpeedOverride = -1f;
            dog.ResetBody();
            dog.Legs.Warp(_roster.Home(dog));
            dog.State.EndCall();
            dog.State.SetOrder(order, _roster.Home(dog));
            dog.SetPose(DogPose.Stand);
            dog.Follow.Reset(Time.time);
        }

        void AfterSleep()
        {
            Dog dog = _roster.Dog;
            if (dog != null && dog.Active && dog.State.Order == DogOrder.Follow)
                SendHome(DogOrder.Follow);
        }

        // A point out of the player's sight near `goal` (behind them), to appear at when it has fallen far behind.
        public bool TryFindReappearPoint(Dog dog, Vector3 goal, out Vector3 point)
        {
            IPlayer player = _players.Current;
            for (int i = 0; i < 6; i++)
            {
                if (!dog.Legs.TryFindDestination(goal, 3f + i, 0f, 2, out point))
                    continue;
                if (player == null || !Visible(player.View, point))
                    return true;
            }
            point = default;
            return false;
        }

        static bool Visible(Transform view, Vector3 point)
        {
            Vector3 to = point + Vector3.up * 0.4f - view.position;
            if (Vector3.Angle(view.forward, to) > 70f)
                return false;
            return !Physics.Raycast(view.position, to.normalized, to.magnitude - 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
