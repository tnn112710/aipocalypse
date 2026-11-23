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

        public bool CallAnnoyed { get; private set; }

        // `now` given: a fresh order from the player ("Stay" makes it a little sad for SadTime); NaN: a quiet change.
        public void SetOrder(DogOrder order, Vector3 at, float now = float.NaN)
        {
            Order = order;
            if (order == DogOrder.Stay)
                StayPoint = at;
            _sadUntil = order == DogOrder.Stay && !float.IsNaN(now) ? now + SadTime : float.NegativeInfinity;
        }

        // From a save: no reaction.
        public void Restore(DogOrder order, Vector3 stayPoint)
        {
            Order = order;
            StayPoint = stayPoint;
            _sadUntil = float.NegativeInfinity;
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
