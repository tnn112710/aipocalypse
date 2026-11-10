using System;
using MB13.Aipocalypse.Core.Events;
using MessagePipe;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.SaveSystem
{
    // Scene: ties a world scene to the GameSession. On the scene's first frame (after every Start) it restores the loaded
    // save or starts a new game; from then on it asks for an autosave each morning (SaveSettings).
    public sealed class WorldSaver : IPostStartable, IDisposable
    {
        readonly GameSession _session;
        readonly SaveSettings _settings;
        readonly ISubscriber<DayPhaseChanged> _phaseChanged;
        IDisposable _subscription;

        public WorldSaver(GameSession session, SaveSettings settings, ISubscriber<DayPhaseChanged> phaseChanged)
        {
            _session = session;
            _settings = settings;
            _phaseChanged = phaseChanged;
        }

        public void PostStart()
        {
            _session.EnterWorld(this);
            // After the restore: moving the clock to the saved time must not count as a new morning.
            _subscription = _phaseChanged.Subscribe(OnPhaseChanged);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _session.LeaveWorld(this);
        }

        void OnPhaseChanged(DayPhaseChanged changed)
        {
            if (changed.Current == _settings.AutosavePhase && changed.Now.Day >= _settings.FirstAutosaveDay)
                _session.RequestAutosave();
        }
    }
}
