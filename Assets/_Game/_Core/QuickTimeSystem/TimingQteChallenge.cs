using UnityEngine;

namespace MB13.Aipocalypse.Core.QuickTimeSystem
{
    [CreateAssetMenu(menuName = "Aipocalypse/QTE/Repeating Timing", fileName = "TimingQte")]
    public sealed class TimingQteChallenge : QteChallenge
    {
        [Min(0.1f), Tooltip("Real seconds for standalone timed events. Owner-controlled events last until their owner finishes.")] public float TotalDuration = 4f;
        [Min(0.1f), Tooltip("Real seconds for one complete fill.")] public float CycleDuration = 0.9f;
        [Range(0.05f, 0.95f)] public float GreenZoneCenter = 0.72f;
        [Range(0.01f, 0.9f)] public float GreenZoneWidth = 0.18f;
        [Tooltip("Prevents button mashing; a missed input gets another attempt on the next fill.")]
        public bool OneAttemptPerCycle = true;
        [Min(0f), Tooltip("Real seconds to hold the pointer after a missed press; repair keeps progressing.")]
        public float MissPauseDuration = 0.24f;
        public bool RelocateZoneOnMiss = true;
        [Range(0f, 1f), Tooltip("Minimum normalized zone movement after a miss, limited by the available track space.")]
        public float MinimumZoneShift = 0.16f;

        public override IQteSession CreateSession(bool ownerControlled = false) => new TimingQteSession(this, ownerControlled);
    }

    public sealed class TimingQteSession : IQteSession
    {
        readonly float _duration;
        readonly float _cycleDuration;
        readonly bool _oneAttemptPerCycle;
        readonly float _missPauseDuration;
        readonly bool _relocateZoneOnMiss;
        readonly float _minimumZoneShift;
        readonly System.Func<float> _random;
        float _elapsed;
        float _pointerElapsed;
        float _missPauseRemaining;
        int _attemptedCycle = -1;

        public TimingQteSession(TimingQteChallenge settings, bool ownerControlled = false, System.Func<float> random = null)
        {
            _duration = ownerControlled ? float.PositiveInfinity : Mathf.Max(0.1f, settings.TotalDuration);
            _cycleDuration = Mathf.Max(0.1f, settings.CycleDuration);
            _oneAttemptPerCycle = settings.OneAttemptPerCycle;
            _missPauseDuration = Mathf.Max(0f, settings.MissPauseDuration);
            _relocateZoneOnMiss = settings.RelocateZoneOnMiss;
            _minimumZoneShift = Mathf.Max(0f, settings.MinimumZoneShift);
            _random = random ?? (() => UnityEngine.Random.value);
            ZoneWidth = Mathf.Clamp(settings.GreenZoneWidth, 0.01f, 0.9f);
            ZoneStart = Mathf.Clamp(settings.GreenZoneCenter - ZoneWidth * 0.5f, 0f, 1f - ZoneWidth);
        }

        public QteResult Result { get; private set; }
        public float Remaining => Mathf.Max(0f, _duration - _elapsed);
        public float RemainingFraction => float.IsPositiveInfinity(_duration) ? 1f : Remaining / _duration;
        public float Progress => Mathf.Repeat(_pointerElapsed / _cycleDuration, 1f);
        public float ZoneStart { get; private set; }
        public float ZoneWidth { get; }
        public int Misses { get; private set; }
        public bool IsPointerPaused => _missPauseRemaining > 0f;
        public bool AttemptAvailable => Result == QteResult.Running && !IsPointerPaused && (!_oneAttemptPerCycle || _attemptedCycle != Cycle);
        int Cycle => Mathf.FloorToInt(_pointerElapsed / _cycleDuration);

        public void Tick(float unscaledDeltaTime)
        {
            if (Result != QteResult.Running)
                return;
            float delta = Mathf.Max(0f, unscaledDeltaTime);
            _elapsed += delta;
            if (_elapsed >= _duration)
            {
                Result = QteResult.TimedOut;
                return;
            }
            if (IsPointerPaused)
            {
                float held = Mathf.Min(delta, _missPauseRemaining);
                _missPauseRemaining -= held;
                delta -= held;
                if (!IsPointerPaused && _relocateZoneOnMiss)
                    RelocateZone();
            }
            _pointerElapsed += delta;
        }

        public QteAttemptResult Press()
        {
            if (Result != QteResult.Running || !AttemptAvailable)
                return QteAttemptResult.Ignored;
            _attemptedCycle = Cycle;
            if (Progress >= ZoneStart && Progress <= ZoneStart + ZoneWidth)
            {
                Result = QteResult.Success;
                return QteAttemptResult.Success;
            }
            Misses++;
            _missPauseRemaining = _missPauseDuration;
            if (!IsPointerPaused && _relocateZoneOnMiss)
                RelocateZone();
            return QteAttemptResult.Miss;
        }

        void RelocateZone()
        {
            float range = 1f - ZoneWidth;
            float shift = Mathf.Min(_minimumZoneShift, range * 0.5f);
            float left = Mathf.Max(0f, ZoneStart - shift);
            float rightStart = Mathf.Min(range, ZoneStart + shift);
            float right = range - rightStart;
            float available = left + right;
            if (available <= 0.00001f)
                ZoneStart = ZoneStart >= range * 0.5f ? 0f : range;
            else
            {
                float sample = Mathf.Clamp01(_random()) * available;
                ZoneStart = Mathf.Clamp(sample < left ? sample : rightStart + sample - left, 0f, range);
            }
        }

        public void Cancel()
        {
            if (Result == QteResult.Running)
                Result = QteResult.Cancelled;
        }
    }
}
