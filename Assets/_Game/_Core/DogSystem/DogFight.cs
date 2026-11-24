using System.Collections.Generic;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.TimeSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // The dog's fight, owned and ticked by DogManager: what it knows of the enemies round it and the player, who to go for
    // or growl a warning at, the bites and takedowns it deals, provoking enemies to swing back, the "get it!" order, backing
    // off on a whistle, and its barks at raiders from the yard.
    public sealed class DogFight
    {
        readonly DogManager _manager;
        readonly DogRoster _roster;
        readonly DogSettings _settings;
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
        float _warningUntil;
        int _pendingBite = -1;
        float _nextRaidBark;
        bool _inBurst;
        int _raidBarksLeft;
        float _nextBurstBark;

        public DogFight(DogManager manager, DogRoster roster, DogSettings settings, EnemyRegistry enemies, DamageService damage,
            TimeService time)
        {
            _manager = manager;
            _roster = roster;
            _settings = settings;
            _enemies = enemies;
            _damage = damage;
            _time = time;
        }

        DogStatSheet Stats => _manager.Stats;

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
        public void See(Dog dog, IPlayer player, float now)
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
                bool near = FlatMath.Length(position - at) <= sense || (_roster.HasPlayer && FlatMath.Length(position - playerAt) <= sense);
                if (!near)
                    continue;
                int id = IdOf(brain);
                bool attackingPlayer = brain.Target is IPlayer;
                MeleeSettings melee = brain.Definition.Melee;
                bool swinging = attackingPlayer && melee != null && FlatMath.Length(position - playerAt) <= melee.AttackRange + 0.5f;
                float health = brain.Body != null && brain.Body.Health != null ? brain.Body.Health.Normalized : 1f;
                _foes.Add(new FoeView(id, position, brain.transform.forward, brain.Definition.Ranged != null, brain.Alerted, attackingPlayer,
                    swinging, health, ClassOf(brain), true, _roster.InYard(position)));
                _brains[id] = brain;
                if (brain.Alerted && _roster.HasPlayer && FlatMath.Length(position - playerAt) <= _settings.FightRange)
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

        int ClassOf(EnemyBrain brain) =>
            DogCombat.ClassOf(_settings.Classes, brain.Definition.Reaction != null ? brain.Definition.Reaction.Stagger : null);

        // A warning under way (its target is cleared at once by the gap): the warn branch holds until it's done.
        public bool Warning => Time.time < _warningUntil;

        // The skirmish's bite (dealt after the tree's tick; Dog.BiteLanded reports it).
        public void RequestBite(Dog dog, int foeId)
        {
            _pendingBite = foeId;
            dog.BiteLanded = 0;
        }

        // Deals the bite the tree asked for in its last tick, if any.
        public void LandBite(Dog dog)
        {
            if (_pendingBite < 0)
                return;
            int foe = _pendingBite;
            _pendingBite = -1;
            dog.BiteLanded = Bite(dog, foe) ? 2 : 1;
            if (dog.BiteLanded == 2)
                dog.SetPin(true);
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
            bool takedown = dog.State.TryTakedown(health, ClassOf(brain), now, _manager.Roll(), Stats);
            if (takedown)
            {
                // The push points back at the dog: the body falls toward it, dragged down.
                _damage.Deal(brain.Body, new DamageInfo(brain.Body.Health.Current, DamageType.Melee, point, -towardEnemy * _settings.BitePush, dog.gameObject));
                _time?.RequestHitStop(_settings.TakedownHitStop, _settings.TakedownHitStopScale);
                _manager.TryPlay(dog, DogSound.Growl);
            }
            else
            {
                _damage.Deal(brain.Body, new DamageInfo(Stats.Get(DogStat.BiteDamage), DamageType.Melee, point, towardEnemy * _settings.BitePush, dog.gameObject));
                if (!brain.IsDead)
                {
                    brain.Stagger(Stats.Get(DogStat.StaggerTime));
                    brain.Animation?.Hit(towardEnemy * _settings.BitePush, false);
                }
                _manager.TryPlay(dog, DogSound.Bite);
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
                if (brain == null || brain.IsDead || !DogCombat.Provokes(dogAt, brain.transform.position, _settings.RetaliationRadius))
                    continue;
                float reach = brain.Definition.Ranged != null ? brain.Definition.Ranged.MaxRange
                    : brain.Definition.Melee != null ? brain.Definition.Melee.AttackRange + _settings.MeleeReachSlack : 2f;
                brain.Retaliate(dog, until, reach);
            }
        }

        // Raiders near a Passive dog at home: it keeps away from them (never bites).
        public bool RaiderClose(Dog dog, out Vector3 raider)
        {
            raider = default;
            if (!_roster.Raid || dog.State.Stance != DogStance.Passive || !_manager.AtHome(dog))
                return false;
            float best = _settings.AvoidRange;
            bool found = false;
            for (int i = 0; i < _foes.Count; i++)
            {
                float d = FlatMath.Length(_foes[i].Position - dog.transform.position);
                if (d < best)
                {
                    best = d;
                    raider = _foes[i].Position;
                    found = true;
                }
            }
            return found;
        }

        // The enemy nearest the dog among those it sees (any distance).
        public bool NearestFoe(Dog dog, out Vector3 at)
        {
            at = default;
            float best = float.MaxValue;
            for (int i = 0; i < _foes.Count; i++)
            {
                float d = FlatMath.Length(_foes[i].Position - dog.transform.position);
                if (_foes[i].Alive && d < best)
                {
                    best = d;
                    at = _foes[i].Position;
                }
            }
            return best < float.MaxValue;
        }

        // A raid with the dog in the yard: bursts of 2–3 barks toward the nearest raider, RaidBarkGap apart.
        public void RaidBarks(Dog dog, float now)
        {
            if (!_roster.Raid || !_manager.AtHome(dog) || dog.State.Down || _foes.Count == 0)
            {
                _raidBarksLeft = 0;
                _inBurst = false;
                return;
            }
            if (_raidBarksLeft == 0 && now >= _nextRaidBark)
            {
                _raidBarksLeft = 2 + (_manager.Roll() < 0.5f ? 1 : 0);
                _nextRaidBark = now + Mathf.Lerp(_settings.RaidBarkGap.x, _settings.RaidBarkGap.y, _manager.Roll());
            }
            if (_raidBarksLeft > 0 && now >= _nextBurstBark)
            {
                _nextBurstBark = now + 1.3f;
                bool first = !_inBurst;
                _inBurst = --_raidBarksLeft > 0;
                if (_manager.TryPlay(dog, DogSound.Bark, burst: !first) && dog.Standing && FlatMath.Length(dog.Legs.Velocity) < 0.5f)
                    dog.Act(DogAct.Bark);
            }
        }

        // Q with an enemy under the aim: "get it!" (even Passive).
        public bool Sic(Dog dog, EnemyBrain enemy)
        {
            if (dog == null || !dog.Usable || enemy == null || enemy.IsDead)
                return false;
            _sentId = IdOf(enemy);
            _sentAt = Time.time;
            _heel = false;
            _manager.TryPlay(dog, DogSound.Bark);
            return true;
        }

        // A plain whistle in a fight: back to heel, the send called off.
        public void CallOff()
        {
            _heel = true;
            _sentId = -1;
        }
    }
}
