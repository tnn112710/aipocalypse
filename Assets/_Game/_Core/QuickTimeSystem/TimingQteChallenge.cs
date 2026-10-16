using UnityEngine;

namespace Aipocalypse.Core.QuickTimeSystem
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

        public override IQteSession CreateSession(bool ownerControlled = false) => new TimingQteSession(this, ownerControlled);
    }

    public sealed class TimingQteSession : IQteSession
    {
        readonly float _duration;
        readonly float _cycleDuration;
        readonly bool _oneAttemptPerCycle;
        float _elapsed;
        int _attemptedCycle = -1;

        public TimingQteSession(TimingQteChallenge settings, bool ownerControlled = false)
        {
            _duration = ownerControlled ? float.PositiveInfinity : Mathf.Max(0.1f, settings.TotalDuration);
            _cycleDuration = Mathf.Max(0.1f, settings.CycleDuration);
            _oneAttemptPerCycle = settings.OneAttemptPerCycle;
            ZoneWidth = Mathf.Clamp(settings.GreenZoneWidth, 0.01f, 0.9f);
            ZoneStart = Mathf.Clamp(settings.GreenZoneCenter - ZoneWidth * 0.5f, 0f, 1f - ZoneWidth);
        }

        public QteResult Result { get; private set; }
        public float Remaining => Mathf.Max(0f, _duration - _elapsed);
        public float RemainingFraction => float.IsPositiveInfinity(_duration) ? 1f : Remaining / _duration;
        public float Progress => Mathf.Repeat(_elapsed / _cycleDuration, 1f);
        public float ZoneStart { get; }
        public float ZoneWidth { get; }
        public int Misses { get; private set; }
        public bool AttemptAvailable => !_oneAttemptPerCycle || _attemptedCycle != Cycle;
        int Cycle => Mathf.FloorToInt(_elapsed / _cycleDuration);

        public void Tick(float unscaledDeltaTime)
        {
            if (Result != QteResult.Running)
                return;
            _elapsed += Mathf.Max(0f, unscaledDeltaTime);
            if (_elapsed >= _duration)
                Result = QteResult.TimedOut;
        }

        public void Press()
        {
            if (Result != QteResult.Running || !AttemptAvailable)
                return;
            _attemptedCycle = Cycle;
            if (Progress >= ZoneStart && Progress <= ZoneStart + ZoneWidth)
                Result = QteResult.Success;
            else
                Misses++;
        }

        public void Cancel()
        {
            if (Result == QteResult.Running)
                Result = QteResult.Cancelled;
        }
    }
}
