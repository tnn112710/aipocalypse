using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // The rules behind an enemy's animator parameters, free of Unity objects: from its speed and turn rate, where it looks,
    // how it leans and when it fidgets. EnemyAnimator feeds it each frame and writes the results.
    public sealed class LocomotionAnimation
    {
        readonly EnemyAnimationSettings _settings;
        readonly int _fidgets;
        readonly System.Random _random;
        float _stillFor;
        float _nextGlance;
        float _fidgetAt;
        bool _fidgetRolled;
        int _pendingFidget = -1;

        public LocomotionAnimation(EnemyAnimationSettings settings, int fidgets, int seed)
        {
            _settings = settings;
            _fidgets = fidgets;
            _random = new System.Random(seed);
            _fidgetAt = Range(settings.FidgetDelay);
        }

        public float Speed { get; private set; }
        public Vector2 Look { get; private set; }
        public float Lean { get; private set; }
        public bool Moving => Speed > _settings.StillSpeed;

        public void Advance(float deltaTime, float speed, float turnRate)
        {
            bool wasMoving = Moving;
            Speed = speed;
            if (Moving)
            {
                Look = new Vector2(Mathf.Clamp(turnRate * _settings.LookIntoTurn, -1f, 1f), 0f);
                Lean = Mathf.Clamp(turnRate * _settings.LeanIntoTurn, -1f, 1f);
                _pendingFidget = -1;
                return;
            }

            if (wasMoving)
                Stopped();
            Lean = 0f;
            _stillFor += deltaTime;
            if (_stillFor >= _nextGlance)
            {
                Look = Glance();
                _nextGlance = _stillFor + Range(_settings.GlanceInterval);
            }
            if (!_fidgetRolled && _fidgets > 0 && _stillFor >= _fidgetAt)
            {
                _fidgetRolled = true;
                if (_random.NextDouble() < _settings.FidgetChance)
                    _pendingFidget = _random.Next(_fidgets);
            }
        }

        // The fidget to start now, once; -1 when none is due.
        public int TakeFidget()
        {
            int fidget = _pendingFidget;
            _pendingFidget = -1;
            return fidget;
        }

        void Stopped()
        {
            Look = Vector2.zero;
            _stillFor = 0f;
            _nextGlance = Range(_settings.GlanceInterval) * 0.5f;
            _fidgetAt = Range(_settings.FidgetDelay);
            _fidgetRolled = false;
        }

        Vector2 Glance()
        {
            if (_random.NextDouble() < _settings.GlanceForwardChance)
                return Vector2.zero;
            float x = Mathf.Lerp(-1f, 1f, (float)_random.NextDouble()) * _settings.GlanceRange.x;
            float y = Mathf.Lerp(-1f, 1f, (float)_random.NextDouble()) * _settings.GlanceRange.y;
            return new Vector2(x, y);
        }

        float Range(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)_random.NextDouble());
    }
}
