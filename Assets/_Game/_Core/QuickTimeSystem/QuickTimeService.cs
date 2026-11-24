using System;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.RandomSystem;
using MB13.Aipocalypse.Core.TimeSystem;
using MB13.Aipocalypse.Input;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.QuickTimeSystem
{
    // Runs one QTE at a time for any owner (a gun's repair today): ticks it in real time while gameplay input is on and the
    // game isn't paused, feeds it the challenge's button and announces QteStarted / QteAttempted / QteEnded for the HUD.
    public sealed class QuickTimeService : ITickable, IDisposable
    {
        readonly InputService _input;
        readonly TimeService _time;
        readonly IPublisher<QteStarted> _started;
        readonly IPublisher<QteEnded> _ended;
        readonly IPublisher<QteAttempted> _attempted;
        Action<QteResult> _completed;
        object _owner;
        readonly IDisposable _playerDied;
        readonly IDisposable _playerChanged;
        // Where a timing zone moves after a miss decides the repair: a gameplay roll.
        readonly Func<float> _roll;

        public QuickTimeService(InputService input, TimeService time, RandomService random, IPublisher<QteStarted> started,
            IPublisher<QteEnded> ended, ISubscriber<PlayerDied> playerDied, ISubscriber<ActivePlayerChanged> playerChanged,
            IPublisher<QteAttempted> attempted)
        {
            RandomStream stream = random.Stream("qte");
            _roll = () => stream.Value;
            _input = input;
            _time = time;
            _started = started;
            _ended = ended;
            _attempted = attempted;
            _playerDied = playerDied.Subscribe(_ => CancelCurrent());
            _playerChanged = playerChanged.Subscribe(_ => CancelCurrent());
        }

        public IQteSession Session { get; private set; }
        public GameButton Button { get; private set; }
        public bool IsActive => Session != null;
        public bool IsActiveFor(object owner) => IsActive && ReferenceEquals(owner, _owner);

        // Gameplay input is on: a QTE started now can be answered (not while a menu is open).
        public bool CanAnswer => _input.GameplayEnabled;

        public bool TryStart(object owner, QteChallenge challenge, Action<QteResult> completed, bool ownerControlled = false)
        {
            if (IsActive || owner == null || challenge == null)
                return false;
            IQteSession session = challenge.CreateSession(ownerControlled, _roll);
            if (session == null)
                return false;
            _owner = owner;
            _completed = completed;
            Session = session;
            string[] presentations = challenge.Presentations;
            string presentation = presentations != null && presentations.Length > 0
                ? presentations[UnityEngine.Random.Range(0, presentations.Length)] : string.Empty;
            Button = challenge.Button;
            if (!ownerControlled && challenge.TimeScale < 1f)
                _time.Request(this, challenge.TimeScale);
            _started.Publish(new QteStarted(session, presentation, Button));
            return true;
        }

        public void Tick()
        {
            if (!IsActive || _time.IsPaused || !_input.GameplayEnabled)
                return;
            // Consumed here, before Player.Update reads it, so a press that completes a gun repair doesn't also start a reload.
            bool pressed = _input.WasPressed(Button);
            if (pressed)
                _input.Consume(Button);
            Advance(Time.unscaledDeltaTime, pressed);
        }

        public void Advance(float unscaledDeltaTime, bool pressed)
        {
            if (!IsActive || _time.IsPaused)
                return;
            IQteSession session = Session;
            // Judged where the pointer was drawn, before this frame moves it, so a hitch can't push a good press out of the zone.
            if (pressed)
            {
                QteAttemptResult attempt = session.Press();
                if (attempt != QteAttemptResult.Ignored)
                    _attempted.Publish(new QteAttempted(session, attempt));
                if (!ReferenceEquals(Session, session))
                    return;
            }
            if (session.Result == QteResult.Running)
                session.Tick(Mathf.Min(unscaledDeltaTime, Time.maximumDeltaTime));
            if (session.Result != QteResult.Running)
                Finish(session.Result);
        }

        public void Cancel(object owner)
        {
            if (IsActiveFor(owner))
                Finish(QteResult.Cancelled);
        }

        void CancelCurrent()
        {
            if (IsActive)
                Finish(QteResult.Cancelled);
        }

        void Finish(QteResult result)
        {
            IQteSession session = Session;
            if (result == QteResult.Cancelled)
                session.Cancel();
            Action<QteResult> completed = _completed;
            Session = null;
            _owner = null;
            _completed = null;
            _time.Release(this);
            _ended.Publish(new QteEnded(session, result));
            completed?.Invoke(result);
        }

        public void Dispose()
        {
            _playerDied.Dispose();
            _playerChanged.Dispose();
            CancelCurrent();
        }
    }
}
