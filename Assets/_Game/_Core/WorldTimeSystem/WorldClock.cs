using System;
using System.Collections.Generic;
using Aipocalypse.Core.Events;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Aipocalypse.Core.WorldTimeSystem
{
    // The in-game clock. Runs on scaled time, so TimeService pauses stop it; its own speed requests only slow or freeze the clock.
    // Per-frame values are read (Now, Phase); hour, phase and day changes are published.
    public sealed class WorldClock : ITickable
    {
        const double Epsilon = 1e-9;

        readonly ClockSettings _settings;
        readonly IPublisher<HourChanged> _hourChanged;
        readonly IPublisher<DayPhaseChanged> _phaseChanged;
        readonly IPublisher<DayStarted> _dayStarted;
        readonly IPublisher<TimeJumped> _jumped;
        readonly IPublisher<TimeSkipEnded> _skipEnded;
        readonly Dictionary<object, float> _speedRequests = new();

        double _totalHours;
        int _day;
        DayPhase _phase;
        bool _skipping;
        double _skipTarget;
        double _skipRate;

        public WorldClock(
            ClockSettings settings,
            IPublisher<HourChanged> hourChanged,
            IPublisher<DayPhaseChanged> phaseChanged,
            IPublisher<DayStarted> dayStarted,
            IPublisher<TimeJumped> jumped,
            IPublisher<TimeSkipEnded> skipEnded)
        {
            _settings = settings;
            _hourChanged = hourChanged;
            _phaseChanged = phaseChanged;
            _dayStarted = dayStarted;
            _jumped = jumped;
            _skipEnded = skipEnded;
            Reset(WorldTime.At(settings.StartDay, settings.StartHour));
        }

        public ClockSettings Settings => _settings;
        public WorldTime Now => new(_totalHours);
        public DayPhase Phase => _phase;
        public bool IsSkipping => _skipping;
        // 0 new moon, 0.5 full moon.
        public float MoonPhase => (float)_settings.MoonPhaseAt(Now);
        // Combined speed requests: the slowest wins, 1 when there are none.
        public float SpeedScale { get; private set; } = 1f;
        // In-game hours per real second right now.
        public float HoursPerSecond => _skipping ? (float)_skipRate : _settings.HoursPerSecond(_phase) * SpeedScale;

        public void Tick() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            double seconds = deltaTime;
            for (int guard = 0; seconds > 0d && guard < 256; guard++)
            {
                double rate = HoursPerSecond;
                if (rate <= 0d)
                    return;

                double next = NextEvent(out bool hourEvent, out bool phaseEvent, out DayPhase nextPhase);
                double limit = _skipping ? Math.Min(next, _skipTarget) : next;
                double reach = _totalHours + seconds * rate;
                if (reach < limit - Epsilon)
                {
                    _totalHours = reach;
                    return;
                }

                seconds -= (limit - _totalHours) / rate;
                _totalHours = limit;
                if (limit >= next - Epsilon)
                    Cross(hourEvent, phaseEvent, nextPhase);
                if (_skipping && _totalHours >= _skipTarget - Epsilon)
                    EndSkip();
            }
        }

        // Instantly to the next time the clock shows this hour (no-op if it shows it now).
        public void JumpTo(float hour) => SetTime(Now.AddHours(Now.HoursUntil(hour)));

        public void JumpToPhase(DayPhase phase) => JumpTo(_settings.Timing(phase).Start);
        public void JumpToNextHour() => SetTime(new WorldTime(Math.Floor(_totalHours + Epsilon) + 1d));
        public void JumpToNextPhase() => JumpToPhase(ClockSettings.Next(_phase));

        // Instantly to any time, also backwards (debug, loading a save). Publishes TimeJumped, then the day and phase changes.
        // A running skip ends first, so whoever waits for TimeSkipEnded (sleep) is never left hanging.
        public void SetTime(WorldTime time)
        {
            WorldTime from = Now;
            if (time.Equals(from))
                return;
            CancelSkip();

            _totalHours = time.TotalHours;
            _jumped.Publish(new TimeJumped(from, time));

            int day = time.Day;
            if (day != _day)
            {
                _day = day;
                _dayStarted.Publish(new DayStarted(day));
            }

            DayPhase phase = _settings.PhaseAt(time.Hour);
            if (phase != _phase)
            {
                DayPhase previous = _phase;
                _phase = phase;
                _phaseChanged.Publish(new DayPhaseChanged(previous, phase, time));
            }
        }

        // Fast-forward to the next time the clock shows this hour over realSeconds (scaled time). Hours pass normally,
        // so every HourChanged / DayPhaseChanged is sent. 0 seconds = JumpTo.
        public void SkipTo(float hour, float realSeconds) => SkipTo(Now.AddHours(Now.HoursUntil(hour)), realSeconds);

        public void SkipTo(WorldTime target, float realSeconds)
        {
            double hours = target.TotalHours - _totalHours;
            if (hours <= Epsilon)
                return;
            if (realSeconds <= 0f)
            {
                SetTime(target);
                return;
            }

            _skipping = true;
            _skipTarget = target.TotalHours;
            _skipRate = hours / realSeconds;
        }

        public void CancelSkip()
        {
            if (_skipping)
                EndSkip();
        }

        // Slow (0–1) or freeze (0) the clock for an owner (cutscene, tutorial, menu). Release restores the others' requests.
        public void RequestSpeed(object owner, float scale)
        {
            _speedRequests[owner] = Mathf.Max(0f, scale);
            ApplySpeed();
        }

        public void Freeze(object owner) => RequestSpeed(owner, 0f);

        public void Release(object owner)
        {
            if (_speedRequests.Remove(owner))
                ApplySpeed();
        }

        // Real seconds until the clock next shows this hour at the current speed (skips not included).
        public double RealSecondsUntil(float hour) =>
            _settings.RealSecondsBetween(Now, Now.AddHours(Now.HoursUntil(hour)), SpeedScale);

        void Reset(WorldTime time)
        {
            _totalHours = time.TotalHours;
            _day = time.Day;
            _phase = _settings.PhaseAt(time.Hour);
        }

        // The next whole hour or the start of the next phase in the cycle, whichever comes first. Stepping to the next phase
        // (instead of "the nearest start") means a frame ending just before a boundary can never skip that phase.
        double NextEvent(out bool hourEvent, out bool phaseEvent, out DayPhase nextPhase)
        {
            const double Coincide = 1e-6;
            double hourOfDay = _totalHours - Math.Floor(_totalHours / WorldTime.HoursPerDay) * WorldTime.HoursPerDay;
            nextPhase = ClockSettings.Next(_phase);
            double nextHour = Math.Floor(_totalHours + Epsilon) + 1d;
            double nextPhaseTime = _totalHours + _settings.HoursUntilStart(hourOfDay, nextPhase);
            double next = Math.Min(nextHour, nextPhaseTime);
            hourEvent = nextHour <= next + Coincide;
            phaseEvent = nextPhaseTime <= next + Coincide;
            return next;
        }

        void Cross(bool hourEvent, bool phaseEvent, DayPhase nextPhase)
        {
            WorldTime now = Now;
            if (hourEvent)
            {
                int day = now.Day;
                if (day != _day)
                {
                    _day = day;
                    _dayStarted.Publish(new DayStarted(day));
                }
                _hourChanged.Publish(new HourChanged(now));
            }

            if (phaseEvent && nextPhase != _phase)
            {
                DayPhase previous = _phase;
                _phase = nextPhase;
                _phaseChanged.Publish(new DayPhaseChanged(previous, nextPhase, now));
            }
        }

        void EndSkip()
        {
            _skipping = false;
            _skipEnded.Publish(new TimeSkipEnded(Now));
        }

        void ApplySpeed()
        {
            float scale = 1f;
            bool any = false;
            foreach (float request in _speedRequests.Values)
            {
                scale = any ? Mathf.Min(scale, request) : request;
                any = true;
            }
            SpeedScale = scale;
        }
    }
}
