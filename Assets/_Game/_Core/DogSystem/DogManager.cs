using System;
using System.Collections.Generic;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.AnimalSystem;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.RobotSystem;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.SleepSystem;
using MB13.Aipocalypse.Core.TimeSystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.RandomSystem;
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
    // into the roster, plans where a following dog wants to be, tilts its head when looked at, opens its command wheel,
    // answers whistles (also as the plain-whistle fallback), limits its noise, thinks its tree. What it senses of the world
    // (the fence, water, rain, the bed) is DogSenses and the fight DogFight, both ticked from here.
    public sealed class DogManager : ITickable, IDisposable, IWhistleFallback
    {
        const float LookAngle = 6f;
        const float StillSpeed = 0.2f;

        readonly DogRoster _roster;
        readonly DogSettings _settings;
        readonly WorldClock _clock;
        readonly PlayerRegistry _players;
        readonly AudioService _audio;
        readonly CommandWheelPresenter _wheel;
        readonly DogVoice _voice;
        readonly RandomStream _random;
        readonly DogFight _fight;
        readonly DogSenses _senses;
        bool _wasDown;
        readonly SicOrder _sic;
        readonly AnimalHerd _herd;
        readonly RobotRoster _robots;
        readonly IDisposable _subscriptions;
        readonly List<WheelSlice> _slices = new();
        Vector3 _lastPlayerPosition;
        bool _hadPlayer;
        float _lookedFor;
        float _stillSince = float.NaN;
        float _nextIdleAct;

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
            _sic = new SicOrder(this);
            Stats = new DogStatSheet(settings.Stats, settings.Classes != null ? settings.Classes.Length : 1);
            _random = resolver.TryResolve(out RandomService random) ? random.Stream("Dog") : new RandomStream(7);
            resolver.TryResolve(out EnemyRegistry enemies);
            resolver.TryResolve(out _herd);
            resolver.TryResolve(out _robots);
            resolver.TryResolve(out DamageService damage);
            resolver.TryResolve(out TimeService time);
            resolver.TryResolve(out IWaterLevel water);
            resolver.TryResolve(out WeatherService weather);
            resolver.TryResolve(out SleepService sleep);
            _fight = new DogFight(this, roster, settings, enemies, damage, time);
            _senses = new DogSenses(roster, settings, water, weather, sleep);
            _subscriptions = DisposableBag.Create(
                revived.Subscribe(_ => SendHome(DogOrder.Follow)),
                skipEnded.Subscribe(_ => AfterSleep()),
                raidStarted.Subscribe(_ => _roster.Raid = true),
                raidEnded.Subscribe(_ => _roster.Raid = false));
        }

        public DogSettings Settings => _settings;
        public WorldClock Clock => _clock;
        // Its fighting stats (base + upgrades; a levelling system adds an IDogUpgradeSource).
        public DogStatSheet Stats { get; }

        // A gameplay roll (0..1) on the Dog stream (dodges, takedowns).
        public float Roll() => _random.Value;

        public bool IsPlayer(GameObject source)
        {
            IPlayer player = _players.Current;
            return source != null && player != null && source.transform.IsChildOf(player.Transform);
        }
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
                && (_roster.Night || (_roster.HasPlayer && FlatMath.Length(dog.State.StayPoint - _roster.PlayerPosition) > _settings.StayGoHomeDistance)))
                dog.State.SetOrder(DogOrder.Stay, _roster.House.Door);
            _senses.CheckPath(dog, now);
            dog.State.Tick(dt, now, Stats);
            dog.SetInjured(dog.State.Limping(now));
            if (!_wasDown && dog.State.Down)
            {
                dog.Escaping = false;
                dog.SetPin(false);
                dog.ResetBody();
                dog.KnockDown(dog.LastHitLeft ? 0 : 1);
            }
            else if (_wasDown && !dog.State.Down)
                dog.Rise();
            _wasDown = dog.State.Down;
            _senses.Fenced(dog, dt);
            _fight.RaidBarks(dog, now);
            GreetRobots(dog, now);
            if (_roster.HasPlayer)
                dog.State.TrackPlayer(_roster.InYard(_roster.PlayerPosition), dog.State.Order == DogOrder.Stay && AtHome(dog) && !_roster.Raid, now);
            _senses.Rain(dog, now, dt);
            _senses.Swim(dog, now);
            LookedAt(dog, player, now, dt);
            IdleActs(dog, now);
            dog.Advance(dt);
            if (now < dog.NextThinkAt)
                return;
            dog.NextThinkAt = now + _settings.ThinkInterval;
            if (_roster.HasPlayer && dog.State.Order == DogOrder.Follow)
                dog.Plan = dog.Follow.Plan(FollowInput(dog, player, now));
            _fight.See(dog, player, now);
            dog.Think();
            // A bite asked for by the tree lands here, after its tick: a kill despawns the enemy's agent, a structural ECS
            // change Behavior Designer's own systems forbid while they run.
            _fight.LandBite(dog);
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
            _roster.PlayerStillFor = FlatMath.Length(_roster.PlayerVelocity) < StillSpeed ? _roster.PlayerStillFor + dt : 0f;
        }

        FollowInput FollowInput(Dog dog, IPlayer player, float now)
        {
            bool aiming = player.HeldItem != null && player.HeldItem.IsAiming;
            _roster.PlayerAiming = aiming;
            return new FollowInput(_roster.PlayerPosition, _roster.PlayerVelocity, _roster.PlayerForward, player.IsSprinting, aiming,
                player.View.position, player.View.forward, dog.transform.position, _senses.HasPathToPlayer, now, _senses.Separated(dog.transform.position));
        }

        // ---- The world (DogSenses) ----

        // Whether it can walk to the player (checked every PathCheckInterval); false across the shut fence or into an island.
        public bool HasPathToPlayer => _senses.HasPathToPlayer;

        // The fence between it and the player: it scrambles over (DogScramble; when: DogSenses.WantsScramble).
        public bool WantsScramble => _senses.WantsScramble;

        // The player at the bed: a floor spot beside it for a following dog; `lyingDown` once they lie down.
        public bool TryGetBedSpot(out Vector3 spot, out Vector3 bed, out bool lyingDown) => _senses.TryGetBedSpot(out spot, out bed, out lyingDown);

        // Looked at for LookTime within LookRange: a head tilt (at most every HeadTiltGap), when it's standing about.
        void LookedAt(Dog dog, IPlayer player, float now, float dt)
        {
            if (!_roster.HasPlayer)
            {
                _lookedFor = 0f;
                return;
            }
            Vector3 eye = player.View.position;
            Vector3 toDog = dog.transform.position + Vector3.up * 1f - eye;
            bool looking = toDog.sqrMagnitude <= _settings.LookRange * _settings.LookRange
                && (ReferenceEquals(player.Interactor?.Focus, dog) || Vector3.Angle(player.View.forward, toDog) < LookAngle);
            _lookedFor = looking ? _lookedFor + dt : 0f;
            if (_lookedFor < _settings.LookTime || !dog.Standing || dog.Pose != DogPose.Stand || FlatMath.Length(dog.Legs.Velocity) > 0.3f
                || !dog.State.TryHeadTilt(now))
                return;
            dog.Legs.Face(_roster.PlayerPosition);
            dog.Act(DogAct.HeadTilt);
        }

        // Standing about (on its feet, still, nothing going on) for IdleActAfter: one of IdleActs every IdleActGap.
        void IdleActs(Dog dog, float now)
        {
            bool still = dog.Standing && dog.Pose == DogPose.Stand && FlatMath.Length(dog.Legs.Velocity) < 0.1f && !dog.Swimming && !dog.InHouse
                && !dog.Scrambling && !dog.State.Down && !InFight && !_roster.Raid && !dog.State.Called(now) && !dog.State.Petting(now)
                && !dog.State.Welcoming(now);
            if (!still)
            {
                _stillSince = float.NaN;
                return;
            }
            if (float.IsNaN(_stillSince))
            {
                _stillSince = now;
                _nextIdleAct = now + _settings.IdleActAfter;
                return;
            }
            if (now < _nextIdleAct)
                return;
            _nextIdleAct = now + Mathf.Lerp(_settings.IdleActGap.x, _settings.IdleActGap.y, Roll());
            if (_settings.IdleActs.TryPick(Roll(), out DogAct act))
                dog.Act(act);
        }

        // ---- The fight (DogFight) ----

        // The enemy it's going for (−1: none) and one it should growl a warning at (−1: none), set each think.
        public int TargetId => _fight.TargetId;
        public int WarnId => _fight.WarnId;
        public bool InFight => _fight.InFight;
        public bool PlayerHurt => _fight.PlayerHurt;
        public IReadOnlyList<FoeView> Foes => _fight.Foes;

        public bool TryGetFoe(int id, out FoeView foe) => _fight.TryGetFoe(id, out foe);

        // A warning under way: the warn branch holds until it's done.
        public bool Warning => _fight.Warning;
        public void Warned(float now) => _fight.Warned(now);
        public void WarningDone() => _fight.WarningDone();

        // The skirmish's bite (dealt after the tree's tick; Dog.BiteLanded reports it).
        public void RequestBite(Dog dog, int foeId) => _fight.RequestBite(dog, foeId);
        public bool Bite(Dog dog, int foeId) => _fight.Bite(dog, foeId);
        public void Provoke(Dog dog, Vector3 dogAt, float now) => _fight.Provoke(dog, dogAt, now);

        // Raiders near a Passive dog at home: it keeps away from them (never bites).
        public bool RaiderClose(Dog dog, out Vector3 raider) => _fight.RaiderClose(dog, out raider);

        // The enemy nearest the dog among those it sees (any distance).
        public bool NearestFoe(Dog dog, out Vector3 at) => _fight.NearestFoe(dog, out at);

        // ---- The farm ----

        public AnimalHerd Herd => _herd;
        public int AnimalsOut => _herd == null ? 0 : _herd.Animals.Count - _herd.CountHome();
        public bool AtHome(Dog dog) => _roster.InYard(dog.transform.position);

        // A free robot close by greets the dog (it waves, heart eyes); the dog sniffs it (DogHomeLife picks it up).
        void GreetRobots(Dog dog, float now)
        {
            if (_robots == null || !dog.InHomeLife || dog.State.Order == DogOrder.Follow || dog.GreetedBy != null || dog.State.Down
                || InFight || _roster.Raid)
                return;
            for (int i = 0; i < _robots.Robots.Count; i++)
            {
                Robot robot = _robots.Robots[i];
                if (FlatMath.Length(robot.transform.position - dog.transform.position) <= _settings.GreetRange && robot.TryGreetVisitor(dog.transform.position, now))
                {
                    // One robot at a time: the dog sniffs the one that greeted it.
                    dog.GreetedBy = robot.transform;
                    return;
                }
            }
        }

        public void HelpUp(Dog dog)
        {
            dog.State.HelpUp(Time.time, Stats);
            TryPlay(dog, DogSound.Whine);
        }

        // ---- Orders ----

        public void OpenWheel(Dog dog)
        {
            DogState state = dog.State;
            _slices.Clear();
            _slices.Add(new WheelSlice(_settings.FollowLabel.Text(), null, true, state.Order == DogOrder.Follow, () => Order(dog, DogOrder.Follow)));
            _slices.Add(new WheelSlice(_settings.StayLabel.Text(), null, true, state.Order == DogOrder.Stay, () => Order(dog, DogOrder.Stay)));
            _slices.Add(new WheelSlice(_settings.PetLabel.Text(), null, true, false, () => Pet(dog)));
            if (_herd != null && _herd.Enabled)
                _slices.Add(new WheelSlice(_settings.HerdingLabel.Text(), null, AnimalsOut > 0, state.Order == DogOrder.Herding, () => BringHerdHome(dog)));
            bool active = state.Stance == DogStance.Active;
            _slices.Add(new WheelSlice((active ? _settings.ActiveLabel : _settings.PassiveLabel).Text(), null, true, true,
                () => state.SetStance(active ? DogStance.Passive : DogStance.Active)));
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

        // Bring the herd home: the bell's call, then the dog pushes the stragglers along.
        public void BringHerdHome(Dog dog)
        {
            if (_herd == null || AnimalsOut == 0)
                return;
            _herd.Orders.Call();
            dog.State.EndCall();
            dog.State.StartHerding(dog.transform.position, Time.time);
            TryPlay(dog, DogSound.Bark);
        }

        public void Pet(Dog dog)
        {
            dog.State.Pet(Time.time);
            TryPlay(dog, DogSound.Pant);
        }

        // Q with an enemy under the aim: "get it!" (even Passive).
        public bool Sic(Dog dog, EnemyBrain enemy) => _fight.Sic(dog, enemy);

        // Answered a whistle (aimed at it or the plain fallback): it comes to heel, following again; annoyed, it comes
        // grumbling; still annoyed, it only shakes its head. In a fight a plain whistle means "back off".
        public bool Whistled(Dog dog, GameObject actor)
        {
            float now = Time.time;
            bool fight = InFight;
            if (fight)
                _fight.CallOff();
            switch (dog.State.Whistle(now, inFight: fight))
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
            if (!Enabled || dog == null || !dog.Active || actor == null || dog.State.Down)
                return false;
            if (FlatMath.Length(dog.transform.position - actor.transform.position) > _settings.HearWhistleRange)
                return false;
            // An enemy under the aim (the first thing the aim hits): the answer is an order to go for it, bound to that
            // enemy, so a later whistle aimed at the dog itself only ever calls it.
            EnemyBrain enemy = null;
            if (Physics.SphereCast(eye, 0.35f, aim, out RaycastHit hit, 50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(actor.transform))
                enemy = hit.collider.GetComponentInParent<EnemyBrain>();
            if (enemy != null && !enemy.IsDead)
            {
                _sic.Set(dog, enemy);
                target = _sic;
            }
            else
                target = dog;
            return true;
        }

        public bool TryPlay(Dog dog, DogSound sound, bool burst = false)
        {
            if (!_voice.TryPlay(sound, Time.time, burst))
                return false;
            if (_audio != null && _settings.Sounds.TryGetValue(sound, out SoundID id) && id.IsValid())
                _audio.PlayAt(id, dog.transform.position + Vector3.up * 1.15f);
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

        // A point out of the player's sight near `goal` (behind them) that the PLAYER can walk to (the dog may be stranded on
        // another island), to appear at when it has fallen far behind.
        public bool TryFindReappearPoint(Dog dog, Vector3 goal, out Vector3 point)
        {
            point = default;
            IPlayer player = _players.Current;
            AstarPath navigation = AstarPath.active;
            if (player == null || navigation == null)
                return false;
            GraphNode from = navigation.GetNearest(player.Transform.position, NearestNodeConstraint.Walkable).node;
            if (from == null)
                return false;
            for (int i = 0; i < 12; i++)
            {
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * (2f + i * 0.5f);
                var nearest = navigation.GetNearest(goal + new Vector3(jitter.x, 0f, jitter.y), NearestNodeConstraint.Walkable);
                if (nearest.node == null || !PathUtilities.IsPathPossible(from, nearest.node) || Visible(player.View, nearest.position))
                    continue;
                point = nearest.position;
                return true;
            }
            return false;
        }

        static bool Visible(Transform view, Vector3 point)
        {
            Vector3 to = point + Vector3.up * 0.8f - view.position;
            if (Vector3.Angle(view.forward, to) > 70f)
                return false;
            return !Physics.Raycast(view.position, to.normalized, to.magnitude - 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        // The fallback's answer while an enemy is under the aim (one instance, reused).
        sealed class SicOrder : IWhistleTarget
        {
            readonly DogManager _manager;
            Dog _dog;
            EnemyBrain _enemy;

            public SicOrder(DogManager manager) => _manager = manager;

            public void Set(Dog dog, EnemyBrain enemy)
            {
                _dog = dog;
                _enemy = enemy;
            }

            public bool TryGetWhistlePrompt(GameObject actor, out InteractionPrompt prompt)
            {
                prompt = default;
                return false;
            }

            public bool TryWhistle(GameObject actor) => _manager.Sic(_dog, _enemy);
        }
    }
}
