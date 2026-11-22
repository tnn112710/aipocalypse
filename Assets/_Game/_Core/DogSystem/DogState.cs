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

        public bool Petting(float now) => now - _petAt < PetTime;

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
