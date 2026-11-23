using System;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.RaidSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MB13.Aipocalypse.Input;
using UnityEngine;
using UnityEngine.Localization;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.SleepSystem
{
    public enum SleepStage
    {
        None,
        // The wake-up clock is open.
        Choosing,
        LyingDown,
        // Lying: the eyes close.
        FallingAsleep,
        // The hours pass.
        Asleep,
        // The eyes open, then the player gets up.
        Waking,
    }

    // Scene service: one sleep from the bed's prompt to standing beside it again. Choose a wake-up time, lie down (a pose
    // sequence in the bed's space), the eyes close, the clock skips with every hour passing, the eyes open, get up. A
    // sleep never passes the start of a raid that hasn't run (SleepRules). The screen (SleepScreen) reads this and calls
    // Shift / Confirm / Cancel. Scaled time: a pause freezes it.
    public sealed class SleepService : ITickable, IDisposable
    {
        static readonly LocalizedString TooLate = Loc.Hud("sleep.too_late");

        readonly SleepSettings _settings;
        readonly WorldClock _clock;
        readonly InputService _input;
        readonly AudioService _audio;
        readonly RaidDirector _raids;
        readonly RaidSettings _raidSettings;
        readonly IRaidSchedule _schedule;
        readonly Func<int, bool> _raidDue;

        Bed _bed;
        IPlayer _player;
        SleepWindow _window;
        float _stageTime;
        bool _skipStarted;

        public SleepService(SleepSettings settings, WorldClock clock, InputService input, AudioService audio, IObjectResolver resolver)
        {
            _settings = settings;
            _clock = clock;
            _input = input;
            _audio = audio;
            // Raids are optional: without them nothing limits a sleep.
            if (resolver.TryResolve(out _raids))
            {
                resolver.TryResolve(out _raidSettings);
                resolver.TryResolve(out _schedule);
            }
            _raidDue = RaidDue;
        }

        public bool Enabled => _settings != null && _settings.Enabled;
        public SleepStage Stage { get; private set; }
        // The bed of the sleep under way (null before the first).
        public Bed Bed => _bed;
        public bool IsBusy => Stage != SleepStage.None;
        // The time the player wakes at (while choosing: the choice so far).
        public WorldTime Wake { get; private set; }
        public WorldTime Now => _clock.Now;
        public SleepWindow Window => _window;
        // Whole hours from now to the wake-up time, rounded up.
        public int HoursAsleep => Mathf.Max(0, Mathf.CeilToInt((float)(Wake.TotalHours - _clock.Now.TotalHours - 0.01)));
        // How shut the eyes are: 0 open .. 1 closed.
        public float Darkness { get; private set; }
        // Why the last Begin was refused; shown by the screen for a moment.
        public string Refusal { get; private set; }
        public int RefusalCount { get; private set; }

        // When the player could wake if they went to bed now.
        public SleepWindow CurrentWindow()
        {
            SleepRaids? raids = _raids != null && _raidSettings != null
                ? new SleepRaids(_raids.IsRunning, _raidSettings.StartHour, _raidSettings.DawnHour, _raidDue)
                : null;
            return SleepRules.Window(_clock.Now, _settings.MinHours, _settings.MaxHours, _settings.WakeBeforeRaid, raids);
        }

        // Opens the wake-up clock, or refuses with a reason (Refusal).
        public bool Begin(Bed bed, IPlayer player)
        {
            if (!Enabled || IsBusy || bed == null || player == null || player.IsDead)
                return false;
            SleepWindow window = CurrentWindow();
            if (!window.CanSleep)
            {
                Refusal = window.Reason.Text();
                RefusalCount++;
                return false;
            }
            _bed = bed;
            _player = player;
            _window = window;
            Wake = SleepRules.DefaultWake(window, _settings.DefaultWakeHour);
            _input.RequestUI(this);
            SetStage(SleepStage.Choosing);
            return true;
        }

        // Moves the chosen wake-up time by whole hours, inside the window.
        public void Shift(int hours)
        {
            if (Stage != SleepStage.Choosing)
                return;
            double time = Math.Clamp(Wake.TotalHours + hours, _window.Earliest.TotalHours, _window.Latest.TotalHours);
            Wake = new WorldTime(time);
        }

        public void Cancel()
        {
            if (Stage == SleepStage.Choosing)
                End();
        }

        public void Confirm()
        {
            if (Stage != SleepStage.Choosing)
                return;
            // The clock ran while it was open: the window is checked once more.
            SleepWindow window = CurrentWindow();
            if (!window.CanSleep || Wake.TotalHours < window.Earliest.TotalHours - 0.001)
            {
                Refusal = (window.CanSleep ? TooLate : window.Reason).Text();
                RefusalCount++;
                End();
                return;
            }
            if (Wake.TotalHours > window.Latest.TotalHours)
                Wake = window.Latest;
            _input.ReleaseUI(this);
            _audio.Play(_settings.LieDownSound);
            _player.PlaySequence(_settings.LieDown, _bed.Anchor);
            SetStage(SleepStage.LyingDown);
        }

        public void Tick()
        {
            if (Stage == SleepStage.None || Stage == SleepStage.Choosing)
                return;
            if (_player == null || _player.IsDead || _bed == null)
            {
                End();
                return;
            }

            _stageTime += Time.deltaTime;
            switch (Stage)
            {
                case SleepStage.LyingDown:
                    if (_settings.LieDown == null || _player.SequenceSettled)
                        SetStage(SleepStage.FallingAsleep);
                    break;
                case SleepStage.FallingAsleep:
                    Darkness = _settings.FadeOut > 0f ? Mathf.Clamp01(_stageTime / _settings.FadeOut) : 1f;
                    if (Darkness >= 1f)
                        SetStage(SleepStage.Asleep);
                    break;
                case SleepStage.Asleep:
                    Darkness = 1f;
                    if (!_skipStarted)
                    {
                        _skipStarted = true;
                        _clock.SkipTo(Wake, _settings.SkipSeconds);
                    }
                    else if (!_clock.IsSkipping)
                    {
                        _audio.Play(_settings.WakeSound);
                        _player.PlaySequence(_settings.GetUp, _bed.Anchor);
                        SetStage(SleepStage.Waking);
                    }
                    break;
                case SleepStage.Waking:
                    Darkness = _settings.FadeIn > 0f ? 1f - Mathf.Clamp01(_stageTime / _settings.FadeIn) : 0f;
                    if (Darkness <= 0f && (_settings.GetUp == null || _player.SequenceSettled))
                        End();
                    break;
            }
        }

        public void Dispose() => End();

        // Back to nothing from any stage: the player stands where the get-up ends (beside the bed) and has the camera back.
        void End()
        {
            if (Stage == SleepStage.None)
                return;
            SleepStage stage = Stage;
            _input.ReleaseUI(this);
            if (stage == SleepStage.Asleep)
                _clock.CancelSkip();
            if (stage != SleepStage.Choosing && _player != null && _player.InSequence && !_player.IsDead)
            {
                if (_bed != null && TryExitPose(out Vector3 position, out float yaw))
                    _player.Teleport(position, yaw);
                _player.StopSequence();
            }
            Darkness = 0f;
            _bed = null;
            _player = null;
            SetStage(SleepStage.None);
        }

        // Where the get-up's last beat stands: on the floor beside the bed, facing the way the beat looks.
        bool TryExitPose(out Vector3 position, out float yaw)
        {
            position = default;
            yaw = 0f;
            PoseSequence getUp = _settings.GetUp;
            if (getUp == null || getUp.Beats.Length == 0)
                return false;
            PoseBeat last = getUp.Beats[^1];
            Transform anchor = _bed.Anchor;
            position = anchor.TransformPoint(new Vector3(last.Offset.x, 0f, last.Offset.y));
            if (Physics.Raycast(position + Vector3.up, Vector3.down, out RaycastHit floor, 3f, getUp.GroundMask, QueryTriggerInteraction.Ignore))
                position = floor.point;
            yaw = anchor.eulerAngles.y + last.Yaw;
            return true;
        }

        bool RaidDue(int night) =>
            night > 0 && !ContainsNight(night) && (_schedule == null || _schedule.PlanFor(night) != null);

        bool ContainsNight(int night)
        {
            foreach (int handled in _raids.HandledNights)
                if (handled == night)
                    return true;
            return false;
        }

        void SetStage(SleepStage stage)
        {
            Stage = stage;
            _stageTime = 0f;
            _skipStarted = false;
        }
    }
}
