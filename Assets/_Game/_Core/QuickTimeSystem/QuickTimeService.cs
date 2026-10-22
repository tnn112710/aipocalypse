using System;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.TimeSystem;
using Aipocalypse.Input;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Aipocalypse.Core.QuickTimeSystem
{
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

        public QuickTimeService(InputService input, TimeService time, IPublisher<QteStarted> started, IPublisher<QteEnded> ended,
            ISubscriber<PlayerDied> playerDied, ISubscriber<ActivePlayerChanged> playerChanged, IPublisher<QteAttempted> attempted)
        {
            _input = input;
            _time = time;
            _started = started;
            _ended = ended;
            _attempted = attempted;
            _playerDied = playerDied.Subscribe(_ => CancelCurrent());
            _playerChanged = playerChanged.Subscribe(_ => CancelCurrent());
        }

        public IQteSession Session { get; private set; }
        public string Presentation { get; private set; }
        public bool IsActive => Session != null;
        public bool IsActiveFor(object owner) => IsActive && ReferenceEquals(owner, _owner);

        public bool TryStart(object owner, QteChallenge challenge, Action<QteResult> completed, bool ownerControlled = false)
        {
            if (IsActive || owner == null || challenge == null)
                return false;
            IQteSession session = challenge.CreateSession(ownerControlled);
            if (session == null)
                return false;
            _owner = owner;
            _completed = completed;
            Session = session;
            string[] presentations = challenge.Presentations;
            Presentation = presentations != null && presentations.Length > 0
                ? presentations[UnityEngine.Random.Range(0, presentations.Length)] : string.Empty;
            if (challenge.TimeScale < 1f)
                _time.Request(this, challenge.TimeScale);
            _started.Publish(new QteStarted(session, Presentation));
            return true;
        }

        public void Tick()
        {
            if (!IsActive || _time.IsPaused || !_input.GameplayEnabled)
                return;
            bool pressed = _input.ReloadPressed;
            if (pressed)
                _input.ConsumeReloadPress();
            Advance(Time.unscaledDeltaTime, pressed);
        }

        public void Advance(float unscaledDeltaTime, bool pressed)
        {
            if (!IsActive || _time.IsPaused)
                return;
            Session.Tick(unscaledDeltaTime);
            IQteSession session = Session;
            if (pressed)
            {
                QteAttemptResult attempt = session.Press();
                if (attempt != QteAttemptResult.Ignored)
                    _attempted.Publish(new QteAttempted(session, attempt));
            }
            if (!ReferenceEquals(Session, session))
                return;
            if (Session.Result != QteResult.Running)
                Finish(Session.Result);
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
            Presentation = null;
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
