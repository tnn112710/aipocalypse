using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // What a robot was asked or put through and for how long: greetings and pats, hits and knockdowns (then a sulk), a gun
    // pointed at it, being woken at night, being walked into, the end of a raid, a welcome home. Plain C#, times passed in.
    public sealed class RobotState
    {
        readonly RobotSettings _settings;
        // Times of the latest hits, newest last, for the knockdown count.
        readonly float[] _hits = new float[8];
        int _hitCount;
        float _greetUntil = float.NegativeInfinity;
        float _hitUntil = float.NegativeInfinity;
        float _downAt = float.NegativeInfinity;
        float _sulkUntil = float.NegativeInfinity;
        float _nervousUntil = float.NegativeInfinity;
        float _wakeUntil = float.NegativeInfinity;
        float _cheerUntil = float.NegativeInfinity;
        float _stepAt = float.NegativeInfinity;
        bool _stepRequested;
        RobotCooldown _waveCooldown;
        RobotCooldown _nervousCooldown;

        public RobotState(RobotSettings settings) => _settings = settings;

        // Each changes when a new one starts, so the task showing it reacts again.
        public int GreetingVersion { get; private set; }
        public int PatVersion { get; private set; }
        public int CheerVersion { get; private set; }
        public int HitIndex { get; private set; }
        public Vector3 StepFrom { get; private set; }
        public RobotCheer Cheer { get; private set; }
        public bool WelcomePending { get; private set; }

        public bool Greeting(float now) => now < _greetUntil;
        public bool Hit(float now) => now < _hitUntil;
        public bool Down(float now) => now < _downAt + _settings.KnockdownLie + _settings.KnockdownRise;
        // Down and not yet getting up.
        public bool Lying(float now) => now < _downAt + _settings.KnockdownLie;
        public bool Sulking(float now) => now < _sulkUntil;
        public bool Nervous(float now) => now < _nervousUntil;
        public bool Cheering(float now) => Cheer != RobotCheer.None && now < _cheerUntil;
        public bool Asleep(bool night, float now) => night && now >= _wakeUntil;
        public bool StepRequested(float now) => _stepRequested && now - _stepAt <= _settings.StepExpiry;
        public bool CanWave(float now) => _waveCooldown.Ready(now);
        public bool CanGetNervous(float now) => _nervousCooldown.Ready(now);

        // A look or E: starts a greeting or keeps one going; `fresh` lets it wave or talk again.
        public void Greet(float now, bool fresh)
        {
            if (!Greeting(now) || fresh)
                GreetingVersion++;
            _greetUntil = now + _settings.GreetHold;
        }

        // A pat: a fresh greeting that shows love, and it forgives being knocked down.
        public void Pat(float now)
        {
            PatVersion++;
            _sulkUntil = float.NegativeInfinity;
            Greet(now, fresh: true);
        }

        public void UseWave(float now) => _waveCooldown.Use(now, _settings.WaveCooldown);

        // `side`: 0 front, 1 left, 2 right. KnockdownHits within KnockdownWindow knock it down; hits while down do nothing.
        public RobotHit TakeHit(float now, int side)
        {
            if (Down(now))
                return RobotHit.None;
            HitIndex = side;
            if (_hitCount == _hits.Length)
            {
                System.Array.Copy(_hits, 1, _hits, 0, _hits.Length - 1);
                _hitCount--;
            }
            _hits[_hitCount++] = now;
            int recent = 0;
            for (int i = 0; i < _hitCount; i++)
                if (now - _hits[i] <= _settings.KnockdownWindow)
                    recent++;
            if (recent >= _settings.KnockdownHits)
            {
                _hitCount = 0;
                _downAt = now;
                _hitUntil = now + _settings.KnockdownLie + _settings.KnockdownRise;
                _sulkUntil = _hitUntil + _settings.SulkTime;
                _stepRequested = false;
                return RobotHit.Knockdown;
            }
            _hitUntil = now + _settings.HitHold;
            _stepRequested = false;
            return RobotHit.Flinch;
        }

        public void MakeNervous(float now)
        {
            _nervousUntil = now + _settings.NervousTime;
            _nervousCooldown.Use(now, _settings.NervousCooldown);
        }

        // E, a pat, a whistle or a hit at night: awake for a while.
        public void Wake(float now) => _wakeUntil = now + _settings.NightWake;

        // Walked into; the request goes stale after StepExpiry if nothing takes it.
        public void RequestStep(Vector3 from, float now)
        {
            _stepRequested = true;
            _stepAt = now;
            StepFrom = from;
        }

        public void ClearStep() => _stepRequested = false;

        public void CheerFor(RobotCheer cheer, float now)
        {
            Cheer = cheer;
            if (cheer == RobotCheer.None)
                return;
            CheerVersion++;
            _cheerUntil = now + _settings.CheerTime;
        }

        public void Welcome() => WelcomePending = true;
        public void ClearWelcome() => WelcomePending = false;

        // Disabled: what was pending is dropped (timers run out on their own).
        public void Reset()
        {
            _stepRequested = false;
            WelcomePending = false;
            Cheer = RobotCheer.None;
            _greetUntil = _nervousUntil = _cheerUntil = float.NegativeInfinity;
        }
    }
}
