using System;
using System.Collections.Generic;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.AnimalSystem;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.RobotSystem;
using MB13.Aipocalypse.Core.EnemySystem;
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


        readonly DogRoster _roster;
        readonly DogSettings _settings;
        readonly WorldClock _clock;
        readonly PlayerRegistry _players;
        readonly AudioService _audio;
        readonly CommandWheelPresenter _wheel;
        readonly IWaterLevel _water;
        readonly DogVoice _voice;
        readonly RandomStream _random;
        readonly EnemyRegistry _enemies;
        readonly DamageService _damage;
        readonly TimeService _time;
        readonly List<FoeView> _foes = new();
        readonly Dictionary<int, EnemyBrain> _brains = new();
        float _fightSeenAt = float.NegativeInfinity;
        bool _heel;
        int _sentId = -1;
        float _sentAt;
        float _lastBiteAt;
        float _nextWarn;
        bool _wasDown;
        EnemyBrain _aimedEnemy;
        readonly AnimalHerd _herd;
        readonly RobotRoster _robots;
        float _nextRaidBark;
        int _raidBarksLeft;
        float _nextBurstBark;
        readonly IDisposable _subscriptions;
        readonly List<WheelSlice> _slices = new();
        Vector3 _lastPlayerPosition;
        bool _hadPlayer;
        float _lookedFor;
        bool _hasPath = true;
        float _nextPathCheck;
        float _shakeUntil = float.NegativeInfinity;
        float _fencedFor;

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
            Stats = new DogStatSheet(settings.Stats, settings.Classes != null ? settings.Classes.Length : 1);
            _random = resolver.TryResolve(out RandomService random) ? random.Stream("Dog") : new RandomStream(7);
            resolver.TryResolve(out _enemies);
            resolver.TryResolve(out _herd);
            resolver.TryResolve(out _robots);
            resolver.TryResolve(out _damage);
            resolver.TryResolve(out _time);
            resolver.TryResolve(out _water);
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
                && (_roster.Night || (_roster.HasPlayer && Flat(dog.State.StayPoint - _roster.PlayerPosition) > _settings.StayGoHomeDistance)))
                dog.State.SetOrder(DogOrder.Stay, _roster.House.Door);
            if (_roster.HasPlayer && now >= _nextPathCheck)
            {
                _nextPathCheck = now + PathCheckInterval;
                _hasPath = HasPath(dog.transform.position, _roster.PlayerPosition);
            }
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
            Fenced(dog, dt);
            RaidBarks(dog, now);
            GreetRobots(dog, now);
            Swim(dog, now);
            LookedAt(dog, player, now, dt);
            dog.Advance(dt);
            if (now < dog.NextThinkAt)
                return;
            dog.NextThinkAt = now + _settings.ThinkInterval;
            if (_roster.HasPlayer && dog.State.Order == DogOrder.Follow)
                dog.Plan = dog.Follow.Plan(FollowInput(dog, player, now));
            SeeFoes(dog, player, now);
            dog.Think();
            // A bite asked for by the tree lands here, after its tick: a kill despawns the enemy's agent, a structural ECS
            // change Behavior Designer's own systems forbid while they run.
            if (_pendingBite >= 0)
            {
                int foe = _pendingBite;
                _pendingBite = -1;
                dog.BiteLanded = Bite(dog, foe) ? 2 : 1;
                if (dog.BiteLanded == 2)
                    dog.SetPin(true);
            }
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
            bool aiming = player.HeldItem != null && player.HeldItem.IsAiming;
            _roster.PlayerAiming = aiming;
            return new FollowInput(_roster.PlayerPosition, _roster.PlayerVelocity, _roster.PlayerForward, player.IsSprinting, aiming,
                player.View.position, player.View.forward, dog.transform.position, _hasPath, now, Separated(dog.transform.position));
        }

        // Whether it can walk to the player (checked every PathCheckInterval); false across the shut fence or into an island.
        public bool HasPathToPlayer => _hasPath;

        // The fence between it and a player within ScrambleRange: at once when there's no way round, else after ScrambleAfter
        // (a gap close by gets used first): it scrambles over (DogScramble).
        public bool WantsScramble { get; private set; }

        void Fenced(Dog dog, float dt)
        {
            Vector3 at = dog.transform.position;
            bool fenced = _roster.HasPlayer && _roster.Yard != null && _roster.Crossings.Count > 0
                && _roster.InYard(at) != _roster.InYard(_roster.PlayerPosition) && Flat(at - _roster.PlayerPosition) <= _settings.ScrambleRange;
            _fencedFor = fenced ? _fencedFor + dt : 0f;
            WantsScramble = fenced && (!_hasPath || _fencedFor >= _settings.ScrambleAfter);
        }

        // A wall between them: one in the yard and the other not (the fence), or one in the house and the other not (the house
        // floor is tagged RobotsOnly).
        bool Separated(Vector3 dog)
        {
            Vector3 player = _roster.PlayerPosition;
            return Indoors(dog) != Indoors(player) || (_roster.Yard != null && _roster.InYard(dog) != _roster.InYard(player));
        }

        static bool Indoors(Vector3 point)
        {
            AstarPath navigation = AstarPath.active;
            GraphNode node = navigation != null ? navigation.GetNearest(point, NearestNodeConstraint.Walkable).node : null;
            return node != null && node.Tag == EnemySystem.NavTags.RobotsOnly;
        }

        static bool HasPath(Vector3 from, Vector3 to)
        {
            AstarPath navigation = AstarPath.active;
            if (navigation == null)
                return true;
            GraphNode a = navigation.GetNearest(from, NearestNodeConstraint.Walkable).node;
            GraphNode b = navigation.GetNearest(to, NearestNodeConstraint.Walkable).node;
            return a == null || b == null || PathUtilities.IsPathPossible(a, b);
        }

        // Deep water: it swims, the model floating with its back at the surface; out again, it shakes itself dry soon after.
        void Swim(Dog dog, float now)
        {
            Vector3 at = dog.transform.position;
            float level = 0f;
            bool deep = _water != null && _water.TryGetLevel(at, out level) && level - at.y > _settings.SwimDepth;
            if (deep)
                dog.TargetLift = Mathf.Max(0f, level - at.y - SwimBodyDepth);
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

        // ---- The fight ----

        // The enemy it's going for (−1: none) and one it should growl a warning at (−1: none), set each think.
        public int TargetId { get; private set; } = -1;
        public int WarnId { get; private set; } = -1;
        public bool InFight => Time.time - _fightSeenAt < _settings.FightOverTime;
        public bool PlayerHurt { get; private set; }
        public IReadOnlyList<FoeView> Foes => _foes;

        public bool TryGetFoe(int id, out FoeView foe)
        {
            for (int i = 0; i < _foes.Count; i++)
                if (_foes[i].Id == id)
                {
                    foe = _foes[i];
                    return true;
                }
            foe = default;
            return false;
        }

        // What it knows of the enemies within SenseRange; whether a fight is on; who to go for (the stance, a send, back
        // off), who to warn about. Never while down.
        void SeeFoes(Dog dog, IPlayer player, float now)
        {
            _foes.Clear();
            _brains.Clear();
            TargetId = -1;
            WarnId = -1;
            PlayerHurt = _roster.HasPlayer && player.Health != null && player.Health.Normalized < _settings.PlayerHurtBelow;
            if (_enemies == null || dog.State.Down)
                return;
            Vector3 at = dog.transform.position;
            float sense = Stats.Get(DogStat.SenseRange);
            Vector3 playerAt = _roster.PlayerPosition;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyBrain brain = _enemies[i];
                if (brain == null || brain.IsDead || !brain.isActiveAndEnabled)
                    continue;
                Vector3 position = brain.transform.position;
                bool near = Flat(position - at) <= sense || (_roster.HasPlayer && Flat(position - playerAt) <= sense);
                if (!near)
                    continue;
                int id = IdOf(brain);
                bool attackingPlayer = brain.Target is IPlayer;
                MeleeSettings melee = brain.Definition.Melee;
                bool swinging = attackingPlayer && melee != null && Flat(position - playerAt) <= melee.AttackRange + 0.5f;
                float health = brain.Body != null && brain.Body.Health != null ? brain.Body.Health.Normalized : 1f;
                _foes.Add(new FoeView(id, position, brain.transform.forward, brain.Definition.Ranged != null, brain.Alerted, attackingPlayer,
                    swinging, health, ClassOf(brain), true, _roster.InYard(position)));
                _brains[id] = brain;
                if (brain.Alerted && _roster.HasPlayer && Flat(position - playerAt) <= _settings.FightRange)
                    _fightSeenAt = now;
            }
            if (!InFight)
                _heel = false;
            if (_sentId >= 0 && (!_brains.ContainsKey(_sentId) || now - Mathf.Max(_sentAt, _lastBiteAt) > _settings.SentGiveUp))
                _sentId = -1;
            bool following = dog.State.Order == DogOrder.Follow && _roster.HasPlayer;
            bool limping = dog.State.Limping(now);
            // A raid with the dog in the yard: Active, it goes for raiders that got in (aware of the player or not).
            bool homeDefence = _roster.Raid && _roster.InYard(at) && dog.State.Stance == DogStance.Active;
            if (_sentId >= 0 || ((following || homeDefence) && !_heel && !limping && !dog.State.Retreating(Stats)))
                TargetId = DogCombat.PickTarget(_foes, playerAt, dog.State.Stance, _sentId, homeDefence, Stats);
            if (TargetId < 0 && now >= _nextWarn && !InFight)
                WarnId = DogCombat.WarnTarget(_foes, at, sense);
        }

        // A non-negative id per enemy (−1 means none): its object hash with the sign bit cleared.
        static int IdOf(EnemyBrain brain) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(brain) & int.MaxValue;

        int ClassOf(EnemyBrain brain)
        {
            StaggerProfile profile = brain.Definition.Reaction != null ? brain.Definition.Reaction.Stagger : null;
            int index = profile != null && _settings.Classes != null ? System.Array.IndexOf(_settings.Classes, profile) : -1;
            return index < 0 ? 0 : index;
        }

        // A warning under way (its target is cleared at once by the gap): the warn branch holds until it's done.
        public bool Warning => Time.time < _warningUntil;
        float _warningUntil;

        int _pendingBite = -1;

        // The skirmish's bite (dealt after the tree's tick; Dog.BiteLanded reports it).
        public void RequestBite(Dog dog, int foeId)
        {
            _pendingBite = foeId;
            dog.BiteLanded = 0;
        }

        public void Warned(float now)
        {
            _nextWarn = now + _settings.WarnGap;
            _warningUntil = now + 6f;
        }

        public void WarningDone() => _warningUntil = 0f;

        // A bite on `foeId`: damage and a stagger, or (a lucky roll) a takedown that drags it down toward the dog. The bitten
        // enemy and those close by may swing back for a moment (EnemyBrain.Retaliate). True: a takedown.
        public bool Bite(Dog dog, int foeId)
        {
            if (!_brains.TryGetValue(foeId, out EnemyBrain brain) || brain == null || brain.IsDead || brain.Body == null || _damage == null)
                return false;
            float now = Time.time;
            _lastBiteAt = now;
            Vector3 dogAt = dog.transform.position;
            Vector3 at = brain.transform.position;
            Vector3 towardEnemy = at - dogAt;
            towardEnemy.y = 0f;
            towardEnemy = towardEnemy.sqrMagnitude > 1e-4f ? towardEnemy.normalized : dog.transform.forward;
            Vector3 point = at + Vector3.up * 0.8f - towardEnemy * 0.3f;
            float health = brain.Body.Health != null ? brain.Body.Health.Normalized : 1f;
            bool takedown = dog.State.TryTakedown(health, ClassOf(brain), now, Roll(), Stats);
            if (takedown)
            {
                // The push points back at the dog: the body falls toward it, dragged down.
                _damage.Deal(brain.Body, new DamageInfo(brain.Body.Health.Current, DamageType.Melee, point, -towardEnemy * _settings.BitePush, dog.gameObject));
                _time?.RequestHitStop(_settings.TakedownHitStop, 0f);
                TryPlay(dog, DogSound.Growl);
            }
            else
            {
                _damage.Deal(brain.Body, new DamageInfo(Stats.Get(DogStat.BiteDamage), DamageType.Melee, point, towardEnemy * _settings.BitePush, dog.gameObject));
                if (!brain.IsDead)
                {
                    brain.Stagger(Stats.Get(DogStat.StaggerTime));
                    brain.Animation?.Hit(towardEnemy * _settings.BitePush, false);
                }
                TryPlay(dog, DogSound.Bite);
            }
            Provoke(dog, dogAt, now);
            return takedown;
        }

        // Enemies within RetaliationRadius of the dog get a short window to swing at it (only while it's within their reach).
        public void Provoke(Dog dog, Vector3 dogAt, float now)
        {
            float until = now + Stats.Get(DogStat.RetaliationWindow);
            foreach (EnemyBrain brain in _brains.Values)
            {
                if (brain == null || brain.IsDead || Flat(brain.transform.position - dogAt) > _settings.RetaliationRadius + 0.5f)
                    continue;
                float reach = brain.Definition.Ranged != null ? brain.Definition.Ranged.MaxRange
                    : brain.Definition.Melee != null ? brain.Definition.Melee.AttackRange + _settings.MeleeReachSlack : 2f;
                brain.Retaliate(dog, until, reach);
            }
        }

        // ---- The farm ----

        public AnimalHerd Herd => _herd;
        public int AnimalsOut => _herd == null ? 0 : _herd.Animals.Count - _herd.CountHome();
        public bool AtHome(Dog dog) => _roster.InYard(dog.transform.position);
        public float HerdStartedAt { get; set; }

        // Raiders near a Passive dog at home: it keeps away from them (never bites).
        public bool RaiderClose(Dog dog, out Vector3 raider)
        {
            raider = default;
            if (!_roster.Raid || dog.State.Stance != DogStance.Passive || !AtHome(dog))
                return false;
            float best = _settings.AvoidRange;
            bool found = false;
            for (int i = 0; i < _foes.Count; i++)
            {
                float d = Flat(_foes[i].Position - dog.transform.position);
                if (d < best)
                {
                    best = d;
                    raider = _foes[i].Position;
                    found = true;
                }
            }
            return found;
        }

        // A raid with the dog in the yard: bursts of 2–3 barks toward the nearest raider, RaidBarkGap apart.
        void RaidBarks(Dog dog, float now)
        {
            if (!_roster.Raid || !AtHome(dog) || dog.State.Down || _foes.Count == 0)
            {
                _raidBarksLeft = 0;
                return;
            }
            if (_raidBarksLeft == 0 && now >= _nextRaidBark)
            {
                _raidBarksLeft = 2 + (Roll() < 0.5f ? 1 : 0);
                _nextRaidBark = now + Mathf.Lerp(_settings.RaidBarkGap.x, _settings.RaidBarkGap.y, Roll());
            }
            if (_raidBarksLeft > 0 && now >= _nextBurstBark)
            {
                _nextBurstBark = now + 1.3f;
                _raidBarksLeft--;
                if (TryPlay(dog, DogSound.Bark) && dog.Standing && Flat(dog.Legs.Velocity) < 0.5f)
                    dog.Act(DogAct.Bark);
            }
        }

        // A free robot close by greets the dog (it waves, heart eyes); the dog sniffs it (DogHomeLife picks it up).
        void GreetRobots(Dog dog, float now)
        {
            if (_robots == null || dog.State.Down || InFight || _roster.Raid)
                return;
            for (int i = 0; i < _robots.Robots.Count; i++)
            {
                Robot robot = _robots.Robots[i];
                if (Flat(robot.transform.position - dog.transform.position) <= _settings.GreetRange && robot.TryGreetVisitor(dog.transform.position, now))
                    dog.GreetedBy = robot.transform;
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
            dog.State.SetOrder(DogOrder.Herding, dog.transform.position);
            HerdStartedAt = Time.time;
            TryPlay(dog, DogSound.Bark);
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
            // Aimed at an enemy: "get it!" (even Passive). In a fight a plain whistle means "back off".
            if (_aimedEnemy != null && !_aimedEnemy.IsDead)
            {
                _sentId = IdOf(_aimedEnemy);
                _sentAt = now;
                _heel = false;
                TryPlay(dog, DogSound.Bark);
                return true;
            }
            bool fight = InFight;
            if (fight)
            {
                _heel = true;
                _sentId = -1;
            }
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
            _aimedEnemy = null;
            Dog dog = _roster.Dog;
            if (!Enabled || dog == null || !dog.Active || actor == null || dog.State.Down)
                return false;
            // An enemy under the aim (the first thing the aim hits): the whistle sends the dog at it.
            if (Physics.SphereCast(eye, 0.35f, aim, out RaycastHit hit, 50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(actor.transform))
                _aimedEnemy = hit.collider.GetComponentInParent<EnemyBrain>();
            if (Flat(dog.transform.position - actor.transform.position) > _settings.HearWhistleRange)
                return false;
            target = dog;
            return true;
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
            Vector3 to = point + Vector3.up * 0.4f - view.position;
            if (Vector3.Angle(view.forward, to) > 70f)
                return false;
            return !Physics.Raycast(view.position, to.normalized, to.magnitude - 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
