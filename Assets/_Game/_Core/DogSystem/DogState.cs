using System.Collections.Generic;
using MB13.Aipocalypse.Core.RobotSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // The dog's rules in plain C# (times passed in, tested): its order (a little sad when told to stay), being called,
    // whistle annoyance, petting, head tilts.
    public sealed class DogState
    {
        // Seconds a call lasts at most (it gives up), a petting, and the sad moment after "Stay".
        public const float CallTimeout = 20f;
        public const float PetTime = 4.5f;
        // Time to get up and come over for a pet before it gives up.
        public const float PetComeTime = 8f;
        public const float SadTime = 6f;

        readonly DogSettings _settings;
        readonly RobotStreak _whistles = new(8);
        float _annoyedUntil = float.NegativeInfinity;
        float _nextHeadTilt;
        float _calledAt = float.NaN;
        float _petAt = float.NegativeInfinity;
        float _sadUntil = float.NegativeInfinity;

        public DogOrder Order { get; private set; }
        public Vector3 StayPoint { get; private set; }

        public DogState(DogSettings settings) => _settings = settings;

        // ---- Welcome home: staying at home, the player away a while and back in the yard ----

        float _awaySince = float.NaN;
        float _welcomeUntil = float.NegativeInfinity;

        // Every think: where the player is, and whether the dog is staying at home.
        public void TrackPlayer(bool playerInYard, bool stayingHome, float now)
        {
            if (!stayingHome)
            {
                // Told to follow (or a raid): a welcome still pending is called off, not saved for the next "Stay".
                _awaySince = float.NaN;
                _welcomeUntil = float.NegativeInfinity;
                return;
            }
            if (!playerInYard)
            {
                if (float.IsNaN(_awaySince))
                    _awaySince = now;
                return;
            }
            if (!float.IsNaN(_awaySince) && now - _awaySince >= _settings.WelcomeAfter)
                _welcomeUntil = now + _settings.WelcomeTime;
            _awaySince = float.NaN;
        }

        public bool Welcoming(float now) => now < _welcomeUntil;
        public void WelcomeDone() => _welcomeUntil = float.NegativeInfinity;

        public bool CallAnnoyed { get; private set; }

        // `now` given: a fresh order from the player ("Stay" makes it a little sad for SadTime); NaN: a quiet change.
        public void SetOrder(DogOrder order, Vector3 at, float now = float.NaN)
        {
            Order = order;
            if (order == DogOrder.Stay)
                StayPoint = at;
            _sadUntil = order == DogOrder.Stay && !float.IsNaN(now) ? now + SadTime : float.NegativeInfinity;
        }

        // From a save: no reaction. `herdFor`: how long it had been herding; `howledNight`: the night it last rolled a howl.
        public void Restore(DogOrder order, Vector3 stayPoint, float herdFor, int howledNight, float now)
        {
            Order = order;
            StayPoint = stayPoint;
            _sadUntil = float.NegativeInfinity;
            HerdStartedAt = now - herdFor;
            _howledDay = howledNight;
        }

        public bool Sad(float now) => now < _sadUntil;

        // Whistled to come: until it arrives (EndCall) or CallTimeout passes. Annoyed: it comes slowly, grumbling.
        public void Call(float now, bool annoyed)
        {
            _calledAt = now;
            CallAnnoyed = annoyed;
            _sadUntil = float.NegativeInfinity;
        }

        public bool Called(float now) => !float.IsNaN(_calledAt) && now - _calledAt < CallTimeout;

        public void EndCall() => _calledAt = float.NaN;

        bool _petArrived;

        public bool Petting(float now) => now - _petAt < (_petArrived ? PetTime : PetComeTime);

        // In front of the player: the petting itself starts (PetTime from here).
        public void PetArrived(float now)
        {
            _petAt = now;
            _petArrived = true;
        }

        // ---- Fighting: stance, toughness, knocked down, limping, takedown rolls ----

        float _hurtAt = float.NegativeInfinity;
        float _downAt;
        float _limpUntil = float.NegativeInfinity;
        float _takedownReadyAt;

        public DogStance Stance { get; private set; }
        public float Toughness { get; private set; }
        public bool Down { get; private set; }
        public float LimpLeft(float now) => Mathf.Max(0f, _limpUntil - now);
        public bool Limping(float now) => now < _limpUntil;

        public void SetStance(DogStance stance) => Stance = stance;

        // ---- The farm (Phase 3) ----

        public const float HerdGiveUp = 180f;
        public const float StuckTime = 20f;
        float _nextSheepChase = float.NaN;
        int _howledDay = -1;

        public static bool CanHerd(int animalsOut) => animalsOut > 0;

        // The night (its starting day) whose howl roll is used up; -1 none yet.
        public int HowledNight => _howledDay;

        public float HerdStartedAt { get; private set; }

        public void StartHerding(Vector3 at, float now)
        {
            SetOrder(DogOrder.Herding, at);
            HerdStartedAt = now;
        }

        public float HerdingFor(float now) => Order == DogOrder.Herding ? now - HerdStartedAt : 0f;

        // Cheats: the next home-life pick is a sheep chase.
        public void ForceSheepChase() => _nextSheepChase = 0f;

        // The animal to push next: the farthest from home among those out (ties: the slowest), skipping any stuck for StuckTime.
        public static int PickHerdTarget(IReadOnlyList<HerdView> animals, Vector3 home)
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < animals.Count; i++)
            {
                HerdView animal = animals[i];
                if (animal.Home || animal.NoProgressFor >= StuckTime)
                    continue;
                float score = Vector2.Distance(new Vector2(animal.Position.x, animal.Position.z), new Vector2(home.x, home.z)) - animal.Speed * 0.01f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = animal.Id;
                }
            }
            return best;
        }

        public static bool HerdDone(int home, int total, float startedAt, float now) => home >= total || now - startedAt >= HerdGiveUp;

        // At home in the day, not herding: a playful run at the sheep every 3–6 minutes (`roll` 0..1 picks when next).
        public bool TryStartSheepChase(float now, bool day, bool atHome, bool herding, float roll)
        {
            if (float.IsNaN(_nextSheepChase))
                _nextSheepChase = now + Mathf.Lerp(180f, 360f, roll);
            if (!day || !atHome || herding || now < _nextSheepChase)
                return false;
            _nextSheepChase = now + Mathf.Lerp(180f, 360f, roll);
            return true;
        }

        // A howl at most once a night: at home, between 22:00 and 02:00, no raid, a HowlChance roll (one roll per night).
        public bool TryHowl(int day, float hour, bool atHome, bool raid, float roll)
        {
            bool lateNight = hour >= 22f || hour < 2f;
            if (!lateNight || !atHome || raid)
                return false;
            // The small hours belong to the night that started the day before.
            int night = hour < 2f ? day - 1 : day;
            if (night == _howledDay)
                return false;
            _howledDay = night;
            return roll < _settings.HowlChance;
        }

        // When its next bite is ready (BiteCooldown after the last).
        public float BiteReadyAt { get; private set; }

        public void Bitten(float now, float cooldown) => BiteReadyAt = now + cooldown;

        public void ResetToughness(DogStatSheet stats)
        {
            Toughness = stats.Get(DogStat.Toughness);
            Down = false;
            _limpUntil = float.NegativeInfinity;
        }

        // From a save.
        public void RestoreFight(DogStance stance, float toughness, bool down, float limpLeft, float now)
        {
            Stance = stance;
            Toughness = toughness;
            Down = down;
            _downAt = now;
            _limpUntil = limpLeft > 0f ? now + limpLeft : float.NegativeInfinity;
        }

        // A hit that got through: at 0 it's knocked down (no more damage while down).
        public void Hurt(float amount, float now, DogStatSheet stats)
        {
            if (Down || amount <= 0f)
                return;
            _hurtAt = now;
            Toughness = Mathf.Max(0f, Toughness - amount);
            if (Toughness > 0f)
                return;
            Down = true;
            _downAt = now;
            _calledAt = float.NaN;
        }

        // Regrows toughness after RegenDelay unhurt; down too long with nobody helping, it gets up by itself.
        public void Tick(float dt, float now, DogStatSheet stats)
        {
            if (Down)
            {
                if (now - _downAt >= _settings.SelfRiseTime)
                    HelpUp(now, stats);
                return;
            }
            if (now - _hurtAt >= _settings.RegenDelay)
                Toughness = Mathf.Min(stats.Get(DogStat.Toughness), Toughness + stats.Get(DogStat.RegenPerSecond) * dt);
        }

        // A hit on the dog: the player's own only make it yelp; while escaping it may dodge (DodgeChance against `roll`); else
        // it loses toughness (a flinch, or knocked down at 0). Down, nothing reaches it.
        public DogHit Hit(float amount, bool fromPlayer, bool escaping, float roll, float now, DogStatSheet stats)
        {
            if (Down)
                return DogHit.Ignored;
            if (fromPlayer)
                return DogHit.Yelped;
            if (escaping && roll < stats.Get(DogStat.DodgeChance))
                return DogHit.Dodged;
            Hurt(amount, now, stats);
            return Down ? DogHit.KnockedDown : DogHit.Flinched;
        }

        // Back on its feet at HelpUpToughness, limping for LimpTime.
        public void HelpUp(float now, DogStatSheet stats)
        {
            if (!Down)
                return;
            Down = false;
            Toughness = stats.Get(DogStat.Toughness) * stats.Get(DogStat.HelpUpToughness);
            _hurtAt = now;
            _limpUntil = now + stats.Get(DogStat.LimpTime);
        }

        // Hurt enough to back off to the player (unless sent at an enemy; the caller checks that).
        public bool Retreating(DogStatSheet stats) => !Down && Toughness <= stats.Get(DogStat.Toughness) * stats.Get(DogStat.RetreatAt);

        public static float TakedownChance(float healthFraction, DogStatSheet stats) =>
            stats.Get(DogStat.TakedownChanceBase) + stats.Get(DogStat.TakedownChanceBonus) * (1f - Mathf.Clamp01(healthFraction));

        // A bite's roll to drag the enemy down: within its class, off cooldown, under the chance.
        public bool TryTakedown(float healthFraction, int enemyClass, float now, float roll, DogStatSheet stats)
        {
            if (enemyClass > stats.GetClass(DogStat.MaxTakedownClass) || now < _takedownReadyAt)
                return false;
            if (roll >= TakedownChance(healthFraction, stats))
                return false;
            _takedownReadyAt = now + stats.Get(DogStat.TakedownCooldown);
            return true;
        }

        public bool Annoyed(float now) => now < _annoyedUntil;

        // A plain whistle. Spam (SpamWhistles within SpamWindow) annoys it for AnnoyedTime; whistles in a fight never count.
        public DogWhistle Whistle(float now, bool inFight)
        {
            if (Annoyed(now))
                return DogWhistle.Ignored;
            if (inFight)
                return DogWhistle.Come;
            if (_whistles.Add(now, _settings.SpamWindow) < _settings.SpamWhistles)
                return DogWhistle.Come;
            _whistles.Clear();
            _annoyedUntil = now + _settings.AnnoyedTime;
            return DogWhistle.Annoyed;
        }

        public void Pet(float now)
        {
            _petAt = now;
            _petArrived = false;
            _sadUntil = float.NegativeInfinity;
            _annoyedUntil = float.NegativeInfinity;
            _whistles.Clear();
        }

        // Sitting where it was called to: done once the player comes to it, or after CallWait.
        public static bool CallOver(float sittingFor, float playerDistance, DogSettings settings) =>
            playerDistance <= settings.ReachMe || sittingFor >= settings.CallWait;

        public bool TryHeadTilt(float now)
        {
            if (now < _nextHeadTilt)
                return false;
            _nextHeadTilt = now + _settings.HeadTiltGap;
            return true;
        }

        // How settled it is after the player stood still for `stillFor` seconds (the times rolled once per settle).
        public static DogPose SettlePose(float stillFor, float napAt, float sitAt, float lieAt)
        {
            if (stillFor >= napAt)
                return DogPose.Sleep;
            if (stillFor >= lieAt)
                return DogPose.Lie;
            return stillFor >= sitAt ? DogPose.Sit : DogPose.Stand;
        }

        public void Reset()
        {
            Order = DogOrder.Follow;
            StayPoint = default;
            _annoyedUntil = float.NegativeInfinity;
            _nextHeadTilt = 0f;
            _whistles.Clear();
            _calledAt = float.NaN;
            _petAt = float.NegativeInfinity;
            _sadUntil = float.NegativeInfinity;
        }
    }
}
